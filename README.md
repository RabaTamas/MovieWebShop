# MovieWebShop

**Felhőalapú filmértékesítő és streaming platform mikroszervíz architektúrával és AI integrációval**

Diplomatervezési projekt — egy olyan webalkalmazás, amely egyszerre valósítja meg a hagyományos webáruház-funkcionalitást, az adaptív videóstreaminget, a valós idejű közösségi funkciókat és a mesterséges intelligencia alapú szolgáltatásokat. A felhasználók filmeket böngészhetnek és vásárolhatnak Stripe-on keresztül, a megvásárolt tartalmakat HLS-protokollal streamelhetik, valós idejű aukciókon licitálhatnak, szinkronizált Watch Party szobában nézhetnek együtt filmet, személyre szabott ajánlásokat kapnak, és egy agentic AI chatbottal léphetnek kapcsolatba, amely a nevükben műveleteket is végrehajt.

---

## Tartalom

- [Funkciók](#funkciók)
- [Architektúra](#architektúra)
- [Technológiai stack](#technológiai-stack)
- [Indítás](#indítás)
- [Konfiguráció](#konfiguráció)
- [Projektstruktúra](#projektstruktúra)
- [Adatmodell](#adatmodell)
- [Továbbfejlesztési terv](#továbbfejlesztési-terv)

---

## Funkciók

### Webshop
- **Filmkatalógus** kategóriaszűréssel, ársávval, rendezéssel és lapozással
- **Elasticsearch-alapú keresés**: teljes szöveges `multi_match` keresés címre és leírásra, `prefix`-alapú autocomplete 350 ms debounce-szal
- **Bevásárlókosár** a kosárba helyezéskori ár rögzítésével (`PriceAtOrder`), így az árváltozás nem hat vissza a kosárra
- **Stripe fizetés** `PaymentIntent` API-val — a kártyaadatok soha nem érintik az alkalmazás szerverét (`PaymentElement`, PCI-DSS-kompatibilis)
- **Rendeléskezelés** állapotgéppel: `Pending → Processing → Shipped → Delivered`, illetve `Cancelled`
- **Értékelések** azoktól a felhasználóktól, akik megvásárolták az adott filmet
- **Soft-delete** a filmeken (`IsDeleted`, `DeletedAt`), hogy a korábbi rendelések hivatkozásai sértetlenek maradjanak

### Videóstreaming
- **HLS adaptív streaming**: az admin által feltöltött videót az `FFmpeg` több felbontásra (480p / 720p / 1080p) kódolja át, majd egy `master.m3u8` mesterlejátszási lista fogja össze a szinteket
- **Azure Blob Storage** tárolja a szegmenseket és a lejátszási listákat; a backend időkorlátos **SAS URL**-eket generál, és menet közben írja át a manifest hivatkozásait
- **HLS.js** kliensoldali lejátszó dinamikus minőségválasztóval — „Auto" módban a sávszélesség alapján vált, kézzel is rögzíthető felbontás
- **Lejátszási pozíció mentése** (`VideoProgress`): 30 másodpercenként, szüneteltetéskor és oldalelhagyáskor is (`keepalive` fetch), visszatéréskor „Folytatás" ajánlattal
- **Watch Party**: hatjegyű szobakód, SignalR-szinkronizált play/pause/seek, szerveroldali időbélyeg alapú **latenciakompenzáció**, beépített csevegőpanel, automatikus gazdaátadás, ha a host lecsatlakozik

### Valós idejű aukció
- **SignalR** (`AuctionHub`) csoportonkénti valós idejű licitfrissítés: ár, licitáló neve, hátralévő idő, licitlista — oldalfrissítés nélkül
- **Optimista konkurenciavezérlés** SQL Server `rowversion` mezővel; ütközés esetén a service háromszor újrapróbálja friss adatolvasással
- **Anti-sniping**: az utolsó 60 másodpercben leadott licit további 60 másodperccel hosszabbítja az aukciót
- **Automatikus állapotátmenetek** (`Pending → Active → Ended`) és Stripe-alapú kifizetés a nyertesnek a `/my-wins` oldalon

### AI szolgáltatások
- **Agentic chatbot** (`AgentService`): OpenAI-kompatibilis function calling a Groq API-n, nyolc eszközzel — `add_movie_to_cart`, `remove_movie_from_cart`, `search_movie`, `navigate_to_page`, `watch_movie`, `start_watch_party`, `update_display_name`, `set_billing_address`. Az agentic ciklus legfeljebb három iterációt tesz meg; a végrehajtott műveletek egy `AgentAction` payloadon keresztül azonnal tükröződnek a frontenden.
- **Kontextusalapú chat** (`ChatService`): háromrétegű rendszerkontextus — a felhasználó vásárlási előzményei, az ajánlórendszer aktuális kimenete, és a teljes filmkatalógus. A modell kizárólag a katalógusban létező filmeket ajánlhatja.
- **Munkamenet-kezelés**: `ConcurrentDictionary` alapú in-memory tárolás, sessionönként max. 20 üzenet, 30 perc inaktivitás után automatikus törlés
- **Hangalapú bevitel és felolvasás** a böngésző natív Web Speech API-jával (`SpeechRecognition` + `SpeechSynthesis`)
- **Ajánlórendszer** két párhuzamos algoritmussal: **kategóriaalapú szűrés** (kategória-átfedés + népszerűség szerinti rangsor, szöveges indoklással) és **kollaboratív szűrés** (a 20 legtöbb közös vásárlással rendelkező hasonló felhasználó alapján)

### Biztonság és hitelesítés
- **JWT** alapú állapotmentes hitelesítés; SignalR esetén a token query stringből (`access_token`) érkezik, mert a WebSocket nem tud HTTP-fejlécet küldeni
- **ASP.NET Core Identity** felhasználó- és szerepkörkezelés, három szerepkörrel: látogató / regisztrált felhasználó / adminisztrátor
- **Google OAuth 2.0** bejelentkezés
- **TOTP kétfaktoros hitelesítés** (RFC 6238): `otpauth://` URI-ból `qrcode.react` QR-kód, ellenőrzés az Identity `VerifyTwoFactorTokenAsync` metódusával
- **Authorization policy**-k (`RequireAdminRole`, `RequireUserRole`) minden védett végponton

### Adminisztrátori panel
Teljes CRUD a rendszer valamennyi entitásán: filmek (TMDB-ből importálható metaadatokkal), kategóriák, felhasználók, rendelések, értékelések, kosarak, címek, aukciók. Külön videófeltöltő oldal a transzkódolás állapotkövetésével.

---

## Architektúra

Háromrétegű (three-tier) webalkalmazás, ahol a rétegek között szigorú felelősségi határok húzódnak. A backenden belül az adatáramlás vertikálisan szervezett: **Controller → Service → EF Core → adatbázis**. A controllerek nem tartalmaznak üzleti logikát, csak validálnak, service-t hívnak és HTTP-választ állítanak össze; a teljes üzleti logika a service rétegben él, az AutoMapper pedig a domain modellek és a DTO-k között képez le.

```
┌──────────────────────────────────────────────────────────────────┐
│  React 19 SPA (Vite + Bootstrap 5)                    :3000      │
│  REST/JSON  ·  JWT Bearer  ·  SignalR WebSocket  ·  HLS.js       │
└───────────────┬──────────────────────────────┬───────────────────┘
                │ REST + SignalR               │ HLS (SAS URL)
┌───────────────▼──────────────────────────────┼───────────────────┐
│  ASP.NET Core 8 API                   :5000  │                   │
│  Controllers → Services → EF Core            │                   │
│  Hubs: /hubs/auction, /hubs/watchparty       │                   │
│  Hangfire (háttérfeladatok)  ·  FFmpeg       │                   │
└──┬──────────────┬───────────────┬────────────┼───────────────────┘
   │              │               │            │
┌──▼─────────┐ ┌──▼────────────┐ ┌▼─────────┐ ┌▼──────────────────┐
│ SQL Server │ │ Elasticsearch │ │  Groq    │ │ Azure Blob Storage│
│   :1433    │ │    :9200      │ │   API    │ │  (HLS szegmensek) │
└────────────┘ └───────────────┘ └──────────┘ └───────────────────┘
```

Néhány kiemelt tervezési döntés:

- **Elasticsearch-szinkronizáció EF Core interceptorral.** Az [ElasticsearchSyncInterceptor](MovieShop/MovieShop.Server/Data/Interceptors/ElasticsearchSyncInterceptor.cs) minden `SaveChanges`/`SaveChangesAsync` után átvizsgálja a `ChangeTracker`-t, és a módosított `Movie` entitásokat automatikusan indexeli vagy törli az indexből. A service réteg kódjában így egyetlen explicit indexelési hívás sincs.
- **Historikus ár rögzítése.** Az `OrderMovie` és `ShoppingCartMovie` közbenső entitások eltárolják a tranzakció pillanatában érvényes árat, így későbbi árváltozás nem írja felül a múltat.
- **Migrációk indításkor.** A backend startup során lefuttatja a `Database.MigrateAsync()`-et, majd beveti a szerepköröket és az admin felhasználót ([Program.cs:209-232](MovieShop/MovieShop.Server/Program.cs#L209-L232)) — nincs kézi migrációs lépés a telepítésben.
- **Streaming két úton.** Az elsődleges út az Azure Blob Storage + SAS URL; emellett fut egy nginx konténer `secure_link` modullal ([nginx-streaming/nginx.conf](MovieShop/nginx-streaming/nginx.conf)), amely aláírt, lejáró URL-ekkel szolgálja ki a lokálisan tárolt fájlokat.

---

## Technológiai stack

| Réteg | Technológia |
|---|---|
| **Frontend** | React 19, Vite 6, React Router 7, Bootstrap 5, Axios |
| **Média / valós idő** | HLS.js, `@microsoft/signalr`, Web Speech API |
| **Fizetés / UI kiegészítők** | `@stripe/react-stripe-js`, Recharts, `qrcode.react`, lucide-react |
| **Backend** | ASP.NET Core 8 (LTS), C#, Entity Framework Core 9, AutoMapper, Hangfire |
| **Hitelesítés** | ASP.NET Core Identity, JWT, Google OAuth 2.0, TOTP (RFC 6238) |
| **Adatbázis** | Microsoft SQL Server 2022 (Code First migrációk) |
| **Keresés** | Elasticsearch 7.17 + NEST kliens |
| **Média** | FFmpeg, HLS (RFC 8216), Azure Blob Storage |
| **Külső API-k** | Stripe, Groq (Llama), TMDB |
| **Infrastruktúra** | Docker, docker-compose, nginx |
| **Dokumentáció** | Swagger / Swashbuckle (OpenAPI) |

---

## Indítás

### Előfeltételek

- Docker Desktop *(a teljes stackhez)*
- .NET 8 SDK vagy újabb és Node.js 20+ *(lokális fejlesztéshez)*

### A) Teljes stack Docker Composeval — ajánlott

A [MovieShop/](MovieShop/) mappából:

```bash
docker-compose up -d --build
```

Öt konténer indul: `movieshop-db`, `movieshop-backend`, `movieshop-frontend`, `movieshop-elasticsearch`, `movieshop-streaming`. A backend induláskor automatikusan lefuttatja a migrációkat és beveti az admin felhasználót.

| Szolgáltatás | URL |
|---|---|
| Frontend | http://localhost:3000 |
| Backend API + Swagger | http://localhost:5000/swagger |
| Hangfire dashboard | http://localhost:5000/hangfire |
| Elasticsearch | http://localhost:9200 |
| SQL Server | `localhost:1433` |
| Streaming (nginx) | http://localhost:8080 |

Hasznos parancsok:

```bash
docker-compose logs -f backend       # logok követése
docker-compose up -d --build backend # újraépítés kódmódosítás után
docker-compose down                  # leállítás (az adatok megmaradnak)
docker-compose down -v               # leállítás a volume-ok törlésével
```

> A konténerekben nincs hot reload — kódmódosítás után újra kell építeni az érintett service-t, vagy váltani a lokális fejlesztői módra.

### B) Lokális fejlesztői mód (hot reload)

Két terminál szükséges.

**Backend** — a [MovieShop/MovieShop.Server/](MovieShop/MovieShop.Server/) mappából:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --urls "http://localhost:5000"
```

Fontos, hogy az 5000-es porton fusson, mert a frontend alapértelmezetten oda irányítja az API-hívásokat. Az adatbázis ilyenkor LocalDB (`(localdb)\MSSQLLocalDB`), a migrációk itt is automatikusan lefutnak.

**Frontend** — a [MovieShop/movieshop.client/](MovieShop/movieshop.client/) mappából:

```bash
npm install     # csak az első alkalommal
npm run dev
```

A Vite dev szerver a https://localhost:5173 címen indul (a tanúsítványt a `vite.config.js` generálja `dotnet dev-certs`-szel) — a backend CORS-konfigurációja pontosan ezt az origint engedélyezi.

A kereséshez az Elasticsearch is kell; önmagában felhúzható: `docker-compose up -d elasticsearch`.

---

## Konfiguráció

A backend beállításai [appsettings.json](MovieShop/MovieShop.Server/appsettings.json)-ből, konténerben pedig környezeti változókból (`Section__Key` formában) érkeznek. A Compose a [MovieShop/](MovieShop/) mappában lévő `.env` fájlból olvassa a titkokat.

| Kulcs | Szerep |
|---|---|
| `ConnectionStrings__DefaultConnection` | SQL Server kapcsolati sztring |
| `Jwt__Key` / `Jwt__Issuer` / `Jwt__Audience` / `Jwt__ExpiryInDays` | JWT token generálás és validáció |
| `Authentication__Google__ClientId` / `__ClientSecret` | Google OAuth |
| `Stripe__SecretKey` / `Stripe__PublishableKey` | fizetési integráció |
| `Groq__ApiKey` | AI chatbot és agent |
| `TmdbApi__ApiKey` | filmmetaadatok importja |
| `AzureBlob__ConnectionString` / `__ContainerName` | videótárolás |
| `Elasticsearch__Url` | keresőmotor |
| `Streaming__BaseUrl` | az nginx streaming szolgáltatás címe |
| `CORS__AllowedOrigins` | engedélyezett frontend originek (vesszővel elválasztva) |

A frontend a `VITE_API_URL` változót olvassa ([src/config/api.js](MovieShop/movieshop.client/src/config/api.js)), fallbackként `http://localhost:5000`-t használ.

> ⚠️ A repóban jelenleg fejlesztői kulcsok vannak verziókövetve az `appsettings.json`-ban. Éles környezet előtt ezeket ki kell emelni környezeti változókba vagy secret managerbe, és rotálni kell őket.

---

## Projektstruktúra

```
MovieWebShop/
├── MovieShop/
│   ├── MovieShop.Server/            # ASP.NET Core 8 REST API
│   │   ├── Controllers/             # 17 controller (Movie, Auction, Chat, Payment, ...)
│   │   ├── Services/
│   │   │   ├── Interfaces/          # service szerződések
│   │   │   └── Implementations/     # üzleti logika (Chat, Agent, Auction, Transcoding, ...)
│   │   ├── Models/                  # domain entitások
│   │   ├── DTOs/                    # adatátviteli objektumok
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   └── Interceptors/        # ElasticsearchSyncInterceptor
│   │   ├── Hubs/                    # AuctionHub, WatchPartyHub
│   │   ├── Migrations/              # EF Core Code First migrációk
│   │   └── Program.cs               # DI, middleware, migrációk, seed
│   ├── movieshop.client/            # React 19 SPA
│   │   └── src/
│   │       ├── pages/               # Home, MovieDetails, Cart, Auctions, WatchParty, Admin*, ...
│   │       ├── components/          # Chatbot, MovieCard, StripeCheckout, PrivateRoute, ...
│   │       ├── contexts/            # AuthContext
│   │       └── config/              # API base URL
│   ├── nginx-streaming/             # nginx secure_link streaming konténer
│   ├── movie-files/                 # videófájlok (volume mount)
│   └── docker-compose.yml
└── docs_*.md                        # diplomaterv fejezetei
```

### Kliensoldali útvonalak

| Nyilvános | Bejelentkezve | Admin |
|---|---|---|
| `/`, `/about` | `/cart`, `/orders`, `/profile` | `/admin/movies` (+ add / edit / categories / video) |
| `/movies/:id` | `/my-movies`, `/my-movies/:id/watch` | `/admin/categories`, `/admin/users` |
| `/auctions`, `/auctions/:id` | `/my-movies/:id/watch-party` | `/admin/orders`, `/admin/orders/:id` |
| `/login`, `/register` | `/my-wins`, `/recommendations` | `/admin/reviews`, `/admin/carts` |
| | `/profile/2fa` | `/admin/addresses`, `/admin/auctions` |

---

## Adatmodell

A `MovieWebShopDB` adatbázis EF Core Code First migrációkkal épül. A fontosabb entitások:

| Entitás | Szerep |
|---|---|
| `User` | `IdentityUser<int>` leszármazott; címek, rendelések, kosár, értékelések navigációval |
| `Movie` | cím, leírás, `Price` + opcionális `DiscountedPrice`, `VideoFileName`, soft-delete mezők, audit időbélyegek |
| `Category` | M:N kapcsolat a filmekkel (`MovieCategories` kapcsolótábla) |
| `Order` / `OrderMovie` | rendelés `OrderStatus`-szal; a tétel rögzíti a `PriceAtOrder` historikus árat |
| `ShoppingCart` / `ShoppingCartMovie` | felhasználónként egy aktív kosár, kosárba helyezéskori árral |
| `Review` | filmértékelés szöveges tartalommal és audit mezőkkel |
| `Address` | szállítási és számlázási címként egyaránt hivatkozható |
| `Auction` / `Bid` | filmhez köthető vagy önálló tárgy; `RowVersion` optimista zároláshoz, `IsPaid` a kifizetéshez |
| `VideoProgress` | felhasználó–film páronként a lejátszási pozíció másodpercben |
| `ConversationHistory` / `ConversationMessage` | **nem adatbázis-entitások** — a chatbot munkamenetei memóriában élnek |

---

## Továbbfejlesztési terv

A következő félévre tervezett irányok:

- **Mikroszervíz architektúra** — a monolit backend szolgáltatásokra bontása (User, Catalog, Order), API Gateway (Ocelot) és aszinkron üzenetsor (Azure Service Bus) mellett
- **Azure deployment** — Static Web Apps (frontend), Container Apps (backend), Azure SQL, Container Registry, GitHub Actions CI/CD
- **Redis cache** a gyakran lekérdezett adatokra (népszerű filmek, kategóriák, chatbot kontextus)
- **Monitoring és terheléstesztelés** — Prometheus + Grafana metrikák, k6 terhelési szcenáriók az architekturális döntések számszerűsítéséhez
- **Frontend modernizálás** — a Bootstrap fokozatos kiváltása Tailwind CSS + shadcn/ui komponensekre
- **PWA** támogatás `vite-plugin-pwa`-val: offline cache és push értesítések

---

## Szerző

**Rába Tamás** — mérnökinformatikus MSc, diplomatervezés, 2025/26.
