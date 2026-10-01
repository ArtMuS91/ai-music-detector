"""
Compact pictures of a clip for the result page: a peak waveform and a log-frequency spectrogram.
The API deletes the audio once a job finishes, so these are stored with the job instead; they are
sized for display (a few tens of KB), not for analysis.
"""

import base64
from dataclasses import dataclass

import numpy as np

from app import dsp
from app.detectors.base import AudioClip

WAVEFORM_POINTS = 800
SPECTROGRAM_FRAMES = 400
SPECTROGRAM_BANDS = 96
MIN_FREQUENCY = 30.0
MIN_DECIBELS = -80.0
_N_FFT = 2048
_SILENCE = 1e-12


@dataclass(frozen=True)
class Spectrogram:
    """
    `values` is frames x bands, time-major, lowest band first: each byte maps 0..255 linearly onto
    MIN_DECIBELS..0 dB relative to the loudest cell. Bands are log-spaced from min to max frequency.
    """

    frames: int
    bands: int
    min_frequency: float
    max_frequency: float
    min_decibels: float
    values: bytes

    @property
    def values_base64(self) -> str:
        return base64.b64encode(self.values).decode("ascii")


@dataclass(frozen=True)
class Visualization:
    duration_seconds: float
    waveform: list[float]
    spectrogram: Spectrogram


def visualize(clip: AudioClip) -> Visualization:
    return Visualization(
        duration_seconds=clip.duration_seconds,
        waveform=waveform(clip.samples),
        spectrogram=spectrogram(clip),
    )


def waveform(samples: np.ndarray, points: int = WAVEFORM_POINTS) -> list[float]:
    """Peak amplitude per slice, scaled so the loudest slice is 1 (silence stays all zeros)."""
    chunks = np.array_split(np.abs(samples), min(points, samples.size))
    peaks = np.array([chunk.max() for chunk in chunks], dtype=np.float64)
    loudest = peaks.max()
    if loudest > 0:
        peaks /= loudest

    return [round(float(peak), 3) for peak in peaks]


def spectrogram(clip: AudioClip, frames: int = SPECTROGRAM_FRAMES, bands: int = SPECTROGRAM_BANDS) -> Spectrogram:
    max_frequency = clip.sample_rate / 2
    # Each output frame averages the STFT frames of its own slice, which keeps memory flat for a
    # three-minute clip (one full STFT would be over 100 MB) and smooths transients for display.
    frames = max(1, min(frames, clip.samples.size // _N_FFT))
    slices = np.array_split(clip.samples, frames)
    power = np.stack([dsp.power_spectrogram(_pad_to_fft(chunk), _N_FFT).mean(axis=1) for chunk in slices])

    band_power = power @ _band_matrix(clip.sample_rate, bands, MIN_FREQUENCY, max_frequency)
    decibels = 10 * np.log10(np.maximum(band_power, _SILENCE))
    # Relative to the loudest cell, except that pure silence stays at the floor instead of
    # becoming "loudest" itself.
    reference = max(decibels.max(), 10 * np.log10(_SILENCE) - MIN_DECIBELS)
    relative = np.clip(decibels - reference, MIN_DECIBELS, 0)
    quantized = np.round((relative - MIN_DECIBELS) / -MIN_DECIBELS * 255).astype(np.uint8)

    return Spectrogram(
        frames=frames,
        bands=bands,
        min_frequency=MIN_FREQUENCY,
        max_frequency=max_frequency,
        min_decibels=MIN_DECIBELS,
        values=quantized.tobytes(),
    )


def _pad_to_fft(chunk: np.ndarray) -> np.ndarray:
    # Reflect padding in power_spectrogram needs more than n_fft // 2 samples.
    return chunk if chunk.size > _N_FFT else np.pad(chunk, (0, _N_FFT + 1 - chunk.size))


def _band_matrix(sample_rate: int, bands: int, low: float, high: float) -> np.ndarray:
    """(fft bins, bands) matrix averaging the bins in each log-spaced band."""
    bin_frequencies = np.fft.rfftfreq(_N_FFT, 1 / sample_rate)
    edges = np.geomspace(low, high, bands + 1)
    matrix = np.zeros((bin_frequencies.size, bands))

    for band in range(bands):
        in_band = (bin_frequencies >= edges[band]) & (bin_frequencies < edges[band + 1])
        if not in_band.any():
            # Low bands are narrower than one FFT bin; borrow the bin nearest their center.
            center = np.sqrt(edges[band] * edges[band + 1])
            in_band = np.arange(bin_frequencies.size) == np.argmin(np.abs(bin_frequencies - center))
        matrix[in_band, band] = 1 / in_band.sum()

    return matrix
