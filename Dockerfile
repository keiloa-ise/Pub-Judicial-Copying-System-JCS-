# ── JCS API (ASP.NET Core 9) ─────────────────────────────────────────────
# Build context = repository root.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore against the API project (pulls in Domain/Application/Infrastructure refs).
# A BuildKit cache mount persists the NuGet package cache across builds/retries, so a flaky
# network only has to fetch each package once. Extra HTTP retries harden the restore further.
ENV NUGET_ENABLE_ENHANCED_HTTP_RETRY=true \
    NUGET_HTTP_MAX_RETRIES=10 \
    NUGET_HTTP_RETRY_DELAY_MILLISECONDS=1000
COPY global.json nuget.config ./
# Local package source: a pre-downloaded copy of the large QuestPDF nupkg (≈39 MB) so the restore
# doesn't have to re-fetch it over a flaky link. Empty on a fresh clone → falls back to nuget.org.
COPY local-nuget/ ./local-nuget/
COPY src/ ./src/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore src/ResourceIQ.Jcs.Api/ResourceIQ.Jcs.Api.csproj

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/ResourceIQ.Jcs.Api/ResourceIQ.Jcs.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ── Runtime ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
# QuestPDF's bundled native renderer needs fontconfig present on Linux for PDF text rendering.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ResourceIQ.Jcs.Api.dll"]
