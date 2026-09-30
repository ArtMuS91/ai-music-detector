from fastapi import FastAPI, HTTPException, UploadFile
from pydantic import BaseModel

from app.audio import InvalidAudioError, load_clip
from app.detectors.base import Detector
from app.detectors.spectral import SpectralArtifactDetector

DETECTORS: dict[str, Detector] = {detector.id: detector for detector in [SpectralArtifactDetector()]}

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


@app.get("/health")
def health() -> HealthResponse:
    return HealthResponse(status="ok", detectors=sorted(DETECTORS))


# Sync handler on purpose: FastAPI runs it in a worker thread, so CPU-bound detection
# doesn't block the event loop.
@app.post("/detect/{detector_id}")
def detect(detector_id: str, audio: UploadFile) -> SignalResponse:
    detector = DETECTORS.get(detector_id)
    if detector is None:
        raise HTTPException(status_code=404, detail=f"Unknown detector '{detector_id}'.")

    try:
        clip = load_clip(audio.file)
    except InvalidAudioError as ex:
        raise HTTPException(status_code=422, detail=str(ex)) from ex

    result = detector.detect(clip)
    return SignalResponse(name=detector.name, score=result.score, weight=result.weight, detail=result.detail)
