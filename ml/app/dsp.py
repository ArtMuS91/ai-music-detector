"""
Numpy ports of the torchaudio transforms that trained models expect, so the service needs no
PyTorch. They reproduce torchaudio's defaults exactly, because a classifier trained on its
features is sensitive to small differences (e.g. how the resampler rolls off near Nyquist).
"""

import math

import numpy as np

# torchaudio.transforms.Resample defaults (resampling_method="sinc_interp_hann").
_LOWPASS_FILTER_WIDTH = 6
_ROLLOFF = 0.99


def resample(samples: np.ndarray, orig_rate: int, new_rate: int) -> np.ndarray:
    """Same result as torchaudio.transforms.Resample(orig_rate, new_rate) with its defaults."""
    if orig_rate == new_rate:
        return samples

    gcd = math.gcd(orig_rate, new_rate)
    orig, new = orig_rate // gcd, new_rate // gcd
    kernel, width = _sinc_kernel(orig, new)

    length = samples.size
    padded = np.pad(samples.astype(np.float32), (width, width + orig))

    # conv1d with stride `orig`: each window yields `new` output samples, one per kernel phase.
    frames = (padded.size - kernel.shape[1]) // orig + 1
    windows = np.lib.stride_tricks.as_strided(
        padded,
        shape=(frames, kernel.shape[1]),
        strides=(padded.strides[0] * orig, padded.strides[0]),
        writeable=False,
    )
    resampled = (windows @ kernel.T).reshape(-1)

    return resampled[: math.ceil(new * length / orig)]


def _sinc_kernel(orig: int, new: int) -> tuple[np.ndarray, int]:
    base_freq = min(orig, new) * _ROLLOFF
    width = math.ceil(_LOWPASS_FILTER_WIDTH * orig / base_freq)

    idx = np.arange(-width, width + orig, dtype=np.float64)[None, :] / orig
    t = (np.arange(0, -new, -1, dtype=np.float64)[:, None] / new + idx) * base_freq
    t = np.clip(t, -_LOWPASS_FILTER_WIDTH, _LOWPASS_FILTER_WIDTH)

    window = np.cos(t * math.pi / _LOWPASS_FILTER_WIDTH / 2) ** 2
    t *= math.pi
    with np.errstate(invalid="ignore", divide="ignore"):
        kernel = np.where(t == 0, 1.0, np.sin(t) / t)
    kernel *= window * base_freq / orig

    return kernel.astype(np.float32), width


def power_spectrogram(samples: np.ndarray, n_fft: int) -> np.ndarray:
    """
    Same result as torchaudio.transforms.Spectrogram(n_fft, power=2) with its defaults:
    periodic Hann window of n_fft, hop n_fft // 2, centered frames with reflect padding.
    Returns shape (n_fft // 2 + 1, frames).
    """
    hop = n_fft // 2
    padded = np.pad(samples.astype(np.float32), n_fft // 2, mode="reflect")
    frames = 1 + (padded.size - n_fft) // hop
    windows = np.lib.stride_tricks.as_strided(
        padded,
        shape=(frames, n_fft),
        strides=(padded.strides[0] * hop, padded.strides[0]),
        writeable=False,
    )
    hann = (0.5 - 0.5 * np.cos(2 * math.pi * np.arange(n_fft) / n_fft)).astype(np.float32)

    return (np.abs(np.fft.rfft(windows * hann, axis=1)) ** 2).T
