# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

AI Music Detector — a web app where a user pastes a YouTube/YouTube Music link and the system estimates whether the track is AI-generated, human-created, or mixed, with confidence and supporting signals. Currently mid-MVP: audio acquisition and preprocessing work end to end; ML detection, aggregation, and the agent layer have not been implemented yet.

## Repository layout

Monorepo with two independently-run apps sharing one git history:

- `src/` — .NET 10 solution (`AiMusicDetector.slnx`)
  - `Api` — ASP.NET Core minimal API (entry point), references `Core`, `Infrastructure`, `Analysis`
  - `Core` — EF-mapped entities (`Core.Entities`), plain domain models (`Core.Models`), and the interfaces other projects implement, split into `Core.Repositories` (persistence seams) and `Core.Services` (business-logic and external-integration seams)
  - `Infrastructure` — implements `Core.Repositories` (EF Core/Postgres) and the external-integration half of `Core.Services` (yt-dlp acquisition, ffmpeg preprocessing, Groq web research), under matching `Repositories`/`Services` folders; references `Core`
  - `Analysis` — implements the business-logic half of `Core.Services` (`IAnalysisService`, `IAnalysisPipeline` today; detection/aggregation later) under its own `Services` folder; references `Core`
- `tests/Core.Tests` — xUnit, references `Core`, `Analysis` and `Infrastructure` (external integrations are tested against stubbed `HttpMessageHandler`s / fakes, never real services)
- `web/` — Vite + React + TypeScript + MUI frontend

Planned but not yet present: `ml/` (Python ML models).

**yt-dlp** is the open-source CLI tool `Infrastructure` shells out to for downloading the audio-only stream from a YouTube/YouTube Music URL — it is not a library dependency, just an executable. The API's Docker image (`src/Api/Dockerfile`) bundles the standalone `yt-dlp_linux` binary so nothing needs installing on the host when running via `docker compose`; running the API with `dotnet run` instead requires `yt-dlp` on the host `PATH` (see Prerequisites below).

**ffmpeg** is the second CLI tool `Infrastructure` shells out to: preprocessing uses it to decode whatever yt-dlp downloaded (webm/opus, m4a, ...) into mono 16-bit PCM WAV at a fixed sample rate, trimmed to a window from the middle of the track (`Ffmpeg` section in `appsettings.json`). The Docker image installs it via apt; `dotnet run` needs it on the host `PATH`.

**Groq** powers the one detection signal that exists so far, `GroqWebResearchSignalProvider`: it sends the track's title/artist to a GPT-OSS model with Groq's built-in `browser_search` tool and asks whether the track or artist is publicly known to be AI-generated. Evidence links are kept only if they appear in the search results Groq reports in `executed_tools` — the model's own cited URLs are never trusted on their own. The API key is a secret: put `GROQ_API_KEY=...` in the gitignored `.env` at the repo root (docker compose passes it as `Groq__ApiKey`); for `dotnet run`, set the `Groq__ApiKey` environment variable. Without a key the provider fails and the pipeline simply leaves that signal out.

`ROADMAP.md` tracks the phased MVP plan — check it before starting new work to see which phase a change belongs to.

## Commands

### Prerequisites
- Docker is the only prerequisite for running the API: `docker compose up -d` from the repo root builds and starts both Postgres and the API (with yt-dlp and ffmpeg bundled in its image), migrating the database on startup. The API is reachable at `http://localhost:5214`, same as the `dotnet run` dev workflow.
- Running the API with `dotnet run` instead of Docker needs two things Docker otherwise provides: Postgres reachable at the `Postgres` connection string in `appsettings.json` (`docker compose up -d postgres` is enough), and `yt-dlp` plus `ffmpeg` on the host `PATH` (override the locations with `YtDlp:ExecutablePath` / `Ffmpeg:ExecutablePath` if they live elsewhere).

### API (`src/Api`, run from repo root)
- Build: `dotnet build`
- Run (dev, hot-reload, needs local Postgres + yt-dlp + ffmpeg — see Prerequisites): `dotnet run --project src/Api` — serves on `http://localhost:5214` (see `src/Api/Properties/launchSettings.json`); in Development only, the OpenAPI document is served at `/openapi/v1.json` and the Scalar UI over it at `/scalar/v1`.
- Run (containerized, no local prerequisites): `docker compose up -d --build` from the repo root.
- Test: `dotnet test`
- Add a migration: `dotnet ef migrations add <Name> --project src/Infrastructure --startup-project src/Api --output-dir Migrations` (`dotnet ef` comes from the local tool manifest — run `dotnet tool restore` once).

### Web (`web/`)
- Install: `npm install`
- Dev server: `npm run dev` — serves on `http://localhost:5173`
- Build (typecheck + bundle): `npm run build`
- Lint: `npm run lint` (oxlint)
- Test: `npm test` (Vitest + React Testing Library on jsdom, single run); `npm run test:watch` for watch mode
- Preview production build: `npm run preview`

## Architecture notes

- The API is a minimal-API project (no MVC controllers) — endpoints are registered directly in `src/Api/Program.cs` via `app.MapGet`/etc.
- CORS in `Program.cs` is restricted to the Vite dev origin (`http://localhost:5173`) — update this policy if the frontend's dev port or deployed origin changes.
- The frontend reads the API base URL from `VITE_API_BASE_URL` (Vite env var), defaulting to `http://localhost:5214` (see `web/src/App.tsx`). Both apps must be running simultaneously for the frontend to reach the API.
- Project references are wired so the dependency direction stays fixed (`Api` → all three, `Infrastructure`/`Analysis` → `Core`).
- **Layering rule: endpoints never depend on a repository directly.** `Api` endpoints call a `Core.Services` interface (e.g. `IAnalysisService`) that owns the business logic (validation, orchestration) and is the only thing that talks to `Core.Repositories`. This keeps request-time rules in one place instead of scattered across endpoint handlers. `AnalysisService` (in `Analysis/Services`) is the current example — it validates the URL and delegates to `IAnalysisJobRepository`. Background services (e.g. `AnalysisWorker`) are the pipeline's own business logic, not a thin API layer, so they may use repositories directly — though the per-job stages live in `AnalysisPipeline` (in `Analysis`, unit-tested with fakes) and the worker only claims jobs and hands them over.
- **Folder convention**: abstractions and their implementations are grouped by kind, not dumped in a generic `Abstractions`/`Contracts` folder — `Repositories/` for persistence seams, `Services/` for everything else (business logic and external integrations). This applies in `Core` (interfaces) and in `Infrastructure`/`Analysis` (implementations). Api request/response DTOs live under `Api/Models/<EndpointGroup>/`, one type per file, grouped by the endpoint group that owns them (mirrors the endpoint file in `Api/Endpoints`).
- **Entity naming**: a `Core` model that EF Core maps to a table lives in `Core/Entities/`, and its class name carries an `Entity` suffix (e.g. `AnalysisJobEntity`, mapped via `AnalysisDbContext`). Types under `Core/Models/` (`Track`, `AnalysisResult`, `Signal`, ...) are plain value objects — some are embedded inside an entity's `jsonb` column, but none has its own table, so they stay unsuffixed in `Models/`.
- Detection is deliberately many independent `IDetectionSignalProvider`s rather than one classifier, so a weak or failing detector lowers confidence instead of breaking the analysis.
- Work is queued, not done in the request: `POST /api/analyze` persists a job and returns 202, `AnalysisWorker` (an `IHostedService` inside the API process — no separate worker project) claims it, `AnalysisPipeline` runs it through every stage to `Completed`/`Failed` and deletes its audio files, and the client polls `GET /api/analyze/{id}`.
- **One job at a time, one worker.** The MVP deliberately processes jobs sequentially in a single worker. That is what makes startup recovery simple: when the worker starts, nothing can really be in flight, so `IAnalysisPipeline.RecoverInterruptedAsync` requeues any job still in `Acquiring`/`Preprocessing`/`Analyzing` (stranded by a shutdown) and deletes its leftover audio. Claiming still uses `FOR UPDATE SKIP LOCKED`, but running a second worker (another replica or parallel loops) would also need a lease/heartbeat, or recovery would requeue a job another worker still owns.
- Signal providers are all `IDetectionSignalProvider`s registered in DI; the pipeline runs each one during `Analyzing` and leaves out any that throws. Until aggregation (Phase 4) exists, the result keeps the collected signals (with their `Evidence` links) but the verdict stays `Inconclusive`.
- `AnalysisJob.Track` and `.Result` are stored as `jsonb` via value converters — they are read and written whole, never queried by inner fields, which keeps migrations out of the way as those shapes evolve.
- Acquisition deliberately does not transcode, so it needs no ffmpeg; format normalization belongs to preprocessing.
- **Database naming**: Postgres identifiers (tables, columns) are snake_case, applied automatically by `EFCore.NamingConventions`'s `.UseSnakeCaseNamingConvention()` in `InfrastructureServiceCollectionExtensions`. Don't hand-roll column names to work around this — raw SQL (e.g. the `FOR UPDATE SKIP LOCKED` query in `EfAnalysisJobRepository`) must reference the snake_case names directly since the naming convention doesn't rewrite raw SQL.

## Web conventions

- TypeScript models/types live in `web/src/models/` (one file per type, e.g. `Track.ts`, `AnalysisJob.ts`), separate from the components and `api.ts` that use them.
- Tests sit next to the file they cover (`App.test.tsx`, `api.test.ts`); shared test setup and helpers live in `web/src/test/`. Tests never hit a real API — stub `fetch` with `mockApi` from `src/test/mockApi.ts`, which routes by `"METHOD /path"` and throws on any request it wasn't told about. Query elements the way a user finds them (role + accessible name) rather than by class or test id. Polling is tested with Vitest fake timers (`vi.useFakeTimers({ shouldAdvanceTime: true })` plus `userEvent.setup({ advanceTimers: vi.advanceTimersByTime })`).
- The web job in CI (`.github/workflows/ci.yml`) runs lint, then tests, then build; a failing test fails the job.
- Style: always terminate statements with semicolons. Control-flow bodies (`if`/`else`/`for`/etc.) always use braces, even for one-liners — never `if (x) return y;`. A `return` (or any statement) inside a block goes on its own line.
