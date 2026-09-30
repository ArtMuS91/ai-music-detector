from fastapi.testclient import TestClient

from app.main import app
from tests.synth import noise, wav_bytes

client = TestClient(app)


def upload(content: bytes) -> dict:
    return {"audio": ("track.wav", content, "audio/wav")}


def test_health_lists_detectors():
    response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {"status": "ok", "detectors": ["spectral"]}


def test_detect_returns_a_signal():
    response = client.post("/detect/spectral", files=upload(wav_bytes(noise())))

    assert response.status_code == 200
    body = response.json()
    assert body["name"] == "Spectral artifacts"
    assert 0 <= body["score"] <= 1
    assert 0 <= body["weight"] <= 1


def test_stereo_upload_is_downmixed():
    response = client.post("/detect/spectral", files=upload(wav_bytes(noise(), channels=2)))

    assert response.status_code == 200


def test_unknown_detector_is_404():
    response = client.post("/detect/nope", files=upload(wav_bytes(noise(seconds=1))))

    assert response.status_code == 404


def test_undecodable_upload_is_422():
    response = client.post("/detect/spectral", files=upload(b"not audio at all"))

    assert response.status_code == 422
    assert "decode" in response.json()["detail"]


def test_missing_upload_is_422():
    response = client.post("/detect/spectral")

    assert response.status_code == 422
