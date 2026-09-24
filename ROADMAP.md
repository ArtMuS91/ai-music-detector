# Roadmap

Informal backlog for this solo pet project. Not a commitment or schedule — just a place to keep track of what's next and what's deliberately deferred.

The core flow from the original brief: YouTube URL → audio acquisition → preprocessing → AI detection models → signal aggregation → AI analysis agent → result + confidence + explanation.

Phases are ordered by dependency, so each one should leave the project in a runnable, testable state before the next builds on it.

## Phase 0 — Foundations

- Domain models in `Core`: `Track`, `AnalysisRequest`, `AnalysisResult` (verdict: AI / Human / Mixed, confidence, supporting `Signal` list), `AnalysisStatus`
- Core interfaces as the seams for later phases: `IAudioAcquisitionService`, `IAudioPreprocessor`, `IDetectionSignalProvider`, `ISignalAggregator`, `IAnalysisJobRepository`
- `tests/` scaffolding — xUnit project referencing `Core` and `Analysis`, runnable via `dotnet test`
- GitHub Actions CI: build + test the .NET solution, `npm run lint` / `npm run build` for the web app

## Phase 1 — Audio acquisition (first vertical slice)

- `yt-dlp`-backed `IAudioAcquisitionService` in `Infrastructure`: YouTube/YouTube Music URL → audio-only file
- API endpoints: `POST /api/analyze` (validate URL, create job, return id) and `GET /api/analyze/{id}` (status/result polling)
- PostgreSQL + EF Core wiring in `Infrastructure` for the job store
- `docker-compose.yml` — Postgres, plus the API containerized with yt-dlp bundled (`src/Api/Dockerfile`) so `docker compose up` needs nothing installed on the host. Web still runs via `npm run dev`; containerizing it waits until there is something to deploy
- Web: URL input form → submit → poll status. No visualization yet, just prove the round trip

## Phase 2 — Preprocessing and background processing

- Preprocessing via ffmpeg (`FfmpegAudioPreprocessor` in `Infrastructure`, since it shells out to an external tool like yt-dlp): decode → resample (44.1 kHz) → downmix to mono → trim to a 3-minute window from the middle of the track, written as 16-bit PCM WAV. Splitting into model-sized segments is left to Phase 3, where each detector knows its own input length
- `AnalysisPipeline` in `Analysis` runs a claimed job through every stage to `Completed`/`Failed`; `AcquisitionWorker` became `AnalysisWorker`, a thin `IHostedService` in the API that claims one job at a time (no separate worker project — the MVP processes one track at a time). With no detectors yet, jobs complete as `Inconclusive`
- Interrupted jobs are requeued at worker startup — safe only because there is a single sequential worker
- Acquired and preprocessed audio is deleted when a job reaches `Completed` or `Failed`, or when an interrupted job is requeued

## Phase 3 — ML detection service

- `ml/` Python project (FastAPI) exposing a `/detect` endpoint over preprocessed audio
- Multiple independent detection signals rather than a single classifier, added incrementally:
  - Spectral anomalies (Librosa / torchaudio features)
  - Vocal synthesis indicators (Hugging Face audio deepfake models)
  - Generative-audio artifacts
  - Metadata heuristics (title/channel patterns, upload metadata)
  - ✅ Web research (done early, alongside Phase 2): `GroqWebResearchSignalProvider` asks a Groq GPT-OSS model with `browser_search` whether the track/artist is publicly known to be AI-generated, returning a score, a confidence-based weight, and evidence links verified against the actual search results. Stored in the result's signals; the verdict stays `Inconclusive` until Phase 4
- Vocal/transcription analysis via Whisper
- `Analysis` implements `IDetectionSignalProvider` by calling the `ml/` service over HTTP; add it to `docker-compose.yml`

## Phase 4 — Aggregation and explanation

- `ISignalAggregator` in `Analysis`: combine signal scores into verdict + confidence (start with weighted rules, not a learned meta-model)
- AI analysis agent (Groq API) turning aggregated signals into a human-readable explanation; MCP tools and RAG over detection knowledge if justification quality needs it

## Phase 5 — Result UI

- Verdict badge, confidence bar, per-signal breakdown
- Waveform / spectrogram visualization
- Progress states (acquiring → preprocessing → analyzing → done) and error states for bad or unavailable URLs

## Phase 6 — Observability and hardening

- OpenTelemetry across the API and the `ml/` service
- Test coverage grown per phase — unit tests for aggregation and preprocessing edge cases, integration tests behind fakes for acquisition and ML calls

## Post-MVP features

- Direct file uploads (as an alternative to a YouTube URL)
- Spotify integration
- Model attribution (identifying which generative model/tool likely produced a track)
- More advanced multi-agent functionality
