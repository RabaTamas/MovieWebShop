"""
MovieShop — monolit ↔ mikroszervíz egyezésvizsgálat
=====================================================

A két architektúrát ugyanazokon a végpontokon hívja meg, és összeveti:
  1. a HTTP státuszkódokat,
  2. a JSON válaszok SZERKEZETÉT (kulcsok és típusok — az adatok eltérhetnek,
     hiszen a két rendszernek külön adatbázisa van),
  3. a kulcsfunkciók tényleges működését: TMDB, chatbot, agent-navigáció,
     kétfaktoros hitelesítés (valódi TOTP-kóddal), Google-login hibaága.

A teszt NEM a valódi admin fiókkal dolgozik (azon 2FA lehet bekapcsolva), hanem
mindkét rendszerben létrehoz egy ideiglenes admin felhasználót, és a végén törli.

Használat (mindkét stacknek futnia kell):
    python services/parity-test.py
"""

import base64
import hashlib
import hmac
import json
import pathlib
import random
import string
import struct
import subprocess
import sys
import time
import urllib.error
import urllib.request

MONO = "http://localhost:5000"
MS = "http://localhost:5200"
SEED_ADMIN = {"email": "admin@movieshop.com", "password": "Admin123!"}
TEST_PASSWORD = "Parity123!"
ENV_FILE = pathlib.Path(__file__).resolve().parent.parent / ".env"

# Szótárak, amelyek kulcsai ADATOK (pl. státusznevek, felbontások), nem mezőnevek —
# ezeknél csak azt nézzük, hogy mindkét oldalon szótár-e.
DYNAMIC_MAPS = {"ordersbystatus", "transcodedversions", "availablequalities"}

passed = failed = skipped = 0


def ok(msg):
    global passed
    passed += 1
    print(f"  [OK]   {msg}")


def bad(msg):
    global failed
    failed += 1
    print(f"  [HIBA] {msg}")


def skip(msg):
    global skipped
    skipped += 1
    print(f"  [SKIP] {msg}")


def section(title):
    print(f"\n=== {title} ===")


def env_value(name, default=""):
    try:
        for line in ENV_FILE.read_text(encoding="utf-8").splitlines():
            if line.startswith(name + "="):
                return line.split("=", 1)[1].strip()
    except OSError:
        pass
    return default


def mono_sql(query):
    """SQL a monolit adatbázisában — csak az ideiglenes tesztfelhasználók kezeléséhez."""
    password = env_value("SA_PASSWORD", "YourStrong@Password123")
    return subprocess.run(
        ["docker", "exec", "movieshop-db", "/opt/mssql-tools18/bin/sqlcmd",
         "-S", "localhost", "-U", "sa", "-P", password, "-C", "-d", "MovieWebShopDB",
         "-h", "-1", "-W", "-b", "-Q", "SET NOCOUNT ON; " + query],
        capture_output=True, text=True, encoding="utf-8", errors="replace")


def call(base, method, path, body=None, token=None, timeout=90):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(base + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            raw = r.read().decode("utf-8", "replace")
            status = r.status
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8", "replace")
        status = e.code
    except Exception as e:  # kapcsolati hiba
        return 0, str(e)
    try:
        return status, json.loads(raw) if raw else None
    except json.JSONDecodeError:
        return status, raw


def shape(value, key=None):
    """A JSON érték szerkezete: kulcsok és típusok, értékek nélkül."""
    if isinstance(value, dict):
        if key in DYNAMIC_MAPS:
            return "map"
        return {k.lower(): shape(v, k.lower()) for k, v in value.items()}
    if isinstance(value, list):
        return [shape(value[0])] if value else []
    if value is None:
        return "null"
    return type(value).__name__


def diff_shapes(mono, ms, path="$"):
    """A monolitban meglévő, de a mikroszervízből hiányzó vagy eltérő típusú mezők."""
    problems = []
    if isinstance(mono, dict):
        if not isinstance(ms, dict):
            return [f"{path}: objektum helyett {ms}"]
        for key, sub in mono.items():
            if key not in ms:
                problems.append(f"{path}.{key} hiányzik")
            else:
                problems += diff_shapes(sub, ms[key], f"{path}.{key}")
    elif isinstance(mono, list):
        if not isinstance(ms, list):
            return [f"{path}: tömb helyett {ms}"]
        if mono and ms:
            problems += diff_shapes(mono[0], ms[0], f"{path}[0]")
    elif mono != ms and "null" not in (mono, ms) and {mono, ms} != {"int", "float"}:
        problems.append(f"{path}: {mono} ↔ {ms}")
    return problems


def random_suffix():
    return "".join(random.choices(string.ascii_lowercase + string.digits, k=8))


def register(base, prefix):
    suffix = random_suffix()
    email = f"{prefix}_{suffix}@test.local"
    status, body = call(base, "POST", "/api/Auth/register",
                        {"name": f"{prefix}{suffix}", "email": email,
                         "password": TEST_PASSWORD, "confirmPassword": TEST_PASSWORD})
    if status != 200 or not isinstance(body, dict):
        return email, None, None
    return email, body.get("token"), (body.get("user") or {}).get("id")


def login(base, email, password):
    status, body = call(base, "POST", "/api/Auth/login", {"email": email, "password": password})
    return body.get("token") if status == 200 and isinstance(body, dict) else None


def totp(shared_key, at=None):
    raw = shared_key.replace(" ", "").upper()
    key = base64.b32decode(raw + "=" * (-len(raw) % 8))
    counter = int((at or time.time()) // 30)
    digest = hmac.new(key, struct.pack(">Q", counter), hashlib.sha1).digest()
    offset = digest[-1] & 0x0F
    code = (struct.unpack(">I", digest[offset:offset + 4])[0] & 0x7FFFFFFF) % 1_000_000
    return f"{code:06d}"


def find_movie_id(base, title):
    status, movies = call(base, "GET", "/api/Movie")
    if status == 200 and isinstance(movies, list) and movies:
        for m in movies:
            if m.get("title", "").lower() == title.lower():
                return m["id"]
        return movies[0]["id"]
    return None


def data_dependent_reason(template, m_body, s_body):
    """Ha a két válasz csak az ADATOK miatt tér el (nem a kód miatt), megadja az okát."""
    if "/Video/" in template and isinstance(m_body, dict) and isinstance(s_body, dict):
        if m_body.get("hasVideo") != s_body.get("hasVideo"):
            return (f"a monolitban hasVideo={m_body.get('hasVideo')}, a mikroszervízben "
                    f"hasVideo={s_body.get('hasVideo')} — más ágat ad vissza, mert csak az egyikben van feltöltött videó")
    return None


cleanup = []  # (leírás, függvény)

try:
    # ── 0. Ideiglenes admin felhasználók ─────────────────────────────────────
    section("0. Ideiglenes admin tesztfelhasználó mindkét rendszerben")

    mono_email, _, mono_uid = register(MONO, "parity_admin")
    ms_email, _, ms_uid = register(MS, "parity_admin")
    ms_root = login(MS, SEED_ADMIN["email"], SEED_ADMIN["password"])

    if mono_uid:
        cleanup.append(("monolit tesztadmin törlése",
                        lambda: mono_sql(f"DELETE FROM AspNetUsers WHERE Email = '{mono_email}'")))
    if ms_uid:
        cleanup.append(("mikroszervíz tesztadmin törlése",
                        lambda: call(MS, "DELETE", f"/api/User/{ms_uid}",
                                     token=login(MS, SEED_ADMIN["email"], SEED_ADMIN["password"]))))

    # Monolit: admin token nélkül a szerepkör csak közvetlenül adható meg
    granted = mono_sql(
        "INSERT INTO AspNetUserRoles (UserId, RoleId) "
        "SELECT u.Id, r.Id FROM AspNetUsers u CROSS JOIN AspNetRoles r "
        f"WHERE u.Email = '{mono_email}' AND r.Name = 'Admin'") if mono_uid else None

    # Mikroszervíz: a seedelt admin lépteti elő (ott nincs 2FA)
    promoted = call(MS, "POST", f"/api/Auth/make-admin/{ms_uid}", token=ms_root)[0] if ms_uid and ms_root else 0

    mono_token = login(MONO, mono_email, TEST_PASSWORD) if granted and granted.returncode == 0 else None
    ms_token = login(MS, ms_email, TEST_PASSWORD) if promoted == 200 else None

    (ok if mono_token else bad)(f"monolit tesztadmin ({mono_email})"
                                + ("" if mono_token else f" — sql: {granted.stderr.strip() if granted else 'regisztráció sikertelen'}"))
    (ok if ms_token else bad)(f"mikroszervíz tesztadmin ({ms_email})")
    if not (mono_token and ms_token):
        raise SystemExit(1)

    mono_movie = find_movie_id(MONO, "Inception")
    ms_movie = find_movie_id(MS, "Inception")

    # ── 1. Végpontok ─────────────────────────────────────────────────────────
    section("1. Végpontok összevetése (státuszkód + JSON szerkezet)")

    endpoints = [
        ("/api/Movie", False),
        ("/api/Movie/{movie}", False),
        ("/api/Movie/categories?categoryIds=1", False),
        ("/api/Movie/admin/all", True),
        ("/api/Movie/admin/{movie}", True),
        ("/api/Movie/purchased", True),
        ("/api/Movie/{movie}/progress", True),
        ("/api/Category", False),
        ("/api/Category/1", False),
        ("/api/Category/1/movie-count", False),
        ("/api/Review", True),
        ("/api/Review/movie/{movie}", False),
        ("/api/Search?q=inception", False),
        ("/api/Search/autocomplete?q=inc", False),
        ("/api/Auction", False),
        ("/api/Auction/all", True),
        ("/api/Auction/my-wins", True),
        ("/api/User/profile", True),
        ("/api/User/all", True),
        ("/api/Address", True),
        ("/api/admin/addresses", True),
        ("/api/ShoppingCart", True),
        ("/api/Order/user", True),
        ("/api/admin/Orders", True),
        ("/api/admin/Orders/statistics", True),
        ("/api/Recommendation", True),
        ("/api/Payment/config", True),
        ("/api/Auth/2fa/status", True),
        ("/api/admin/Video/{movie}/info", True),
    ]

    for template, auth in endpoints:
        m_status, m_body = call(MONO, "GET", template.replace("{movie}", str(mono_movie)),
                                token=mono_token if auth else None)
        s_status, s_body = call(MS, "GET", template.replace("{movie}", str(ms_movie)),
                                token=ms_token if auth else None)

        if m_status != s_status:
            bad(f"{template}: státusz {m_status} ↔ {s_status}")
            continue

        reason = data_dependent_reason(template, m_body, s_body)
        if reason:
            skip(f"{template}: {reason}")
            continue

        problems = diff_shapes(shape(m_body), shape(s_body))
        if problems:
            bad(f"{template}: szerkezeti eltérés — " + "; ".join(problems[:4]))
        else:
            ok(f"{template} ({s_status})")

    # ── 2. TMDB ──────────────────────────────────────────────────────────────
    section("2. TMDB integráció")
    status, body = call(MS, "GET", f"/api/Movie/{ms_movie}")
    tmdb = body.get("tmdbInfo") if isinstance(body, dict) else None
    _, mono_body = call(MONO, "GET", f"/api/Movie/{mono_movie}")
    mono_tmdb = mono_body.get("tmdbInfo") if isinstance(mono_body, dict) else None
    # Nem elég a tmdbId: a pontszámnak és a megjelenési dátumnak is ki kell töltődnie (snake_case deszerializálás)
    tmdb_filled = bool(tmdb and tmdb.get("tmdbId") and tmdb.get("voteAverage", 0) > 0
                       and tmdb.get("voteCount", 0) > 0 and tmdb.get("releaseDate"))
    (ok if tmdb_filled else bad)(f"filmadatlap kitöltött TMDB-adattal: {tmdb}")
    if mono_tmdb and tmdb:
        (ok if mono_tmdb.get("tmdbId") == tmdb.get("tmdbId") and mono_tmdb.get("voteAverage") == tmdb.get("voteAverage") else bad)(
            f"TMDB-adatok egyeznek (monolit {mono_tmdb.get('voteAverage')} / ms {tmdb.get('voteAverage')})")

    for name, base, movie_id in (("monolit", MONO, mono_movie), ("mikroszervíz", MS, ms_movie)):
        status, extras = call(base, "GET", f"/api/Movie/{movie_id}/tmdb")
        filled = (status == 200 and isinstance(extras, dict) and extras.get("backdrops")
                  and extras.get("cast") and extras.get("genres") and extras.get("runtime"))
        (ok if filled else bad)(
            f"{name}: /api/Movie/{{id}}/tmdb bővített adatok ({status}; képek: "
            f"{len(extras.get('backdrops', [])) if isinstance(extras, dict) else '-'}, "
            f"szereplők: {len(extras.get('cast', [])) if isinstance(extras, dict) else '-'})")

    # ── 3–4. Chatbot ─────────────────────────────────────────────────────────
    section("3. Chatbot (kontextusalapú válasz)")
    ERRORS = ("Sorry, I'm having trouble", "Technical error", "couldn't generate", "Sajnálom")
    for name, base, token in [("monolit", MONO, mono_token), ("mikroszervíz", MS, ms_token)]:
        status, body = call(base, "POST", "/api/Chat/ask",
                            {"question": "Do you have Inception movie in your webshop?"}, token)
        answer = body.get("answer", "") if isinstance(body, dict) else str(body)
        if status == 200 and answer and not any(e in answer for e in ERRORS):
            ok(f"{name}: [{body.get('source')}] {answer[:90]}")
        else:
            bad(f"{name}: {status} {answer[:120]}")

    section("4. Chatbot agent (eszközhívás + navigáció)")
    for name, base, token in [("monolit", MONO, mono_token), ("mikroszervíz", MS, ms_token)]:
        status, body = call(base, "POST", "/api/Chat/ask", {"question": "Go to auctions"}, token)
        action = body.get("action") if isinstance(body, dict) else None
        if status == 200 and body.get("source") == "agent" and action and (action.get("payload") or {}).get("page") == "auctions":
            ok(f"{name}: navigate → {action['payload']}")
        else:
            bad(f"{name}: {status} source={body.get('source') if isinstance(body, dict) else '?'} action={action}")

    # ── 5. Kétfaktoros hitelesítés ───────────────────────────────────────────
    section("5. Kétfaktoros hitelesítés (regisztráció → 2FA bekapcsolás → TOTP-s belépés)")
    for name, base, admin_token in [("monolit", MONO, mono_token), ("mikroszervíz", MS, ms_token)]:
        email, token, user_id = register(base, "parity_2fa")
        if not token:
            bad(f"{name}: regisztráció sikertelen")
            continue

        _, setup = call(base, "GET", "/api/Auth/2fa/setup", token=token)
        key = setup.get("sharedKey") if isinstance(setup, dict) else None
        enabled = call(base, "POST", "/api/Auth/2fa/enable", {"code": totp(key)}, token)[0] == 200 if key else False

        _, first = call(base, "POST", "/api/Auth/login", {"email": email, "password": TEST_PASSWORD})
        requires = isinstance(first, dict) and first.get("requiresTwoFactor")
        two_fa_id = first.get("twoFactorUserId") if isinstance(first, dict) else None

        _, final = call(base, "POST", "/api/Auth/2fa/login",
                        {"twoFactorUserId": two_fa_id, "code": totp(key)}) if requires else (0, None)
        final_token = final.get("token") if isinstance(final, dict) else None

        if key and enabled and requires and final_token:
            ok(f"{name}: jelszó után TOTP-kódot kért, a kóddal tokent adott")
        else:
            bad(f"{name}: key={bool(key)} enabled={enabled} requires2fa={requires} token={bool(final_token)}")

        if user_id:
            call(base, "DELETE", f"/api/User/{user_id}", token=admin_token)

    # ── 6. Google-login ──────────────────────────────────────────────────────
    section("6. Google-login (érvénytelen tokennel — a hibaágnak egyeznie kell)")
    m_status, m_body = call(MONO, "POST", "/api/Auth/google-login", {"idToken": "invalid"})
    s_status, s_body = call(MS, "POST", "/api/Auth/google-login", {"idToken": "invalid"})
    same = m_status == s_status and shape(m_body) == shape(s_body)
    (ok if same else bad)(f"státusz {m_status} ↔ {s_status}; monolit: {str(m_body)[:70]} | ms: {str(s_body)[:70]}")

    # ── 7. SignalR ───────────────────────────────────────────────────────────
    section("7. SignalR hubok a gatewayen át (negotiate)")
    for hub in ["auction", "watchparty"]:
        status, _ = call(MS, "POST", f"/hubs/{hub}/negotiate?negotiateVersion=1", token=ms_token)
        (ok if status == 200 else bad)(f"/hubs/{hub}/negotiate → {status}")

    # ── 8. Rendelés életciklusa ──────────────────────────────────────────────
    section("8. Rendelés életciklusa (cím mentése, hozzáférés státuszváltáskor, törlés kaszkádja)")

    def wait_until(predicate, timeout=15):
        """A mikroszervízben az események pár másodperc alatt érnek célba."""
        end = time.time() + timeout
        while time.time() < end:
            try:
                if predicate():
                    return True
            except Exception:
                pass
            time.sleep(1)
        return False

    def purchased_ids(base, token):
        _, movies = call(base, "GET", "/api/Movie/purchased", token=token)
        return {m["id"] for m in movies} if isinstance(movies, list) else set()

    def order_ids(base, admin_token):
        _, orders = call(base, "GET", "/api/admin/Orders", token=admin_token)
        return {o["id"] for o in orders} if isinstance(orders, list) else set()

    for name, base, admin_token, movie_id in [("monolit", MONO, mono_token, mono_movie),
                                               ("mikroszervíz", MS, ms_token, ms_movie)]:
        email, token, user_id = register(base, "parity_order")
        if not token:
            bad(f"{name}: regisztráció sikertelen")
            continue

        _, before = call(base, "GET", "/api/Address", token=token)
        before_count = len(before) if isinstance(before, list) else -1

        call(base, "POST", "/api/ShoppingCart/add", {"movieId": movie_id, "quantity": 1}, token)
        status, order = call(base, "POST", "/api/Order",
                             {"billingAddress": {"street": "Parity utca 1", "city": "Budapest", "zip": "1111"},
                              "movies": [], "totalPrice": 0}, token)
        order_id = order.get("id") if status == 200 and isinstance(order, dict) else None

        if not order_id:
            bad(f"{name}: rendelés létrehozása sikertelen ({status}: {str(order)[:100]})")
            call(base, "DELETE", f"/api/User/{user_id}", token=admin_token)
            continue

        _, after = call(base, "GET", "/api/Address", token=token)
        after_count = len(after) if isinstance(after, list) else -1
        (ok if after_count == before_count + 1 else bad)(
            f"{name}: a számlázási cím bekerült a felhasználó címei közé ({before_count} → {after_count})")

        call(base, "PUT", f"/api/admin/Orders/{order_id}/status", {"status": "Completed"}, admin_token)
        (ok if wait_until(lambda: movie_id in purchased_ids(base, token)) else bad)(
            f"{name}: Completed állapotban a film megjelent a Saját filmjeim között")

        call(base, "PUT", f"/api/admin/Orders/{order_id}/status", {"status": "Pending"}, admin_token)
        (ok if wait_until(lambda: movie_id not in purchased_ids(base, token)) else bad)(
            f"{name}: Pending-re visszaállítva a hozzáférés megszűnt")

        status, _ = call(base, "DELETE", f"/api/User/{user_id}", token=admin_token)
        (ok if status == 204 and wait_until(lambda: order_id not in order_ids(base, admin_token)) else bad)(
            f"{name}: a felhasználó törlésével a rendelése is törlődött (DELETE → {status})")

finally:
    for description, action in cleanup:
        try:
            action()
        except Exception as e:  # a takarítás hibája ne takarja el az eredményt
            print(f"  (takarítás sikertelen: {description}: {e})")

print(f"\n==============================================\n  Eredmény: {passed} sikeres, {failed} hibás, {skipped} adatfüggő (kihagyva)\n==============================================")
sys.exit(0 if failed == 0 else 1)
