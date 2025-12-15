# Video Streaming Megvalósítás - MovieShop Alkalmazás

## Áttekintés
A video streaming funkció **4 fázisban** lett kifejlesztve, a legegyszerűbb megoldástól az ipari szintű adaptive streaming-ig:
- **Fázis 1**: YouTube Trailer Beágyazás
- **Fázis 2**: Lokális Fájlrendszer (movie-files mappa)
- **Fázis 3**: Azure Blob Storage + Direct MP4 Streaming
- **Fázis 4**: HLS Adaptive Streaming (FFmpeg transzkódolás)

---

## Fázis 1: YouTube Trailer Beágyazás (Alapok)

### **Cél**
Gyors proof-of-concept videó megjelenítéshez, külső hosting használatával.

### **Implementáció**

#### **Backend**

**TMDBService - Trailer kulcs lekérése TMDB API-ból:**
```csharp
// TMDB API hívás a film trailer adataihoz, JSON parsing és YouTube key kinyerése
public async Task<TrailerDto> GetMovieTrailerAsync(int tmdbId)
{
    var response = await _httpClient.GetAsync($"movie/{tmdbId}/videos");
    var data = await response.Content.ReadFromJsonAsync<TmdbVideosResponse>();
    
    var trailer = data.Results.FirstOrDefault(v => v.Type == "Trailer" && v.Site == "YouTube");
    
    return new TrailerDto 
    { 
        YoutubeKey = trailer?.Key,
        Name = trailer?.Name 
    };
}
```

**MovieController - Trailer endpoint jogosultság ellenőrzéssel:**
```csharp
// Ellenőrzi hogy a bejelentkezett user megvásárolta-e a filmet, ha igen visszaadja a trailer adatokat
[HttpGet("{id}/trailer")]
[Authorize]
public async Task<ActionResult<TrailerDto>> GetTrailer(int id)
{
    var userId = GetUserIdFromToken();
    
    // Jogosultság ellenőrzés: van-e order a user-hez és movie-hoz
    var hasPurchased = await _context.Orders
        .AnyAsync(o => o.UserId == userId && 
                      o.OrderMovies.Any(om => om.MovieId == id));
    
    if (!hasPurchased)
        return StatusCode(403, new { message = "You need to purchase this movie first." });
    
    var movie = await _context.Movies.FindAsync(id);
    var trailerData = await _tmdbService.GetMovieTrailerAsync(movie.TmdbId);
    
    return Ok(trailerData);
}
```

#### **Frontend (WatchMovie.jsx)**

**YouTube iframe beágyazás 16:9 aspect ratio-val:**
```jsx
// YouTube player beágyazása iframe-mel, autoplay kikapcsolva, kapcsolódó videók letiltva
{trailerData && trailerData.youtubeKey && (
    <div className="ratio ratio-16x9">
        <iframe
            src={`https://www.youtube.com/embed/${trailerData.youtubeKey}?autoplay=0&rel=0`}
            title={trailerData.name || 'Movie Trailer'}
            frameBorder="0"
            allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
            allowFullScreen
        />
    </div>
)}
```

### **Előnyök**
- ✅ Gyors implementáció (1-2 óra)
- ✅ Nincs szerver-oldali tárolás/feldolgozás
- ✅ YouTube infrastruktúra kezelése (CDN, sávszélesség)

### **Hátrányok**
- ❌ Nem a teljes film, csak trailer
- ❌ Külső függőség (YouTube API)
- ❌ Nem kontrollálható lejátszási élmény
- ❌ Reklámok, ajánlások megjelenhetnek

---

## Fázis 2: Lokális Fájlrendszer (movie-files mappa)

### **Cél**
Teljes filmek tárolása és streaming a szerveren belül, külső szolgáltatások nélkül.

### **Implementáció**

#### **Backend (MovieService.cs)**

**Video feltöltés lokális fájlrendszerbe:**
```csharp
// Admin által feltöltött videó fájl mentése a /app/movie-files mappába
public async Task UploadVideoAsync(int movieId, IFormFile videoFile)
{
    var movie = await _context.Movies.FindAsync(movieId);
    if (movie == null) throw new NotFoundException("Movie not found");
    
    // Fájl mentése lokális fájlrendszerbe
    var uploadPath = "/app/movie-files";
    Directory.CreateDirectory(uploadPath);
    
    var fileName = $"{movieId}-{videoFile.FileName}";
    var filePath = Path.Combine(uploadPath, fileName);
    
    using (var stream = new FileStream(filePath, FileMode.Create))
    {
        await videoFile.CopyToAsync(stream);
    }
    
    // Adatbázis frissítése fájlnévvel
    movie.VideoFileName = fileName;
    await _context.SaveChangesAsync();
}
```

**Video streaming endpoint range request támogatással:**
```csharp
// HTTP Range Request-eket támogató stream endpoint (seek funkcióhoz szükséges)
[HttpGet("{id}/stream")]
[Authorize]
public async Task<IActionResult> StreamVideo(int id)
{
    var userId = GetUserIdFromToken();
    
    // Jogosultság ellenőrzés
    var hasPurchased = await _movieService.HasUserPurchasedMovie(userId, id);
    if (!hasPurchased) return Forbid();
    
    var movie = await _context.Movies.FindAsync(id);
    var filePath = Path.Combine("/app/movie-files", movie.VideoFileName);
    
    if (!System.IO.File.Exists(filePath))
        return NotFound("Video file not found");
    
    var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    
    // Range request támogatás (video seek-eléshez)
    return File(fileStream, "video/mp4", enableRangeProcessing: true);
}
```

#### **Docker Volume Konfiguráció**

**docker-compose.yml - Megosztott volume a perzisztens tároláshoz:**
```yaml
# movie-files volume létrehozása és csatolása backend konténerhez
services:
  backend:
    volumes:
      - movie-files-volume:/app/movie-files

volumes:
  movie-files-volume:
    driver: local
```

**Backend Dockerfile - movie-files mappa létrehozása:**
```dockerfile
# Mappa létrehozása és írási jogosultság biztosítása a videó feltöltésekhez
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

RUN mkdir -p /app/movie-files && chmod 777 /app/movie-files

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MovieShop.Server.dll"]
```

#### **Frontend (WatchMovie.jsx)**

**HTML5 video player direktben a backend endpoint-ra mutatva:**
```jsx
// Native HTML5 video player, Authorization header a protected endpoint-hoz
useEffect(() => {
    if (!streamingData?.url) return;
    
    const video = videoRef.current;
    
    // Direct URL a backend stream endpoint-ra
    video.src = streamingData.url; // pl: http://localhost:5000/api/Movie/42/stream
    video.load();
}, [streamingData]);

<video 
    ref={videoRef} 
    controls 
    className="w-100"
>
    Your browser does not support the video tag.
</video>
```

### **Előnyök**
- ✅ Teljes filmek tárolása és streamelése
- ✅ Nincs külső függőség (YouTube, Azure)
- ✅ Gyors fejlesztés (pár óra)
- ✅ Range request támogatás (seek funkció működik)
- ✅ Docker volume perzisztens tárolás

### **Hátrányok**
- ❌ Korlátozott tárkapacitás (szerver HDD/SSD mérete)
- ❌ Nincs CDN (minden request a backend szerverre megy)
- ❌ Nem skálázható (single server bottleneck)
- ❌ Biztonsági kockázat (fájlrendszer hozzáférés)
- ❌ Backup és disaster recovery nehéz
- ❌ Nincs földrajzi redundancia

---

## Fázis 3: Azure Blob Storage + Direct MP4 Streaming

### **Cél**
Teljes filmek tárolása felhőben, biztonságos streaming SAS tokenekkel.

### **Implementáció**

#### **Azure Blob Storage Setup**
- **Container**: `movie-videos` (private hozzáférés)
- **Connection String**: `appsettings.json`-ban tárolva
- **Blob naming**: `{movieId}-{quality}.mp4` (pl. `42-1080p.mp4`)

#### **Backend (MovieService.cs)**

**Video feltöltés Azure Blob Storage-ba:**
```csharp
// Admin által feltöltött videó fájl Azure Blob container-be mentése
public async Task UploadVideoAsync(int movieId, IFormFile videoFile)
{
    var movie = await _context.Movies.FindAsync(movieId);
    if (movie == null) throw new NotFoundException("Movie not found");
    
    // Azure Blob Client inicializálás
    var blobServiceClient = new BlobServiceClient(_configuration["AzureBlobStorage:ConnectionString"]);
    var containerClient = blobServiceClient.GetBlobContainerClient("movie-videos");
    
    // Blob fájlnév és upload
    var blobName = $"{movieId}-original.mp4";
    var blobClient = containerClient.GetBlobClient(blobName);
    
    await blobClient.UploadAsync(videoFile.OpenReadStream(), overwrite: true);
    
    // Adatbázis frissítése
    movie.VideoFileName = blobName;
    await _context.SaveChangesAsync();
}
```

**SAS Token generálás időkorláttal és read-only joggal:**
```csharp
// 1 órás érvényességű olvasási jogosultságú SAS URL generálása biztonságos streaminghez
public async Task<StreamingUrlDto> GetStreamingUrlAsync(int movieId, int userId)
{
    // Jogosultság ellenőrzés
    var hasPurchased = await _context.Orders
        .AnyAsync(o => o.UserId == userId && 
                      o.OrderMovies.Any(om => om.MovieId == movieId));
    
    if (!hasPurchased) 
        throw new UnauthorizedException("You need to purchase this movie first.");
    
    var movie = await _context.Movies.FindAsync(movieId);
    
    // Azure Blob Client
    var blobServiceClient = new BlobServiceClient(_configuration["AzureBlobStorage:ConnectionString"]);
    var containerClient = blobServiceClient.GetBlobContainerClient("movie-videos");
    var blobClient = containerClient.GetBlobClient(movie.VideoFileName);
    
    // SAS Token Builder - 1 óra lejárat, csak olvasási jog
    var sasBuilder = new BlobSasBuilder
    {
        BlobContainerName = "movie-videos",
        BlobName = movie.VideoFileName,
        Resource = "b", // Blob level
        StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Clock skew biztonsága
        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
    };
    sasBuilder.SetPermissions(BlobSasPermissions.Read);
    
    // SAS URI generálás
    var sasUri = blobClient.GenerateSasUri(sasBuilder);
    
    return new StreamingUrlDto 
    { 
        Url = sasUri.ToString(),
        ExpiresAt = sasBuilder.ExpiresOn.Value,
        IsHls = false
    };
}
```

**MovieController - SAS URL endpoint:**
```csharp
// Streaming URL lekérése endpoint, visszaadja a time-limited SAS tokent
[HttpGet("{id}/stream")]
[Authorize]
public async Task<ActionResult<StreamingUrlDto>> GetStreamingUrl(int id)
{
    var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    var result = await _movieService.GetStreamingUrlAsync(id, userId);
    return Ok(result);
}
```

#### **Frontend (WatchMovie.jsx)**

**Streaming URL lekérése és HTML5 video player beállítása:**
```jsx
// Backend-től SAS token lekérése és video player src beállítása
useEffect(() => {
    const fetchStreamingData = async () => {
        try {
            const response = await fetch(`${API_BASE_URL}/api/Movie/${movieId}/stream`, {
                headers: { 
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json'
                }
            });
            
            if (!response.ok) {
                throw new Error('Failed to get streaming URL');
            }
            
            const data = await response.json();
            setStreamingData(data); // { url, expiresAt, isHls }
        } catch (err) {
            setError(err.message);
        }
    };
    
    fetchStreamingData();
}, [movieId, token]);

// Video elem src beállítása Azure Blob SAS URL-re
useEffect(() => {
    if (streamingData?.url && videoRef.current) {
        videoRef.current.src = streamingData.url;
        videoRef.current.load();
    }
}, [streamingData]);

// Native HTML5 video player
<video ref={videoRef} controls className="w-100">
    Your browser does not support the video tag.
</video>

// Token lejárati idő megjelenítése
{streamingData && (
    <div className="badge bg-secondary">
        Expires: {new Date(streamingData.expiresAt).toLocaleTimeString()}
    </div>
)}
```

### **Előnyök**
- ✅ Teljes filmek streamelése
- ✅ Biztonságos hozzáférés (SAS token, időkorlát)
- ✅ Skálázható (Azure CDN)
- ✅ Jogosultság ellenőrzés (order-based)

### **Hátrányok**
- ❌ Egyetlen minőség (1080p fix)
- ❌ Nincs adaptív streaming (lassú neten is 1080p-t tölt)
- ❌ Buffering problémák gyenge kapcsolaton
- ❌ Nagy sávszélesség igény

---

## Fázis 4: HLS Adaptive Streaming (Production-Ready)

### **Cél**
Professzionális streaming megoldás, automatikus minőség váltással a felhasználó internet sebességéhez igazodva.

### **3.1 Architektúra**

#### **Komponensek**
1. **Backend API** (.NET 8)
   - Videó feltöltés kezelés
   - Transzkódolás triggerelés
   - HLS URL generálás (SAS token Azure Blob-ra)

2. **Streaming Service** (Node.js + Express + FFmpeg)
   - FFmpeg transzkódolás koordináció
   - MP4 → HLS konverzió (multi-quality)
   - Shared volume hozzáférés

3. **Azure Blob Storage**
   - Eredeti MP4 fájlok tárolása
   - Transzkódolt HLS szegmensek (.ts fájlok)
   - Master playlist (.m3u8) és quality variant playlist-ek

4. **Frontend** (React + HLS.js)
   - HLS stream lejátszás
   - Adaptív minőség váltás
   - Manuális minőség beállítás lehetőség

---

### **3.2 Backend Implementáció**

#### **MovieService.cs - Transzkódolás Triggerelés**

**Eredeti MP4 letöltése Azure-ról és streaming service hívása FFmpeg transzkódoláshoz:**
```csharp
// 3 lépéses folyamat: Azure Blob letöltés → Streaming service trigger → DB frissítés
public async Task<string> TranscodeToHlsAsync(int movieId)
{
    var movie = await _context.Movies.FindAsync(movieId);
    if (movie?.VideoFileName == null) throw new NotFoundException("Movie not found");

    // 1. Eredeti MP4 letöltése Azure Blob-ból shared volume-ra
    var localPath = $"/app/movie-files/{movie.VideoFileName}";
    var blobClient = _blobContainer.GetBlobClient(movie.VideoFileName);
    
    using (var fileStream = File.OpenWrite(localPath))
    {
        await blobClient.DownloadToAsync(fileStream);
    }

    // 2. Streaming service triggerelése (Node.js + FFmpeg konténer)
    var streamingServiceUrl = "http://movieshop-streaming:3001/transcode";
    var requestBody = new 
    { 
        inputFile = localPath,
        movieId = movieId 
    };
    
    var response = await _httpClient.PostAsJsonAsync(streamingServiceUrl, requestBody);
    
    if (!response.IsSuccessStatusCode) 
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        throw new Exception($"Transcoding failed: {errorContent}");
    }

    var result = await response.Content.ReadFromJsonAsync<TranscodeResultDto>();
    
    // 3. Adatbázis frissítése HLS master playlist útvonallal
    movie.HlsManifestPath = result.MasterPlaylistPath; // pl: hls/42/master.m3u8
    await _context.SaveChangesAsync();
    
    return result.MasterPlaylistPath;
}
```

#### **MovieController - HLS Stream Endpoint**

**Prioritási sorrend: HLS (ha létezik) → MP4 fallback, mindkettő SAS tokennel:**
```csharp
// Intelligens endpoint: HLS-t preferálja, de visszaesik MP4-re ha nincs transzkódolva
[HttpGet("{id}/stream")]
[Authorize]
public async Task<ActionResult<StreamingUrlDto>> GetStreamingUrl(int id)
{
    var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    
    // Jogosultság ellenőrzés: megvásárolta-e a user a filmet
    var hasPurchased = await _movieService.HasUserPurchasedMovie(userId, id);
    if (!hasPurchased) 
        return StatusCode(403, new { message = "Purchase required" });

    var movie = await _context.Movies.FindAsync(id);
    
    // Prioritás 1: HLS adaptive streaming (ha van transzkódolva)
    if (!string.IsNullOrEmpty(movie.HlsManifestPath))
    {
        var hlsSasToken = GenerateSasTokenForBlob(movie.HlsManifestPath, hours: 1);
        
        return Ok(new StreamingUrlDto
        {
            Url = hlsSasToken,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            IsHls = true
        });
    }
    
    // Fallback: Direct MP4 (Fázis 3 megoldás)
    if (!string.IsNullOrEmpty(movie.VideoFileName))
    {
        var mp4SasToken = GenerateSasTokenForBlob(movie.VideoFileName, hours: 1);
        return Ok(new StreamingUrlDto
        {
            Url = mp4SasToken,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            IsHls = false
        });
    }
    
    return NotFound(new { message = "No video available" });
}
```

---

### **3.3 Streaming Service (Node.js + FFmpeg)**

#### **Dockerfile**

**Node.js + FFmpeg Docker image streaming service-hez:**
```dockerfile
FROM node:18-alpine

# FFmpeg és szükséges codec-ek telepítése (libx264, aac)
RUN apk add --no-cache ffmpeg

WORKDIR /app

# Node.js dependency-k telepítése
COPY package*.json ./
RUN npm install

# Express szerver másolása
COPY server.js ./

EXPOSE 3001
CMD ["node", "server.js"]
```

#### **server.js - Transzkódolás API**

**Express API endpoint FFmpeg multi-quality HLS transzkódoláshoz:**
```javascript
// REST API endpoint amely fogadja az input fájlt, FFmpeg-et futtatja, és visszatér a master playlist útvonallal
const express = require('express');
const { exec } = require('child_process');
const fs = require('fs');
const path = require('path');

const app = express();
app.use(express.json());

app.post('/transcode', async (req, res) => {
    const { inputFile, movieId } = req.body;
    
    console.log(`Starting transcoding for movie ${movieId}: ${inputFile}`);
    
    // Output mappa létrehozása shared volume-on
    const outputDir = `/app/movie-files/hls/${movieId}`;
    await fs.promises.mkdir(outputDir, { recursive: true });

    // FFmpeg parancs - 3 minőség párhuzamos generálása (480p, 720p, 1080p)
    const ffmpegCmd = `
        ffmpeg -i "${inputFile}" \
        -filter_complex "[0:v]split=3[v1][v2][v3]" \
        -map "[v1]" -c:v libx264 -b:v 800k -s 854x480 \
            -map 0:a -c:a aac -b:a 128k -hls_time 10 \
            -hls_segment_filename "${outputDir}/480p_%03d.ts" \
            "${outputDir}/480p.m3u8" \
        -map "[v2]" -c:v libx264 -b:v 2000k -s 1280x720 \
            -map 0:a -c:a aac -b:a 192k -hls_time 10 \
            -hls_segment_filename "${outputDir}/720p_%03d.ts" \
            "${outputDir}/720p.m3u8" \
        -map "[v3]" -c:v libx264 -b:v 5000k -s 1920x1080 \
            -map 0:a -c:a aac -b:a 256k -hls_time 10 \
            -hls_segment_filename "${outputDir}/1080p_%03d.ts" \
            "${outputDir}/1080p.m3u8"
    `;

    // FFmpeg futtatása child process-ben
    exec(ffmpegCmd, async (error, stdout, stderr) => {
        if (error) {
            console.error('FFmpeg transcoding error:', stderr);
            return res.status(500).json({ 
                error: 'Transcoding failed', 
                details: stderr 
            });
        }

        console.log('FFmpeg transcoding completed successfully');

        // Master playlist létrehozása (tartalmazza a 3 minőség listáját)
        const masterPlaylist = `#EXTM3U
#EXT-X-VERSION:3
#EXT-X-STREAM-INF:BANDWIDTH=800000,RESOLUTION=854x480
480p.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=2000000,RESOLUTION=1280x720
720p.m3u8
#EXT-X-STREAM-INF:BANDWIDTH=5000000,RESOLUTION=1920x1080
1080p.m3u8
`;
        
        const masterPath = `${outputDir}/master.m3u8`;
        await fs.promises.writeFile(masterPath, masterPlaylist);
        
        console.log('Master playlist created:', masterPath);

        // HLS fájlok feltöltése Azure Blob Storage-ba (opcionális)
        try {
            await uploadHlsToAzure(outputDir, movieId);
            console.log('HLS files uploaded to Azure Blob');
        } catch (uploadError) {
            console.error('Azure upload failed:', uploadError);
        }

        // Válasz a backend-nek
        res.json({ 
            success: true,
            masterPlaylistPath: `hls/${movieId}/master.m3u8`,
            message: 'Transcoding completed successfully'
        });
    });
});

app.listen(3001, () => {
    console.log('Streaming service running on port 3001');
});
```

#### **FFmpeg Paraméterek Magyarázat**
- **`-filter_complex "[0:v]split=3[v1][v2][v3]"`**: Videó stream szétválasztása 3 példányra
- **`-c:v libx264`**: H.264 codec (széles böngésző támogatás)
- **`-b:v 800k/2000k/5000k`**: Bitrate beállítások (480p/720p/1080p)
- **`-s 854x480/1280x720/1920x1080`**: Felbontások
- **`-c:a aac -b:a 128k/192k/256k`**: Audio codec és bitrate
- **`-hls_time 10`**: 10 másodperces szegmensek
- **`-hls_segment_filename`**: Szegmens fájl nevek (.ts)

---

### **3.4 Frontend (React + HLS.js)**

#### **Package Install**
```bash
npm install hls.js
```

#### **WatchMovie.jsx - HLS Player**

**HLS.js inicializálás és konfigurálás adaptive streaming-hez:**
```jsx
// HLS.js library használata adaptive bitrate streaming lejátszásához
import Hls from 'hls.js';

const WatchMovie = () => {
    const videoRef = useRef(null);
    const hlsRef = useRef(null);
    const [hlsQualityLevels, setHlsQualityLevels] = useState([]);
    const [selectedHlsLevel, setSelectedHlsLevel] = useState(-1); // -1 = auto (adaptive)

    // HLS player setup amikor streamingData betöltődik
    useEffect(() => {
        if (!streamingData || !videoRef.current) return;

        const video = videoRef.current;

        // Előző HLS instance cleanup (memória szivárgás elkerülése)
        if (hlsRef.current) {
            hlsRef.current.destroy();
            hlsRef.current = null;
        }

        // Modern böngésző - HLS.js használata (Chrome, Firefox, Edge)
        if (streamingData.isHls && Hls.isSupported()) {
            const hls = new Hls({
                enableWorker: true, // Web Worker használata jobb teljesítményhez
                lowLatencyMode: false, // Live streaming esetén true
                backBufferLength: 90, // 90 másodperc buffer megőrzése seek-eléshez
                xhrSetup: (xhr, url) => {
                    // Authorization header hozzáadása minden Azure Blob request-hez
                    if (url.includes('/api/Movie/')) {
                        xhr.setRequestHeader('Authorization', `Bearer ${token}`);
                    }
                }
            });

            hlsRef.current = hls;
            hls.loadSource(streamingData.url); // Master playlist (master.m3u8)
            hls.attachMedia(video); // Video elem csatolása

            // Master playlist betöltve - elérhető minőségek detektálása
            hls.on(Hls.Events.MANIFEST_PARSED, () => {
                console.log('HLS manifest parsed');
                console.log('Available qualities:', hls.levels.map(l => `${l.height}p @ ${Math.round(l.bitrate/1000)}kbps`));
                setHlsQualityLevels(hls.levels); // State frissítés UI selector-hoz
                video.play().catch(err => console.log('Autoplay prevented by browser:', err));
            });

            // Hibakezelés (network, media error recovery)
            hls.on(Hls.Events.ERROR, (event, data) => {
                console.error('HLS error:', data);
                
                if (data.fatal) {
                    switch (data.type) {
                        case Hls.ErrorTypes.NETWORK_ERROR:
                            // Network hiba - próbálkozás újratöltéssel
                            console.log('Network error, attempting recovery...');
                            hls.startLoad();
                            break;
                        case Hls.ErrorTypes.MEDIA_ERROR:
                            // Media dekódolási hiba - recovery
                            console.log('Media error, attempting recovery...');
                            hls.recoverMediaError();
                            break;
                        default:
                            // Fatal hiba - instance destroy
                            console.error('Fatal error, destroying HLS instance');
                            hls.destroy();
                            setError('Video playback failed');
                            break;
                    }
                }
            });

            // Minőség váltás event logging
            hls.on(Hls.Events.LEVEL_SWITCHED, (event, data) => {
                const level = hls.levels[data.level];
                console.log(`Quality switched to: ${level.height}p (${Math.round(level.bitrate / 1000)}kbps)`);
            });

        } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
            // Safari és iOS native HLS támogatás
            video.src = streamingData.url;
            video.addEventListener('loadedmetadata', () => {
                console.log('HLS loaded via native support');
                video.play().catch(err => console.log('Autoplay prevented'));
            });
        } else {
            console.error('HLS not supported in this browser');
            setError('Your browser does not support HLS streaming');
        }

        // Cleanup function - component unmount vagy dependency változás
        return () => {
            if (hlsRef.current) {
                hlsRef.current.destroy();
                hlsRef.current = null;
            }
            video.pause();
            video.src = '';
        };
    }, [streamingData, token]);

    // Manuális minőség váltás (user által kezdeményezett)
    useEffect(() => {
        if (hlsRef.current && hlsQualityLevels.length > 0) {
            if (selectedHlsLevel === -1) {
                // Auto mode: HLS.js dönt a bandwidth alapján
                hlsRef.current.currentLevel = -1;
                console.log('Quality mode: Auto (adaptive bitrate)');
            } else {
                // Manual mode: fix minőség lock
                hlsRef.current.currentLevel = selectedHlsLevel;
                const level = hlsQualityLevels[selectedHlsLevel];
                console.log(`Quality mode: Manual lock - ${level.height}p`);
            }
        }
    }, [selectedHlsLevel, hlsQualityLevels]);

    return (
        <div>
            {/* Quality Selector UI - csak HLS streaming esetén */}
            {streamingData?.isHls && hlsQualityLevels.length > 0 && (
                <div className="quality-selector mb-3">
                    <div className="btn-group btn-group-sm">
                        <button 
                            className={`btn ${selectedHlsLevel === -1 ? 'btn-success' : 'btn-outline-light'}`}
                            onClick={() => setSelectedHlsLevel(-1)}
                        >
                            <i className="bi bi-wifi me-1"></i>Auto
                        </button>
                        {hlsQualityLevels.map((level, index) => (
                            <button 
                                key={index}
                                className={`btn ${selectedHlsLevel === index ? 'btn-primary' : 'btn-outline-light'}`}
                                onClick={() => setSelectedHlsLevel(index)}
                            >
                                {level.height}p
                            </button>
                        ))}
                    </div>
                    <small className="text-muted ms-2">
                        Auto mode adjusts quality based on your connection
                    </small>
                </div>
            )}

            {/* Video Player */}
            <video 
                ref={videoRef} 
                controls 
                className="w-100"
                style={{ maxHeight: '80vh' }}
            >
                Your browser does not support the video tag.
            </video>
        </div>
    );
};
```

---

### **3.5 Azure Blob Storage Struktúra**

```
movie-videos/
├── 1-original.mp4              # Eredeti feltöltött fájl
├── hls/
│   ├── 1/
│   │   ├── master.m3u8         # Master playlist (minőség lista)
│   │   ├── 480p.m3u8           # 480p variant playlist
│   │   ├── 480p_000.ts         # 480p szegmensek
│   │   ├── 480p_001.ts
│   │   ├── 720p.m3u8
│   │   ├── 720p_000.ts
│   │   ├── 1080p.m3u8
│   │   ├── 1080p_000.ts
│   │   └── ...
│   └── 2/
│       └── ...
```

---

### **3.6 Adaptive Streaming Működése**

#### **1. Kezdeti Minőség Kiválasztás**
- HLS.js elemzi a rendelkezésre álló sávszélességet
- Alacsonyabb minőséggel indul (480p) gyors start miatt
- Automatikusan feljebb vált ha stabil a kapcsolat

#### **2. Dinamikus Minőség Váltás (ABR - Adaptive Bitrate)**
- **Bandwidth estimation**: Minden szegmens letöltésekor méri a sebességet
- **Buffer monitoring**: Ha buffering van, alacsonyabb minőségre vált
- **Smooth switching**: Szegmens határon vált (nem szakítja meg a lejátszást)

#### **3. Manuális Override**
- Felhasználó rákattint egy minőségre (pl. 720p)
- HLS.js `currentLevel` property beállítása
- Lock mode: Nem vált automatikusan, amíg nincs kritikus buffer probléma

---

### **3.7 Előnyök és Hátrányok**

#### **Előnyök**
- ✅ **Adaptív streaming**: Automatikus minőség váltás internet sebesség alapján
- ✅ **Széles kompatibilitás**: Desktop, mobil, tablet, smart TV
- ✅ **Sávszélesség optimalizáció**: Kevesebb buffer, kevesebb adatforgalom
- ✅ **Professzionális élmény**: Netflix-szerű streamelés
- ✅ **Skálázhatóság**: Azure CDN + HLS (millió felhasználó)
- ✅ **Manuális kontroll**: User választhat fix minőséget

#### **Hátrányok**
- ❌ **Komplexitás**: FFmpeg, Node.js streaming service, HLS.js
- ❌ **Tárhely igény**: 3x videó méret (480p + 720p + 1080p)
- ❌ **Transzkódolási idő**: ~2-5x videó hossza (1 óra film → 2-5 óra feldolgozás)
- ❌ **Szerver erőforrás**: CPU-intenzív FFmpeg folyamat

---

## Összehasonlítás

| Szempont | Fázis 1 (YouTube) | Fázis 2 (Lokális) | Fázis 3 (Azure MP4) | Fázis 4 (HLS) |
|----------|-------------------|-------------------|---------------------|---------------|
| **Implementációs idő** | 2 óra | 4 óra | 1 nap | 3-4 nap |
| **Video minőség** | Trailer (2-3 perc) | Teljes film (fix 1080p) | Teljes film (fix 1080p) | Teljes film (adaptív) |
| **Sávszélesség optimalizáció** | Automatikus (YouTube) | Nincs | Nincs | Kiváló (ABR) |
| **Tárhely igény** | 0 MB | ~3-5 GB/film | ~3-5 GB/film | ~10-15 GB/film |
| **Tárhely lokáció** | YouTube CDN | Szerver HDD/SSD | Azure Blob + CDN | Azure Blob + CDN |
| **Skálázhatóság** | ✅ Kiváló | ❌ Korlátozott | ✅ Kiváló | ✅ Kiváló |
| **Szerver terhelés** | Nincs | Magas (streaming) | Alacsony (csak auth) | Magas (transzkódolás) |
| **User élmény** | YouTube player | Alapvető HTML5 | Alapvető HTML5 | Professzionális |
| **Költség** | Ingyenes | Olcsó (hosting) | Közepesen drága | Drága (tárhely + CPU) |
| **Production-ready** | ❌ | ❌ | ⚠️ (kis skálára) | ✅ |

---

## Továbbfejlesztési Lehetőségek

### **Rövidtávú (1-2 hét)**
- 🔄 **Thumbnail generálás**: FFmpeg screenshot minden 10. másodpercben
- 🔄 **Progress tracking**: Mentse el hol tartott user a filmben
- 🔄 **Subtitle support**: WebVTT beágyazás HLS-be

### **Középtávú (1-2 hónap)**
- 🔄 **Queue-based transcoding**: RabbitMQ/Redis + worker processek
- 🔄 **GPU acceleration**: NVIDIA NVENC FFmpeg support
- 🔄 **Parallel transcoding**: 480p, 720p, 1080p egyidejű generálás

### **Hosszútávú (3+ hónap)**
- 🔄 **4K support**: 2160p variant hozzáadása
- 🔄 **Live streaming**: RTMP input → HLS output
- 🔄 **DRM protection**: Widevine/FairPlay titkosítás
- 🔄 **Analytics**: Nézettségi statisztikák, ABR decision logging

---

## Összefoglalás

A MovieShop streaming megoldása **4 iterációs cikluson** ment keresztül:

1. **MVP (Fázis 1)**: YouTube trailers - gyors validáció, külső hosting
2. **Alapvető backend (Fázis 2)**: Lokális fájlrendszer - teljes filmek, single server
3. **Cloud tárhely (Fázis 3)**: Azure Blob Storage + direct MP4 - skálázható tárolás
4. **Production-ready (Fázis 4)**: HLS adaptive streaming - professzionális élmény

**Evolúciós lépcsők**:
- Fázis 1→2: Saját tartalom tárolása és kiszolgálása
- Fázis 2→3: Cloud migráció és CDN integráció
- Fázis 3→4: Adaptive streaming és multi-quality support

**Jelenlegi állapot**: Fázis 4 teljesen implementálva, FFmpeg multi-quality transzkódolással, HLS.js adaptive playback-kel, Azure Blob CDN-nel.

**Következő lépés**: Queue-based transcoding a skálázhatóság javításához, thumbnail generálás a jobb UX-ért, DRM védelem prémium tartalomhoz.
