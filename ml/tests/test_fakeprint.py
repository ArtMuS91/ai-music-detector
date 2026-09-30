from pathlib import Path

import numpy as np

from app import dsp
from app.detectors.base import AudioClip
from app.detectors.fakeprint import FakeprintDetector
from tests.synth import SAMPLE_RATE, noise, reference_input

detector = FakeprintDetector()


def test_returns_a_probability_with_weight():
    result = detector.detect(AudioClip(noise(seconds=20), SAMPLE_RATE))

    assert 0.0 <= result.score <= 1.0
    assert result.weight > 0
    assert "model probability" in result.detail


def test_fakeprint_has_one_value_per_model_feature_normalized_to_one():
    fakeprint = detector.fakeprint(AudioClip(noise(seconds=20), SAMPLE_RATE))

    assert fakeprint.shape == (3585,)
    assert fakeprint.min() >= 0.0
    assert np.isclose(fakeprint.max(), 1.0, atol=1e-3)


def test_short_audio_gives_no_signal():
    result = detector.detect(AudioClip(noise(seconds=5), SAMPLE_RATE))

    assert result.weight == 0.0


def test_silence_gives_no_signal():
    result = detector.detect(AudioClip(np.zeros(SAMPLE_RATE * 20, dtype=np.float32), SAMPLE_RATE))

    assert result.weight == 0.0


def test_fakeprint_matches_torchaudio_reference():
    """
    The model was trained on features from torchaudio's Resample and Spectrogram; the numpy port
    must reproduce them. The fixture was generated once with torchaudio 2.11 from the same input.
    """
    reference = np.load(Path(__file__).parent / "fixtures" / "fakeprint_torchaudio_reference.npy")

    fakeprint = detector.fakeprint(AudioClip(reference_input(), SAMPLE_RATE))

    # Float32 rounding differs slightly between the two, and the hull's minimum filter can
    # magnify it in single bins; what must hold is the average and the model's output.
    assert np.abs(fakeprint - reference).mean() < 1e-4
    assert abs(_probability(fakeprint) - _probability(reference)) < 1e-4


def _probability(fakeprint: np.ndarray) -> float:
    return 1.0 / (1.0 + np.exp(-(float(np.dot(fakeprint, detector._weights)) + detector._bias)))


def test_resample_keeps_passband_and_length():
    tone = np.sin(2 * np.pi * 1_000 * np.arange(SAMPLE_RATE) / SAMPLE_RATE).astype(np.float32)

    resampled = dsp.resample(tone, SAMPLE_RATE, 16_000)

    assert resampled.size == 16_000
    expected = np.sin(2 * np.pi * 1_000 * np.arange(16_000) / 16_000)
    assert np.abs(resampled[500:-500] - expected[500:-500]).max() < 1e-3
