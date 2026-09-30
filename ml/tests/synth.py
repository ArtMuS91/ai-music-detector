import io

import numpy as np
import soundfile as sf

SAMPLE_RATE = 44_100


def noise(seconds: float = 20.0, seed: int = 0) -> np.ndarray:
    """Broadband noise: a smooth long-term spectrum, like well-mixed recorded music."""
    return (np.random.default_rng(seed).standard_normal(int(seconds * SAMPLE_RATE)) * 0.1).astype(np.float32)


def wav_bytes(samples: np.ndarray, channels: int = 1) -> bytes:
    buffer = io.BytesIO()
    data = np.column_stack([samples] * channels) if channels > 1 else samples
    sf.write(buffer, data, SAMPLE_RATE, format="WAV", subtype="PCM_16")
    return buffer.getvalue()


def reference_input() -> np.ndarray:
    """Deterministic 20 s of noise plus a sine sweep, used for the torchaudio parity fixture."""
    t = np.arange(int(20 * SAMPLE_RATE)) / SAMPLE_RATE
    sweep = 0.2 * np.sin(2 * np.pi * np.cumsum(np.linspace(50, 20_000, t.size)) / SAMPLE_RATE)
    return (noise(seconds=20, seed=42) * 0.5 + sweep).astype(np.float32)
