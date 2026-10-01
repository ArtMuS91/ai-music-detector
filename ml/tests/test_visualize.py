import base64

import numpy as np

from app.detectors.base import AudioClip
from app.visualize import MIN_DECIBELS, SPECTROGRAM_BANDS, SPECTROGRAM_FRAMES, WAVEFORM_POINTS, spectrogram, waveform
from tests.synth import SAMPLE_RATE, noise


def tone(frequency: float, seconds: float = 10.0) -> np.ndarray:
    t = np.arange(int(seconds * SAMPLE_RATE)) / SAMPLE_RATE
    return (0.5 * np.sin(2 * np.pi * frequency * t)).astype(np.float32)


def test_waveform_has_fixed_resolution_and_peaks_at_one():
    samples = noise(seconds=10)
    samples[: samples.size // 2] *= 0.1

    peaks = waveform(samples)

    assert len(peaks) == WAVEFORM_POINTS
    assert max(peaks) == 1.0
    # The quiet first half reads as quiet.
    assert max(peaks[: WAVEFORM_POINTS // 2 - 1]) < 0.2


def test_waveform_of_short_audio_has_one_point_per_sample():
    assert len(waveform(np.array([0.1, -0.5, 0.25], dtype=np.float32))) == 3


def test_waveform_of_silence_stays_zero():
    assert set(waveform(np.zeros(SAMPLE_RATE, dtype=np.float32))) == {0.0}


def test_spectrogram_shape_and_range():
    result = spectrogram(AudioClip(noise(seconds=20), SAMPLE_RATE))

    assert (result.frames, result.bands) == (SPECTROGRAM_FRAMES, SPECTROGRAM_BANDS)
    assert len(result.values) == result.frames * result.bands
    assert result.max_frequency == SAMPLE_RATE / 2
    assert result.min_decibels == MIN_DECIBELS
    assert max(result.values) == 255
    assert base64.b64decode(result.values_base64) == result.values


def test_spectrogram_puts_a_tone_in_its_band():
    result = spectrogram(AudioClip(tone(1000), SAMPLE_RATE))
    grid = np.frombuffer(result.values, dtype=np.uint8).reshape(result.frames, result.bands)

    edges = np.geomspace(result.min_frequency, result.max_frequency, result.bands + 1)
    expected_band = np.searchsorted(edges, 1000) - 1
    assert abs(int(np.argmax(grid.mean(axis=0))) - expected_band) <= 1


def test_spectrogram_of_short_audio_uses_fewer_frames():
    result = spectrogram(AudioClip(noise(seconds=0.5), SAMPLE_RATE))

    assert result.frames == int(0.5 * SAMPLE_RATE) // 2048
    assert len(result.values) == result.frames * result.bands


def test_spectrogram_of_silence_stays_at_the_floor():
    result = spectrogram(AudioClip(np.zeros(SAMPLE_RATE * 2, dtype=np.float32), SAMPLE_RATE))

    assert set(result.values) == {0}
