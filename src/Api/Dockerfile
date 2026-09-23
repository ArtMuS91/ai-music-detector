# Build context is the repo root (see docker-compose.yml) because the Api project
# references sibling projects under src/Core, src/Infrastructure, src/Analysis.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AiMusicDetector.slnx .
COPY src/Api/Api.csproj src/Api/
COPY src/Core/Core.csproj src/Core/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Analysis/Analysis.csproj src/Analysis/
RUN dotnet restore src/Api/Api.csproj

COPY src/Api/ src/Api/
COPY src/Core/ src/Core/
COPY src/Infrastructure/ src/Infrastructure/
COPY src/Analysis/ src/Analysis/
RUN dotnet publish src/Api/Api.csproj --no-restore --configuration Release --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Standalone yt-dlp build has no Python dependency, keeping the runtime image small.
ADD https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux /usr/local/bin/yt-dlp
RUN chmod a+rx /usr/local/bin/yt-dlp

COPY --from=build /app .

ENV YtDlp__ExecutablePath=/usr/local/bin/yt-dlp
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Api.dll"]
