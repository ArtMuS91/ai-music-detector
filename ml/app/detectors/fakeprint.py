from pathlib import Path

import numpy as np
from scipy.ndimage import minimum_filter1d

from app import dsp
from app.detectors.base import AudioClip, Detection

_WEIGHTS_PATH = Path(__file__).resolve().parent.parent / "models" / "fakeprint" / "weights.npz"

# Must match the extraction the model was trained with; see models/fakeprint/README.md.
_SAMPLE_RATE = 16_000
_N_FFT = 8192
_FREQ_HZ = (1_000, 8_000)
_HULL_BINS = 10
_MAX_DB = 5.0
_MIN_DB = -45.0
_MAX_SECONDS = 300

# On test 14-track YouTube check (7 known AI, 7 human) it separated every track, AI at 1.00 and
# human at <= 0.005, so re-encoding to Opus does not blur it. Not 1.0: a small sample, and
# generators newer than its training data (Suno > 5, Udio > 1.5) may not leave the same print.
_WEIGHT = 0.8

_SILENCE_RMS = 1e-3


class FakeprintDetector:
    """
    A trained classifier (logistic regression) over a track's "fakeprint": the fine peak
    structure left in the average spectrum by the transposed-convolution decoders of AI music
    generators (Afchar et al., "A Fourier Explanation of AI-music Artifacts", ISMIR 2025).
    Trained on Suno <= 5 and Udio <= 1.5.
    """

    id = "fakeprint"
    name = "Generator fingerprint"

    def __init__(self) -> None:
        weights = np.load(_WEIGHTS_PATH)
        self._weights = weights["weights"].reshape(-1).astype(np.float64)
        self._bias = float(weights["bias"].reshape(-1)[0])

        freqs = np.linspace(0, _SAMPLE_RATE / 2, num=_N_FFT // 2 + 1)
        self._band = (freqs >= _FREQ_HZ[0]) & (freqs <= _FREQ_HZ[1])
        if int(self._band.sum()) != self._weights.size:
            raise ValueError(f"Model expects {self._weights.size} features, extraction gives {int(self._band.sum())}.")

    def detect(self, clip: AudioClip) -> Detection:
        if clip.duration_seconds < 10:
            return Detection(score=0.5, weight=0.0, detail="The audio is too short for a reliable fingerprint.")

        if float(np.sqrt(np.mean(np.square(clip.samples, dtype=np.float64)))) < _SILENCE_RMS:
            return Detection(score=0.5, weight=0.0, detail="The audio is nearly silent.")

        probability = self.probability(clip)
        verdict = "matches" if probability >= 0.5 else "does not match"
        return Detection(
            score=probability,
            weight=_WEIGHT,
            detail=f"The spectral fingerprint {verdict} AI music generators such as Suno and Udio "
            f"(model probability {probability:.2f}).",
        )

    def probability(self, clip: AudioClip) -> float:
        logit = float(np.dot(self.fakeprint(clip), self._weights)) + self._bias
        return 1.0 / (1.0 + np.exp(-logit))

    def fakeprint(self, clip: AudioClip) -> np.ndarray:
        samples = dsp.resample(clip.samples, clip.sample_rate, _SAMPLE_RATE)[: _MAX_SECONDS * _SAMPLE_RATE]

        power = dsp.power_spectrogram(samples, _N_FFT)
        mean_db = (10 * np.log10(np.clip(power, 1e-10, 1e6))).mean(axis=1)
        spectrum = mean_db[self._band]

        hull = np.clip(minimum_filter1d(spectrum, size=_HULL_BINS, mode="nearest"), _MIN_DB, None)
        residue = np.clip(spectrum - hull, 0, _MAX_DB)
        return residue / (residue.max() + 1e-6)
