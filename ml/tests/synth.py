import io

import numpy as np
import soundfile as sf

SAMPLE_RATE = 44_100


def noise(seconds: float = 20.0, seed: int = 0) -> np.ndarray:
    """Broadband noise: a smooth long-term spectrum, like well-mixed recorded music."""
    return (np.random.default_rng(seed).standard_normal(int(seconds * SAMPLE_RATE)) * 0.1).astype(np.float32)


def with_peak_comb(samples: np.ndarray, spacing_hz: float) -> np.ndarray:
    """Adds evenly spaced tones across the analysed band, like a decoder's upsampling artifacts."""
    t = np.arange(samples.size) / SAMPLE_RATE
    comb = sum(np.sin(2 * np.pi * f * t) for f in np.arange(spacing_hz * 12, 16_000, spacing_hz))
    return (samples + 0.005 * comb).astype(np.float32)


def wav_bytes(samples: np.ndarray, channels: int = 1) -> bytes:
    buffer = io.BytesIO()
    data = np.column_stack([samples] * channels) if channels > 1 else samples
    sf.write(buffer, data, SAMPLE_RATE, format="WAV", subtype="PCM_16")
    return buffer.getvalue()
