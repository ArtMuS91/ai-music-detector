# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

AI Music Detector — a web app where a user pastes a YouTube/YouTube Music link and the system estimates whether the track is AI-generated, human-created, or mixed, with confidence and supporting signals. Currently mid-MVP: audio acquisition, preprocessing, detection signals (web research, metadata heuristics, an audio detector in the Python ML service), rule-based aggregation into a verdict, a Groq-written explanation (with Whisper lyrics as context), and the result UI work end to end.

## Repository layout

Monorepo with two independently-run apps sharing one git history:

- `src/` — .NET 10 solution (`AiMusicDetector.slnx`)
  - `Api` — ASP.NET Core minimal API (entry point), references `Core`, `Infrastructure`, `Analysis`
  - `Core` — EF-mapped entities (`Core.Entities`), plain domain models (`Core.Models`), and the interfaces other projects implement, split into `Core.Repositories` (persistence seams) and `Core.Services` (business-logic and external-integration seams)
  - `Infrastructure` — implements `Core.Repositories` (EF Core/Postgres) and the external-integration half of `Core.Services` (yt-dlp acquisition, ffmpeg preprocessing, Groq web research / explanation / lyrics transcription), under matching `Repositories`/`Services` folders; references `Core`
  - `Analysis` — implements the business-logic half of `Core.Services` (`IAnalysisService`, `IAnalysisPipeline`, `ISignalAggregator`, the metadata signal) under its own `Services` folder; references `Core`
- `src/Tests/Core.Tests` — xUnit, references `Core`, `Analysis` and `Infrastructure` (external integrations are tested against stubbed `HttpMessageHandler`s / fakes, never real services)
- `web/` — Vite + React + TypeScript + MUI frontend
- `ml/` — Python FastAPI service hosting the audio detectors (`app/detectors/`) and the waveform/spectrogram renderer (`app/visualize.py`), tests in `ml/tests` (pytest)

**yt-dlp** is the open-source CLI tool `Infrastructure` shells out to for downloading the audio-only stream from a YouTube/YouTube Music URL — it is not a library dependency, just an executable. The API's Docker image (`src/Api/Dockerfile`) bundles the standalone `yt-dlp_linux` binary so nothing needs installing on the host when running via `docker compose`; running the API with `dotnet run` instead requires `yt-dlp` on the host `PATH` (see Prerequisites below).

**ffmpeg** is the second CLI tool `Infrastructure` shells out to: preprocessing uses it to decode whatever yt-dlp downloaded (webm/opus, m4a, ...) into mono 16-bit PCM WAV at a fixed sample rate, trimmed to a window from the middle of the track (`Ffmpeg` section in `appsettings.json`). The Docker image installs it via apt; `dotnet run` needs it on the host `PATH`.

**Groq** powers three integrations, all sharing `GroqRequests` (API key check plus retry on 429 / malformed tool calls):
- `GroqWebResearchSignalProvider`, a detection signal: it sends the track's title/artist to a GPT-OSS model with Groq's built-in `browser_search` tool and asks whether the track or artist is publicly known to be AI-generated. Evidence links are kept only if they appear in the search results Groq reports in `executed_tools` — the model's own cited URLs are never trusted on their own. The API key is a secret: put `GROQ_API_KEY=...` in the gitignored `.env` at the repo root (docker compose passes it as `Groq__ApiKey`); for `dotnet run`, set the `Groq__ApiKey` environment variable. Without a key the provider fails and the pipeline simply leaves that signal out.
- `GroqLyricsTranscriber` runs Whisper over the preprocessed audio. Lyrics are context for the explanation, not a signal, and segments Whisper rates as unreliable are dropped.
- `GroqResultExplainer` writes the result's explanation from the already-aggregated result. If either of these two fails, the result keeps the aggregator's rule-based summary.

`ROADMAP.md` lists planned post-MVP features — check it before starting new work.

## Commands

### Prerequisites
- Docker is the only prerequisite for running the API: `docker compose up -d` from the repo root builds and starts Postgres, the ML service and the API (with yt-dlp and ffmpeg bundled in its image), migrating the database on startup. The API is reachable at `http://localhost:5214`, same as the `dotnet run` dev workflow.
- Running the API with `dotnet run` instead of Docker needs two things Docker otherwise provides: Postgres reachable at the `Postgres` connection string in `appsettings.json` (`docker compose up -d postgres` is enough; compose publishes it on host port **5433**, not 5432, so it does not clash with a locally installed PostgreSQL), and `yt-dlp` plus `ffmpeg` on the host `PATH` (override the locations with `YtDlp:ExecutablePath` / `Ffmpeg:ExecutablePath` if they live elsewhere).

### API (`src/Api`, run from repo root)
- Build: `dotnet build`
- Run (dev, hot-reload, needs local Postgres + yt-dlp + ffmpeg — see Prerequisites): `dotnet run --project src/Api` — serves on `http://localhost:5214` (see `src/Api/Properties/launchSettings.json`); in Development only, the OpenAPI document is served at `/openapi/v1.json` and the Scalar UI over it at `/scalar/v1`.
- Run (containerized, no local prerequisites): `docker compose up -d --build` from the repo root.
- Test: `dotnet test`
- Add a migration: `dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api --output-dir Migrations` (`dotnet ef` comes from the local tool manifest — run `dotnet tool restore` once).

### ML service (`ml/`, run from `ml/`)
- Setup (once): `python -m venv .venv`, then `.venv/Scripts/pip install -r requirements-dev.txt` (`.venv/bin/pip` on Linux/macOS)
- Run (dev): `.venv/Scripts/uvicorn app.main:app --reload --port 8000` — the API's default `MlService:BaseUrl` is `http://localhost:8000/`; `docker compose up -d ml` works too
- Test: `.venv/Scripts/pytest`
- Lint/format: `.venv/Scripts/ruff check .` and `.venv/Scripts/ruff format .`

### Web (`web/`)
- Install: `npm install`
- Dev server: `npm run dev` — serves on `http://localhost:5173`
- Build (typecheck + bundle): `npm run build`
- Lint: `npm run lint` (oxlint)
- Test: `npm test` (Vitest + React Testing Library on jsdom, single run); `npm run test:watch` for watch mode
- Preview production build: `npm run preview`

### Pre-commit AI review
- `.githooks/pre-commit` runs the `code-reviewer` agent (`.claude/agents/code-reviewer.md`) headlessly over the staged diff. Enable once per clone: `git config core.hooksPath .githooks`.
- The agent's last output line is `VERDICT: PASS|WARN|FAIL`; only `FAIL` (a Critical finding) blocks the commit. `AI_REVIEW_STRICT=1` also blocks on `WARN`; `SKIP_AI_REVIEW=1` or `git commit --no-verify` skips the review; `AI_REVIEW_TIMEOUT` (seconds, default 900) bounds it.
- Uses `claude` from `PATH`, else `CLAUDE_BIN`, else the binary bundled with the VS Code extension. If none is found, or the run errors or times out, the commit is allowed — the review never blocks on its own failure.
- To review without committing, ask Claude Code to use the `code-reviewer` agent (defaults to all uncommitted changes).

### Architecture reports
- Ask Claude Code to "run the architecture-reporter agent" (`.claude/agents/architecture-reporter.md`). It's manual only, not part of any hook.
- Each run writes a new self-contained `docs/architecture/architecture-YYYY-MM-DD_HHmmss.html` (open it in a browser; Mermaid loads from a CDN): a 3-tier view (presentation / application / data & integration) with styled Mermaid diagrams (tier overview, job state machine, deployment), detection signals, and roadmap position. Earlier reports are never edited or compared against. Commit them so git keeps the history of how the architecture changed.

## Architecture notes

- The API is a minimal-API project (no MVC controllers) — endpoints are registered directly in `src/Api/Program.cs` via `app.MapGet`/etc.
- CORS in `Program.cs` is restricted to the Vite dev origin (`http://localhost:5173`) — update this policy if the frontend's dev port or deployed origin changes.
- The frontend reads the API base URL from `VITE_API_BASE_URL` (Vite env var), defaulting to `http://localhost:5214` (see `web/src/api.ts`). Both apps must be running simultaneously for the frontend to reach the API. The `??` there matters: setting `VITE_API_BASE_URL=` to an empty string (e.g. in the gitignored `web/.env.development.local`) makes API calls same-origin, and the Vite dev server proxies `/api` to `VITE_API_PROXY_TARGET` (default `http://localhost:5214`, see `web/vite.config.ts`). That is the dev-tunnel workflow: forward only port 5173 and the API is reached through the proxy, with no CORS change needed.
- Project references are wired so the dependency direction stays fixed (`Api` → all three, `Infrastructure`/`Analysis` → `Core`).
- **Layering rule: endpoints never depend on a repository directly.** `Api` endpoints call a `Core.Services` interface (e.g. `IAnalysisService`) that owns the business logic (validation, orchestration) and is the only thing that talks to `Core.Repositories`. This keeps request-time rules in one place instead of scattered across endpoint handlers. `AnalysisService` (in `Analysis/Services`) is the current example — it validates the URL and delegates to `IAnalysisJobRepository`. Background services (e.g. `AnalysisWorker`) are the pipeline's own business logic, not a thin API layer, so they may use repositories directly — though the per-job stages live in `AnalysisPipeline` (in `Analysis`, unit-tested with fakes) and the worker only claims jobs and hands them over.
- **Folder convention**: abstractions and their implementations are grouped by kind, not dumped in a generic `Abstractions`/`Contracts` folder — `Repositories/` for persistence seams, `Services/` for everything else (business logic and external integrations). This applies in `Core` (interfaces) and in `Infrastructure`/`Analysis` (implementations). Api request/response DTOs live under `Api/Models/<EndpointGroup>/`, one type per file, grouped by the endpoint group that owns them (mirrors the endpoint file in `Api/Endpoints`).
- **Entity naming**: a `Core` model that EF Core maps to a table lives in `Core/Entities/`, and its class name carries an `Entity` suffix (e.g. `AnalysisJobEntity`, mapped via `AnalysisDbContext`). Types under `Core/Models/` (`Track`, `AnalysisResult`, `Signal`, ...) are plain value objects — some are embedded inside an entity's `jsonb` column, but none has its own table, so they stay unsuffixed in `Models/`.
- Detection is deliberately many independent `IDetectionSignalProvider`s rather than one classifier, so a weak or failing detector lowers confidence instead of breaking the analysis.
- Work is queued, not done in the request: `POST /api/analyze` persists a job and returns 202 (unless the same video, matched by canonical URL, already has a `Completed` job: then that job comes back as 200 and nothing is queued; failed jobs are never reused, so a failed link can be retried), `AnalysisWorker` (an `IHostedService` inside the API process — no separate worker project) claims it, `AnalysisPipeline` runs it through every stage to `Completed`/`Failed` and deletes its audio files, and the client polls `GET /api/analyze/{id}`.
- **One job at a time, one worker.** The MVP deliberately processes jobs sequentially in a single worker. That is what makes startup recovery simple: when the worker starts, nothing can really be in flight, so `IAnalysisPipeline.RecoverInterruptedAsync` requeues any job still in `Acquiring`/`Preprocessing`/`Analyzing` (stranded by a shutdown) and deletes its leftover audio. Claiming still uses `FOR UPDATE SKIP LOCKED`, but running a second worker (another replica or parallel loops) would also need a lease/heartbeat, or recovery would requeue a job another worker still owns.
- Signal providers are all `IDetectionSignalProvider`s registered in DI; the pipeline runs each one during `Analyzing` and leaves out any that throws. Where they live follows the folder rule: pure logic over data we already have (`MetadataHeuristicsSignalProvider`, reading title/tags/description/upload date) is in `Analysis`; anything calling out (Groq, the ML service) is in `Infrastructure`. After the signals, `ISignalAggregator` (`WeightedSignalAggregator`, rules, no model) sets verdict, AI probability and confidence. Then lyrics transcription and the AI explanation run as optional steps (`TryOptionalAsync`, like the visualizer): **the LLM explains the verdict but never decides it**, so prompt injection via track metadata or lyrics can at worst skew the wording.
- **A signal that can only find evidence one way must say so with weight 0.** If finding nothing proves nothing (no AI keywords in the metadata, say), return weight 0 rather than a lean toward "human". Scoring absence as counter-evidence pushes real AI tracks toward "human" — a hand-tuned spectral-peak detector did exactly that on known AI tracks, because YouTube's re-encoding erases the peaks.
- **ML service contract**: each audio detector is its own endpoint, `POST /detect/{id}` (multipart field `audio`, the preprocessed WAV), returning `{name, score, weight, detail}` — the `Signal` shape. `GET /health` lists detector ids and is what compose's healthcheck waits on. On the .NET side, `MlDetectionSignalProvider` (Infrastructure) is registered once per id in `MlService:Detectors`, so a failing detector drops only its own signal. Adding a detector = implement the `Detector` protocol in `ml/app/detectors/`, add it to `DETECTORS` in `ml/app/main.py`, and add its id to `MlService:Detectors` in `appsettings.json`. The service also has `POST /visualize` (same upload), which is not a detector: it returns a display-sized peak waveform and a log-frequency spectrogram (bytes, base64) that `MlAudioVisualizer` turns into the job's `AudioVisualization`. The pipeline runs it after preprocessing and saves it with the move to `Analyzing`, so the UI can show it while detectors run. If it fails, the job continues without the picture.
- **ML models without PyTorch**: pretrained weights are vendored under `ml/app/models/<name>/` (small files only, with the upstream `LICENSE` and a README pinning the source revision), and the features they were trained on are reproduced in numpy (`ml/app/dsp.py` ports torchaudio's `Resample` and `Spectrogram` defaults). A model is only as good as that match, so each such port has a parity test against a fixture generated once with the real library (`ml/tests/fixtures/`) — regenerate it with torchaudio in a throwaway venv, never by running the port itself.
- `AnalysisJob.Track`, `.Result` and `.Visualization` are stored as `jsonb` via value converters — they are read and written whole, never queried by inner fields, which keeps migrations out of the way as those shapes evolve.
- Acquisition deliberately does not transcode, so it needs no ffmpeg; format normalization belongs to preprocessing.
- **Database naming**: Postgres identifiers (tables, columns) are snake_case, applied automatically by `EFCore.NamingConventions`'s `.UseSnakeCaseNamingConvention()` in `InfrastructureServiceCollectionExtensions`. Don't hand-roll column names to work around this — raw SQL (e.g. the `FOR UPDATE SKIP LOCKED` query in `EfAnalysisJobRepository`) must reference the snake_case names directly since the naming convention doesn't rewrite raw SQL.

## Web conventions

- UI pieces live in `web/src/components/` (one component per file, default export); `App.tsx` keeps only the form, polling and page-level errors. Pure helpers that components use (formatting, spectrogram decoding/colormap) are plain modules in `web/src/` (`format.ts`, `visualization.ts`) so they can be unit-tested without rendering.
- TypeScript models/types live in `web/src/models/` (one file per type, e.g. `Track.ts`, `AnalysisJob.ts`), separate from the components and `api.ts` that use them.
- Tests sit next to the file they cover (`App.test.tsx`, `api.test.ts`); shared test setup and helpers live in `web/src/test/`. Tests never hit a real API — stub `fetch` with `mockApi` from `src/test/mockApi.ts`, which routes by `"METHOD /path"` and throws on any request it wasn't told about. Query elements the way a user finds them (role + accessible name) rather than by class or test id. Data builders for richer responses (`job`, `result`, `signal`, `visualization`) live in `mockApi.ts` too. jsdom has no canvas, so `setup.ts` stubs `getContext` to return null; canvas-drawing code must handle that. Polling is tested with Vitest fake timers (`vi.useFakeTimers({ shouldAdvanceTime: true })` plus `userEvent.setup({ advanceTimers: vi.advanceTimersByTime })`).
- The web job in CI (`.github/workflows/ci.yml`) runs lint, then tests, then build; a failing test fails the job.
- Style: always terminate statements with semicolons. Control-flow bodies (`if`/`else`/`for`/etc.) always use braces, even for one-liners — never `if (x) return y;`. A `return` (or any statement) inside a block goes on its own line.
