# Többfázisú build: az SDK image csak fordításra kell, a futtatáshoz elég a runtime.
# A build kontextusa a services/ mappa, mert a projekt hivatkozik a megosztott
# MovieShop.Contracts és MovieShop.ServiceDefaults könyvtárakra.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Előbb csak a projektfájlok másolása — így a NuGet-visszaállítás rétege
# cache-elhető marad, amíg a függőségek nem változnak.
COPY MovieShop.Contracts/MovieShop.Contracts.csproj MovieShop.Contracts/
COPY MovieShop.ServiceDefaults/MovieShop.ServiceDefaults.csproj MovieShop.ServiceDefaults/
COPY MovieShop.CatalogService/MovieShop.CatalogService.csproj MovieShop.CatalogService/
RUN dotnet restore MovieShop.CatalogService/MovieShop.CatalogService.csproj

# Most jöhet a teljes forrás
COPY MovieShop.Contracts/ MovieShop.Contracts/
COPY MovieShop.ServiceDefaults/ MovieShop.ServiceDefaults/
COPY MovieShop.CatalogService/ MovieShop.CatalogService/

RUN dotnet publish MovieShop.CatalogService/MovieShop.CatalogService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# A Catalog Service az egyetlen, amelyik FFmpeg-et futtat: a feltöltött videót
# HLS szegmensekre kódolja át (TranscodingService). A többi service runtime
# image-e ezért kisebb marad.
RUN apt-get update     && apt-get install -y --no-install-recommends ffmpeg     && rm -rf /var/lib/apt/lists/*

WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MovieShop.CatalogService.dll"]
