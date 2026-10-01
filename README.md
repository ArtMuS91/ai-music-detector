# ai-music-detector

Music track analyser to detect if it fully or partially AI generated

## Demo

Paste a YouTube or YouTube Music link and get a verdict (AI-generated, human-made or mixed) with an AI likelihood, a confidence score, the signals behind it and a written explanation.

**Human-made:** Rammstein – *Zick Zack*

![Analyzing a human-made track](docs/media/demo-human.gif)

**AI-generated:** The Velvet Sundown – *Dust on the Wind*

![Analyzing an AI-generated track](docs/media/demo-ai.gif)

## Getting started

### API (.NET 10)

```
dotnet run --project src/Api
```

Serves `GET /health` and OpenAPI docs (dev only) on the URL printed at startup.

### ML service (Python + FastAPI)

```
cd ml
python -m venv .venv
.venv/Scripts/pip install -r requirements-dev.txt
.venv/Scripts/uvicorn app.main:app --reload --port 8000
```

Hosts the audio detectors the API calls during analysis (`GET /health` lists them). Or skip all of the above with `docker compose up -d`, which runs Postgres, the ML service and the API together.

### Web (React + TypeScript + MUI)

```
cd web
npm install
npm run dev
```

Serves the app at http://localhost:5173. The app calls the API's `/health` endpoint on load to confirm connectivity (set `VITE_API_BASE_URL` to override the default `http://localhost:5214`).
