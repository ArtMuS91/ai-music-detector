import math

import numpy as np
from scipy import ndimage
from scipy import signal as sps

from app.detectors.base import AudioClip, Detection

# Welch segment length: ~5.4 Hz bins at 44.1 kHz, fine enough to resolve closely spaced peaks.
_SEGMENT = 8192

# The band that carries the artifacts; lower frequencies are dominated by the music itself.
_BAND_HZ = (1_000.0, 16_000.0)

# Median-filter width that removes the spectrum's overall slope but keeps narrow peaks.
_TREND_BINS = 63

# Peak spacings to look for. Stacked 2x upsampling layers put them at sample_rate / total_stride.
_SPACING_HZ = (40.0, 2_000.0)

# About -60 dBFS: below this the spectrum is mostly noise floor.
_SILENCE_RMS = 1e-3

# Placeholder thresholds, not yet calibrated on labelled tracks. Hence the low weight.
_HUMAN_BELOW = 0.15
_AI_ABOVE = 0.5
_MAX_WEIGHT = 0.2


class SpectralArtifactDetector:
    """
    Looks for evenly spaced peaks in the track's long-term average spectrum. Neural audio
    decoders that upsample with transposed convolutions leave such a comb behind (described
    in Deezer's research on music-deepfake detection), while recorded and mixed music
    averages out to a smooth spectrum over a few minutes.

    Sustained harmonic content (a drone, a held organ chord) also forms a comb, which is one
    reason this signal is weighted low until it is calibrated.
    """

    id = "spectral"
    name = "Spectral artifacts"

    def detect(self, clip: AudioClip) -> Detection:
        samples = clip.samples

        if samples.size < _SEGMENT * 4:
            return _no_signal("The audio is too short to estimate a stable spectrum.")

        if math.sqrt(float(np.mean(np.square(samples, dtype=np.float64)))) < _SILENCE_RMS:
            return _no_signal("The audio is nearly silent.")

        freqs, power = sps.welch(samples, fs=clip.sample_rate, nperseg=_SEGMENT)
        band = (freqs >= _BAND_HZ[0]) & (freqs <= min(_BAND_HZ[1], 0.9 * clip.sample_rate / 2))
        spectrum_db = 10 * np.log10(power[band] + 1e-20)

        residual = spectrum_db - ndimage.median_filter(spectrum_db, size=_TREND_BINS, mode="nearest")
        residual -= residual.mean()

        periodicity, spacing_hz = _strongest_periodicity(residual, bin_hz=freqs[1] - freqs[0])
        score = min(max((periodicity - _HUMAN_BELOW) / (_AI_ABOVE - _HUMAN_BELOW), 0.0), 1.0)

        return Detection(
            score=score,
            weight=_MAX_WEIGHT,
            detail=f"Strongest regular spectral peak pattern repeats every {spacing_hz:.0f} Hz "
            f"(periodicity {periodicity:.2f}; above {_AI_ABOVE} suggests decoder artifacts).",
        )


def _strongest_periodicity(residual: np.ndarray, bin_hz: float) -> tuple[float, float]:
    """Highest normalized autocorrelation within the searched spacing range, and the spacing it occurs at."""
    energy = float(np.dot(residual, residual))
    if energy == 0:
        return 0.0, 0.0

    autocorrelation = np.correlate(residual, residual, mode="full")[residual.size - 1 :] / energy

    # Lags of a few bins are correlated by window leakage alone, so start well past them.
    min_lag = max(4, math.ceil(_SPACING_HZ[0] / bin_hz))
    max_lag = min(autocorrelation.size - 1, math.floor(_SPACING_HZ[1] / bin_hz))
    if max_lag <= min_lag:
        return 0.0, 0.0

    candidates = autocorrelation[min_lag : max_lag + 1]
    best = int(np.argmax(candidates))
    return max(float(candidates[best]), 0.0), (min_lag + best) * bin_hz


def _no_signal(detail: str) -> Detection:
    return Detection(score=0.5, weight=0.0, detail=detail)
