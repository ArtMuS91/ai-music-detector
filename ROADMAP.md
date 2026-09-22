# Roadmap

Informal backlog for this solo pet project. Not a commitment or schedule — just a place to keep track of what's next and what's deliberately deferred.

## MVP path (not yet built)

The core flow from the original brief: YouTube URL → audio acquisition → preprocessing → AI detection models → signal aggregation → AI analysis agent → result + confidence + explanation.

- Audio acquisition from a YouTube/YouTube Music URL
- Audio preprocessing pipeline
- Background processing for audio analysis (.NET)
- AI detection models (Python/PyTorch, Hugging Face models, Librosa/torchaudio)
- Optional vocal/transcription analysis via Whisper
- Multiple independent detection signals (spectral anomalies, vocal synthesis indicators, generative-audio artifacts, metadata) rather than a single classifier
- Feature/signal aggregation
- AI analysis agent (Groq API, MCP tools, RAG over AI-generated-music detection knowledge)
- Result UI: AI-generated probability, confidence, supporting signals, waveform/spectrogram visualization

## Infrastructure (deferred from initial scaffolding)

- `docker-compose.yml` (API, web, Postgres, Python ML service)
- PostgreSQL + EF Core wiring in `Infrastructure`
- `ml/` Python project for audio ML models
- `tests/`
- GitHub Actions CI
- OpenTelemetry observability

## Post-MVP features

- Direct file uploads (as an alternative to a YouTube URL)
- Spotify integration
- Model attribution (identifying which generative model/tool likely produced a track)
- More advanced multi-agent functionality
