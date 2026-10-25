FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/SesliKitap.Data/SesliKitap.Data.csproj src/SesliKitap.Data/
COPY src/SesliKitap.Business/SesliKitap.Business.csproj src/SesliKitap.Business/
COPY src/SesliKitap.API/SesliKitap.API.csproj src/SesliKitap.API/

RUN dotnet restore src/SesliKitap.API/SesliKitap.API.csproj

COPY src/ src/
RUN dotnet publish src/SesliKitap.API/SesliKitap.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/logs && chown -R app:app /app

COPY --from=build --chown=app:app /app/publish .

USER app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=5 \
    CMD curl -fsS http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "SesliKitap.API.dll"]
