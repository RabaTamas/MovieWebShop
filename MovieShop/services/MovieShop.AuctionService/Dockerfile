# Többfázisú build: az SDK image csak fordításra kell, a futtatáshoz elég a runtime.
# A build kontextusa a services/ mappa, mert a projekt hivatkozik a megosztott
# MovieShop.Contracts és MovieShop.ServiceDefaults könyvtárakra.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Előbb csak a projektfájlok másolása — így a NuGet-visszaállítás rétege
# cache-elhető marad, amíg a függőségek nem változnak.
COPY MovieShop.Contracts/MovieShop.Contracts.csproj MovieShop.Contracts/
COPY MovieShop.ServiceDefaults/MovieShop.ServiceDefaults.csproj MovieShop.ServiceDefaults/
COPY MovieShop.AuctionService/MovieShop.AuctionService.csproj MovieShop.AuctionService/
RUN dotnet restore MovieShop.AuctionService/MovieShop.AuctionService.csproj

# Most jöhet a teljes forrás
COPY MovieShop.Contracts/ MovieShop.Contracts/
COPY MovieShop.ServiceDefaults/ MovieShop.ServiceDefaults/
COPY MovieShop.AuctionService/ MovieShop.AuctionService/

RUN dotnet publish MovieShop.AuctionService/MovieShop.AuctionService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MovieShop.AuctionService.dll"]
