# Docker Konténerizáció - MovieShop Alkalmazás

## Áttekintés
A MovieShop alkalmazás teljes mértékben konténerizált Docker környezetben fut, 4 fő szolgáltatással:
- **Frontend** (React + Vite + Nginx)
- **Backend** (.NET 8 API)
- **Database** (PostgreSQL)
- **Streaming Service** (FFmpeg + Node.js)

---

## 1. Docker Compose Konfiguráció

### **docker-compose.yml**
- **Központi orchestrációs fájl** amely definiálja az összes szolgáltatást
- **Hálózatok**: Közös `movieshop-network` bridge hálózat a szolgáltatások közötti kommunikációhoz
- **Volumes**: Perzisztens adattárolás az adatbázishoz és videó fájlokhoz
- **Függőségek**: Backend függ a DB-től, frontend függ a backend-től
- **Egészségellenőrzések**: Health check minden szolgáltatáshoz a megbízható indulásért

---

## 2. Frontend Konténer (movieshop.client)

### **Dockerfile (Multi-stage build)**
- **Build Stage**:
  - Node.js 20 Alpine image
  - `npm ci` - tiszta dependency telepítés
  - `npm run build` - Vite production build
  - Eredmény: `/app/dist` mappában optimalizált statikus fájlok

- **Production Stage**:
  - Nginx Alpine image (lightweight)
  - Build kimenet másolása `/usr/share/nginx/html`-be
  - Custom nginx.conf a React Router támogatásához

### **nginx.conf**
- **SPA routing**: Minden kérés az index.html-re irányítva
- **API proxy**: `/api` prefix továbbítása a backend konténerhez
- **CORS és cache beállítások**: Optimális fejléc konfigurációk

### **Portok**
- Container port: 80
- Host port: 3000 (http://localhost:3000)

---

## 3. Backend Konténer (MovieShop.Server)

### **Dockerfile (Multi-stage build)**
- **Build Stage**:
  - .NET 8 SDK image
  - `dotnet restore` - NuGet package visszaállítás
  - `dotnet build -c Release` - optimalizált build
  - `dotnet publish -c Release` - production ready output

- **Runtime Stage**:
  - .NET 8 ASP.NET Runtime image (kisebb méret)
  - FFmpeg telepítés (videó feldolgozáshoz)
  - `/app/movie-files` mappa létrehozása videó tároláshoz
  - Published kimenet másolása

### **Volumes**
- `movie-files-volume:/app/movie-files` - videó fájlok perzisztens tárolása
- Megosztott a streaming service-szel

### **Environment Variables**
- `ASPNETCORE_ENVIRONMENT=Development`
- Connection string PostgreSQL-hez
- JWT és Google OAuth konfigurációk
- Azure Blob Storage connection string

### **Portok**
- Container port: 8080
- Host port: 5000 (http://localhost:5000)

---

## 4. Database Konténer

### **postgres:15-alpine**
- **Lightweight Alpine verzió** (kisebb image méret)
- **Perzisztens volume**: `postgres-data:/var/lib/postgresql/data`
- **Kezdő adatbázis**: `movieshopdb`
- **User**: `movieshopuser` / `movieshoppass`
- **Health check**: `pg_isready` parancs az elérhetőség ellenőrzésére

### **Portok**
- Container port: 5432
- Host port: 5432 (külső kapcsolatokhoz)

---

## 5. Streaming Service Konténer

### **Dockerfile**
- **Node.js 18 image**
- **FFmpeg telepítés**: Videó transzkódoláshoz szükséges
- **Express szerver**: Egyszerű HTTP API a transzkódolás triggerelésére
- **Shared volume**: `/app/movie-files` - hozzáférés a backend által feltöltött videókhoz

### **Funkciók**
- **HLS transzkódolás**: MP4 → .m3u8 + .ts szegmensek
- **Multi-quality output**: 480p, 720p, 1080p változatok
- **Adaptive streaming**: Automatikus minőség váltás a sávszélesség alapján

### **Portok**
- Container port: 3001
- Host port: 3001

---

## 6. Hálózati Konfiguráció

### **movieshop-network (bridge)**
- **Belső DNS**: Szolgáltatások elérik egymást névvel (pl. `movieshop-backend`)
- **Izolált környezet**: Biztonságos kommunikáció a konténerek között
- **Service discovery**: Automatikus névfeloldás

### **Inter-service kommunikáció**
- Frontend → Backend: `http://movieshop-backend:8080/api`
- Backend → Database: `Host=movieshop-db;Port=5432`
- Backend → Streaming: `http://movieshop-streaming:3001`

---

## 7. Volume Management

### **Perzisztens Volume-ok**
1. **postgres-data**
   - Cél: Adatbázis adatok megőrzése
   - Mount: `/var/lib/postgresql/data`

2. **movie-files-volume**
   - Cél: Feltöltött videók és transzkódolt fájlok
   - Mount: `/app/movie-files` (backend + streaming)
   - Megosztott: Mindkét szolgáltatás írhat/olvashat

---

## 8. Build és Deploy Folyamat

### **Docker Image Build**
```bash
# Teljes stack build
docker-compose build

# Egyedi szolgáltatás build (cache-el)
docker-compose build backend

# Tiszta build (no cache)
docker-compose build --no-cache frontend
```

### **Konténerek Indítása**
```bash
# Összes szolgáltatás indítása (detached mode)
docker-compose up -d

# Egyedi szolgáltatás újraindítása
docker-compose up -d backend

# Szolgáltatások leállítása
docker-compose down

# Leállítás volume törlésével
docker-compose down -v
```

### **Monitoring és Debug**
```bash
# Konténer státusz ellenőrzése
docker ps

# Logok megtekintése
docker logs movieshop-backend
docker logs -f movieshop-frontend  # Follow mode

# Konténerbe belépés
docker exec -it movieshop-backend bash

# Health check státusz
docker inspect movieshop-db | grep -i health
```

---

## 9. Környezeti Változók és Konfiguráció

### **Backend (appsettings.json)**
- **Database**: Connection string PostgreSQL-hez (konténer név használata)
- **JWT**: Token generáláshoz és validáláshoz
- **Google OAuth**: ClientId és ClientSecret
- **Azure Blob Storage**: Connection string videó tároláshoz
- **CORS**: Frontend origin engedélyezése

### **Frontend (hardcoded)**
- **API_BASE_URL**: `http://localhost:5000` (host gépről)
- **Google Client ID**: OAuth bejelentkezéshez
- **Vite config**: Proxy beállítások development módban

---

## 10. Biztonsági Szempontok

### **Implemented**
- ✅ **Health checks**: Automatikus újraindítás hibás konténereknél
- ✅ **Non-root user**: Backend és frontend nem root jogosultsággal fut
- ✅ **Network isolation**: Bridge hálózat, csak szükséges portok nyitva
- ✅ **Environment separation**: Development/Production konfigurációk

### **Production Ready Improvements (Jövőbeli)**
- 🔄 **Secrets management**: Docker secrets használata jelszavakhoz
- 🔄 **HTTPS**: Nginx SSL/TLS konfiguráció
- 🔄 **Rate limiting**: API védelme túlterhelés ellen
- 🔄 **Logging**: Centralizált log gyűjtés (ELK stack, Grafana)
- 🔄 **Container scanning**: Biztonsági sérülékenységek ellenőrzése

---

## 11. CI/CD Pipeline Alapok

### **Jelenleg**
- Manuális build és deploy parancsokkal
- Git verziókezelés az összes Dockerfile-hoz

### **Lehetséges Továbbfejlesztés**
- GitHub Actions workflow
- Automated testing konténer indítása előtt
- Docker image push Docker Hub-ra vagy private registry-be
- Staging és production környezetek
- Automatic deployment Kubernetes-re (K8s)

---

## 12. Teljesítmény Optimalizációk

### **Multi-stage builds**
- Kisebb production image méret (SDK vs Runtime)
- Frontend: Node build → Nginx serve (~150MB → ~30MB)
- Backend: .NET SDK → ASP.NET Runtime (~700MB → ~200MB)

### **Alpine Linux**
- Minimal base image (PostgreSQL, Node.js, Nginx)
- Gyorsabb build és deploy idők
- Kisebb attack surface

### **Layer caching**
- Dependency telepítés külön rétegekben
- Kód változás nem triggerel teljes rebuild-et
- `npm ci` és `dotnet restore` cache-elve

---

## Összefoglalás

A MovieShop alkalmazás **production-ready Docker környezetben** fut, amely:
- ✅ **Skálázható**: Minden szolgáltatás függetlenül skálázható
- ✅ **Izolált**: Hálózati szegmentáció és volume elkülönítés
- ✅ **Megbízható**: Health checks és automatikus újraindítás
- ✅ **Karbantartható**: Egyértelmű konfiguráció és dokumentáció
- ✅ **Teljesítmény-optimalizált**: Multi-stage builds, Alpine images, layer caching

**Következő lépések**: Kubernetes orchestráció, monitoring stack (Prometheus/Grafana), automated CI/CD pipeline.
