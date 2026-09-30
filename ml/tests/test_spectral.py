import numpy as np

from app.detectors.base import AudioClip
from app.detectors.spectral import SpectralArtifactDetector
from tests.synth import SAMPLE_RATE, noise, with_peak_comb

detector = SpectralArtifactDetector()


def test_smooth_spectrum_scores_human():
    result = detector.detect(AudioClip(noise(), SAMPLE_RATE))

    assert result.score == 0.0
    assert result.weight > 0


def test_evenly_spaced_peaks_score_ai_and_report_their_spacing():
    result = detector.detect(AudioClip(with_peak_comb(noise(), spacing_hz=86.0), SAMPLE_RATE))

    assert result.score == 1.0
    assert "86 Hz" in result.detail


def test_silence_gives_no_signal():
    result = detector.detect(AudioClip(np.zeros(SAMPLE_RATE * 10, dtype=np.float32), SAMPLE_RATE))

    assert result.weight == 0.0
    assert result.score == 0.5


def test_too_short_audio_gives_no_signal():
    result = detector.detect(AudioClip(noise(seconds=0.5), SAMPLE_RATE))

    assert result.weight == 0.0
