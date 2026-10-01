from fastapi import FastAPI, HTTPException, UploadFile
from pydantic import BaseModel

from app.audio import InvalidAudioError, load_clip
from app.detectors.base import AudioClip, Detector
from app.detectors.fakeprint import FakeprintDetector
from app.visualize import visualize

DETECTORS: dict[str, Detector] = {detector.id: detector for detector in [FakeprintDetector()]}

app = FastAPI(title="AI Music Detector ML service")


class HealthResponse(BaseModel):
    status: str
    detectors: list[str]


class SignalResponse(BaseModel):
    """Same shape as the API's Signal, minus evidence links, which audio detectors never produce."""

    name: str
    score: float
    weight: float
    detail: str | None


class SpectrogramResponse(BaseModel):
    """`values` is base64 of frames x bands bytes (time-major, lowest band first), 0..255 = min_decibels..0 dB."""

    frames: int
    bands: int
    min_frequency: float
    max_frequency: float
    min_decibels: float
    values: str


class VisualizationResponse(BaseModel):
    duration_seconds: float
    waveform: list[float]
    spectrogram: SpectrogramResponse


@app.get("/health")
def health() -> HealthResponse:
    return HealthResponse(status="ok", detectors=sorted(DETECTORS))


# Sync handlers on purpose: FastAPI runs them in a worker thread, so CPU-bound work
# doesn't block the event loop.
@app.post("/detect/{detector_id}")
def detect(detector_id: str, audio: UploadFile) -> SignalResponse:
    detector = DETECTORS.get(detector_id)
    if detector is None:
        raise HTTPException(status_code=404, detail=f"Unknown detector '{detector_id}'.")

    clip = _load(audio)
    result = detector.detect(clip)
    return SignalResponse(name=detector.name, score=result.score, weight=result.weight, detail=result.detail)


@app.post("/visualize")
def visualize_audio(audio: UploadFile) -> VisualizationResponse:
    """Waveform and spectrogram for the result page; not a detection signal."""
    picture = visualize(_load(audio))
    spectrogram = picture.spectrogram

    return VisualizationResponse(
        duration_seconds=picture.duration_seconds,
        waveform=picture.waveform,
        spectrogram=SpectrogramResponse(
            frames=spectrogram.frames,
            bands=spectrogram.bands,
            min_frequency=spectrogram.min_frequency,
            max_frequency=spectrogram.max_frequency,
            min_decibels=spectrogram.min_decibels,
            values=spectrogram.values_base64,
        ),
    )


def _load(audio: UploadFile) -> AudioClip:
    try:
        return load_clip(audio.file)
    except InvalidAudioError as ex:
        raise HTTPException(status_code=422, detail=str(ex)) from ex
