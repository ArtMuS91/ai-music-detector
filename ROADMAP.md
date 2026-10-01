# Roadmap

Informal backlog for this solo pet project. Not a commitment or schedule — just a place to keep track of what's next and what's deliberately deferred.

The core flow from the original brief: YouTube URL → audio acquisition → preprocessing → AI detection models → signal aggregation → AI analysis agent → result + confidence + explanation.

Phases are ordered by dependency, so each one should leave the project in a runnable, testable state before the next builds on it.

## Phase 0 — Foundations

- Domain models in `Core`: `Track`, `AnalysisRequest`, `AnalysisResult` (verdict: AI / Human / Mixed, confidence, supporting `Signal` list), `AnalysisStatus`
- Core interfaces as the seams for later phases: `IAudioAcquisitionService`, `IAudioPreprocessor`, `IDetectionSignalProvider`, `ISignalAggregator`, `IAnalysisJobRepository`
- `src/Tests/` scaffolding — xUnit project referencing `Core` and `Analysis`, runnable via `dotnet test`
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

- ✅ `ml/` Python project (FastAPI) exposing `POST /detect/{detector}` over the uploaded preprocessed WAV — one endpoint per detector, so each stays an independent signal; `GET /health` lists detectors
- Multiple independent detection signals rather than a single classifier, added incrementally:
  - ✅ Generative-audio artifacts — `fakeprint`: pretrained logistic regression (`lofcz/ai-music-detector`, MIT, trained on Suno <= 5 / Udio <= 1.5) over the "fakeprint" of Afchar et al. (ISMIR 2025). Features are ported to numpy (no PyTorch) and checked against torchaudio. On 14 YouTube tracks (7 known AI, 7 human) it separated every one (AI 1.00, human <= 0.005), weight 0.8. It replaced a hand-tuned spectral-peak detector that could barely tell the two apart on the same tracks
  - ✅ Metadata heuristics: `MetadataHeuristicsSignalProvider` (in `Analysis`) — AI tools/disclosures named in title/channel/tags/description (AI; no signal on pre-2023 uploads, which may be about AI or early experiments like OpenAI Jukebox), otherwise a pre-2023 upload date (strongly human, as AI song tools weren't widespread yet), "no AI"-style claims (weakly human). `Track` now carries the video description and tags
  - ✅ Web research (done early, alongside Phase 2): `GroqWebResearchSignalProvider` asks a Groq GPT-OSS model with `browser_search` whether the track/artist is publicly known to be AI-generated, returning a score, a confidence-based weight, and evidence links verified against the actual search results. Stored in the result's signals; the verdict stays `Inconclusive` until Phase 4
- ✅ `MlDetectionSignalProvider` (in `Infrastructure`, alongside the other external integrations) calls the `ml/` service over HTTP, one provider per id in `MlService:Detectors`; the service is in `docker-compose.yml` and CI

## Phase 4 — Aggregation and explanation

- `ISignalAggregator` in `Analysis`: combine signal scores into verdict + confidence (start with weighted rules, not a learned meta-model). Signals can disagree confidently: on the Phase 3 check, web research called an AI act (Aventhis) human at weight 0.8 while `fakeprint` scored it 1.00, so a strong audio signal should be able to outvote it
- AI analysis agent (Groq API) turning aggregated signals into a human-readable explanation; MCP tools and RAG over detection knowledge if justification quality needs it
- Lyrics transcription via Whisper, as input for the agent's explanation rather than a detection signal of its own

## Phase 5 — Result UI

Built ahead of Phase 4, so until aggregation lands every job shows as `Inconclusive` at 0% confidence, but the UI already renders all four verdicts.

- ✅ Verdict badge, confidence and AI-likelihood meters, explanation, and a per-signal breakdown (heaviest first, lean toward human/AI, weight, detail, evidence links with their stance)
- ✅ Waveform / spectrogram visualization: the audio is deleted when a job finishes, so the `ml/` service's `POST /visualize` turns the preprocessed WAV into a display-sized peak waveform and a log-frequency spectrogram (dB quantized to bytes, ~50 KB) during the job. The result is stored on the job (`visualization` jsonb) and saved with the move to `Analyzing`, so it shows up while the detectors still run. A failed visualization only costs the picture
- ✅ Progress stepper (queue → download → preprocess → analyze → result). A failed job records the stage it failed in (`failed_stage`) and shows it in the stepper and in the error message. A rejected URL is flagged on the input field, a failed poll shows a warning and polling continues, and a job that no longer exists stops polling

## Post-MVP features

- Spotify integration
- Model attribution (identifying which generative model/tool likely produced a track)
