# ai-music-detector

Music track analyser to detect if it fully or partially AI generated

## Getting started

### API (.NET 10)

```
dotnet run --project src/Api
```

Serves `GET /health` and OpenAPI docs (dev only) on the URL printed at startup.

### Web (React + TypeScript + MUI)

```
cd web
npm install
npm run dev
```

Serves the app at http://localhost:5173. The app calls the API's `/health` endpoint on load to confirm connectivity (set `VITE_API_BASE_URL` to override the default `http://localhost:5214`).
