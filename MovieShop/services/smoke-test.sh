#!/usr/bin/env bash
# ==============================================================================
# MovieShop mikroszervíz stack — füstteszt
# ==============================================================================
# Végigméri a rendszer legfontosabb útvonalait, kiemelten az eseményvezérelt
# adatreplikációt: azt ellenőrzi, hogy a Catalogban létrehozott film valóban
# megérkezik-e az Order Service MovieSnapshot táblájába a RabbitMQ-n keresztül.
#
# Használat:  bash services/smoke-test.sh
# ==============================================================================

set -uo pipefail

GATEWAY="${GATEWAY:-http://localhost:5200}"
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@movieshop.com}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-Admin123!}"

PASS=0
FAIL=0

ok()   { echo "  [OK]   $1"; PASS=$((PASS+1)); }
bad()  { echo "  [HIBA] $1"; FAIL=$((FAIL+1)); }
head_() { echo; echo "=== $1 ==="; }

# ── 1. Health checkek ────────────────────────────────────────────────────────
head_ "1. Service-ek elérhetősége"

for svc in "gateway:5200" "user:5201" "catalog:5202" "order:5203" "auction:5204" "chat:5205"; do
  name="${svc%%:*}"
  port="${svc##*:}"
  code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 "http://localhost:$port/health" 2>/dev/null)
  if [ "$code" = "200" ]; then ok "$name (:$port) health 200"; else bad "$name (:$port) health $code"; fi
done

code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 "http://localhost:15672" 2>/dev/null)
if [ "$code" = "200" ]; then ok "RabbitMQ kezelőfelület (:15672)"; else bad "RabbitMQ kezelőfelület $code"; fi

# ── 2. Gateway útválasztás ───────────────────────────────────────────────────
head_ "2. Gateway útválasztás (a frontend útvonalain)"

for route in "/api/Movie:Catalog" "/api/Category:Catalog" "/api/Auction:Auction"; do
  path="${route%%:*}"
  svc="${route##*:}"
  code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "$GATEWAY$path")
  if [ "$code" = "200" ]; then ok "$path -> $svc (200)"; else bad "$path -> $svc ($code)"; fi
done

# A védett végpontnak 401-et KELL adnia token nélkül
code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "$GATEWAY/api/ShoppingCart")
if [ "$code" = "401" ]; then ok "/api/ShoppingCart token nélkül 401 (helyes)"; else bad "/api/ShoppingCart token nélkül $code (401 várt)"; fi

# A belső végpontok NEM lehetnek elérhetők kívülről
code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "$GATEWAY/api/internal/catalog/movies")
if [ "$code" = "404" ] || [ "$code" = "403" ]; then
  ok "/api/internal/* nincs közzétéve a gatewayen ($code)"
else
  bad "/api/internal/* elérhető kívülről ($code) — biztonsági rés!"
fi

# ── 3. Hitelesítés ───────────────────────────────────────────────────────────
head_ "3. Bejelentkezés (User Service a gatewayen át)"

LOGIN=$(curl -s --max-time 15 -X POST "$GATEWAY/api/Auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}")

TOKEN=$(echo "$LOGIN" | python3 -c "import sys,json; print(json.load(sys.stdin).get('token',''))" 2>/dev/null)

if [ -n "$TOKEN" ]; then
  ok "Admin bejelentkezés sikeres, JWT megkapva"
else
  bad "Admin bejelentkezés sikertelen: $LOGIN"
  echo; echo "A további tesztek token nélkül nem futtathatók."
  echo "Eredmény: $PASS sikeres, $FAIL hibás"
  exit 1
fi

AUTH="Authorization: Bearer $TOKEN"

# A tokent egy MÁSIK service-nek is el kell fogadnia — ez igazolja, hogy a
# hitelesítés valóban állapotmentes és service-független.
code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 -H "$AUTH" "$GATEWAY/api/ShoppingCart")
if [ "$code" = "200" ]; then ok "A User Service tokenjét az Order Service is elfogadja"; else bad "Order Service a tokennel $code"; fi

code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 -H "$AUTH" "$GATEWAY/api/Movie/admin/all")
if [ "$code" = "200" ]; then ok "A Catalog Service is elfogadja ugyanazt a tokent"; else bad "Catalog Service a tokennel $code"; fi

# ── 4. Eseményvezérelt replikáció ────────────────────────────────────────────
head_ "4. Eseményfolyam: Catalog -> RabbitMQ -> Order"

TITLE="Smoke Test Movie $(date +%s)"

CREATE=$(curl -s --max-time 15 -X POST "$GATEWAY/api/Movie" \
  -H "$AUTH" -H "Content-Type: application/json" \
  -d "{\"title\":\"$TITLE\",\"description\":\"Event flow verification\",\"price\":1500,\"imageUrl\":\"https://example.com/x.jpg\",\"categories\":[]}")

MOVIE_ID=$(echo "$CREATE" | python3 -c "import sys,json; print(json.load(sys.stdin).get('id',''))" 2>/dev/null)

if [ -n "$MOVIE_ID" ] && [ "$MOVIE_ID" != "0" ]; then
  ok "Film létrehozva a Catalogban (id=$MOVIE_ID)"
else
  bad "A film létrehozása sikertelen: $CREATE"
fi

if [ -n "$MOVIE_ID" ] && [ "$MOVIE_ID" != "0" ]; then
  # Az Order Service csak akkor tudja kosárba tenni, ha a MovieChanged esemény
  # megérkezett és feltöltötte a MovieSnapshot táblát. Ez a replikáció bizonyítéka.
  echo "  ... várakozás az esemény terjedésére"
  REPLICATED=0

  for attempt in $(seq 1 15); do
    sleep 1
    code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 -X POST "$GATEWAY/api/ShoppingCart/add" \
      -H "$AUTH" -H "Content-Type: application/json" \
      -d "{\"movieId\":$MOVIE_ID,\"quantity\":1}")

    if [ "$code" = "200" ]; then
      ok "MovieChanged megérkezett az Orderhez ${attempt} mp alatt (kosárba helyezés sikeres)"
      REPLICATED=1
      break
    fi
  done

  [ "$REPLICATED" = "0" ] && bad "A MovieChanged esemény 15 mp alatt sem ért el az Order Service-hez"

  # Takarítás
  curl -s -o /dev/null --max-time 10 -X DELETE "$GATEWAY/api/ShoppingCart/remove/$MOVIE_ID" -H "$AUTH"
  curl -s -o /dev/null --max-time 10 -X DELETE "$GATEWAY/api/Movie/$MOVIE_ID" -H "$AUTH"
  echo "  ... teszt-film törölve"
fi

# ── 5. Ajánlórendszer ────────────────────────────────────────────────────────
head_ "5. Ajánlórendszer (lokális join a MovieSnapshot replikán)"

code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 15 -H "$AUTH" "$GATEWAY/api/Recommendation")
if [ "$code" = "200" ]; then ok "/api/Recommendation 200"; else bad "/api/Recommendation $code"; fi

# ── Összegzés ────────────────────────────────────────────────────────────────
echo
echo "=============================================="
echo "  Eredmény: $PASS sikeres, $FAIL hibás"
echo "=============================================="

[ "$FAIL" -eq 0 ] && exit 0 || exit 1
