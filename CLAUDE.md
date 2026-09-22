# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

AI Music Detector — a web app where a user pastes a YouTube/YouTube Music link and the system estimates whether the track is AI-generated, human-created, or mixed, with confidence and supporting signals. Currently at MVP scaffolding stage: only the API and web skeletons exist; no audio acquisition, ML analysis, or agent layer has been implemented yet.

## Repository layout

Monorepo with two independently-run apps sharing one git history:

- `src/` — .NET 10 solution (`AiMusicDetector.slnx`)
  - `Api` — ASP.NET Core minimal API (entry point), references `Core`, `Infrastructure`, `Analysis`
  - `Core` — class library, intended for domain models/interfaces (currently empty)
  - `Infrastructure` — class library, intended for persistence/external services (currently empty), references `Core`
  - `Analysis` — class library, intended for audio analysis orchestration (currently empty), references `Core`
- `web/` — Vite + React + TypeScript + MUI frontend

Planned but not yet present: `ml/` (Python ML models), `tests/`, `docker-compose.yml`, Postgres/EF Core wiring.

## Commands

### API (`src/Api`, run from repo root)
- Build: `dotnet build`
- Run (dev): `dotnet run --project src/Api` — serves on `http://localhost:5214` (see `src/Api/Properties/launchSettings.json`); OpenAPI docs are mapped in Development only.

### Web (`web/`)
- Install: `npm install`
- Dev server: `npm run dev` — serves on `http://localhost:5173`
- Build (typecheck + bundle): `npm run build`
- Lint: `npm run lint` (oxlint)
- Preview production build: `npm run preview`

No test runner is configured yet in either project.

## Architecture notes

- The API is a minimal-API project (no MVC controllers) — endpoints are registered directly in `src/Api/Program.cs` via `app.MapGet`/etc.
- CORS in `Program.cs` is restricted to the Vite dev origin (`http://localhost:5173`) — update this policy if the frontend's dev port or deployed origin changes.
- The frontend reads the API base URL from `VITE_API_BASE_URL` (Vite env var), defaulting to `http://localhost:5214` (see `web/src/App.tsx`). Both apps must be running simultaneously for the frontend to reach the API.
- `Core`/`Infrastructure`/`Analysis` are scaffolded as empty class libraries with project references already wired (`Api` → all three, `Infrastructure`/`Analysis` → `Core`) so the intended dependency direction is fixed even before real code lands in them.
