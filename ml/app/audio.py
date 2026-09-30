from typing import BinaryIO

import numpy as np
import soundfile as sf

from app.detectors.base import AudioClip


class InvalidAudioError(ValueError):
    pass


def load_clip(file: BinaryIO) -> AudioClip:
    """Decodes an uploaded WAV (or any format libsndfile reads) and downmixes it to mono."""
    try:
        samples, sample_rate = sf.read(file, dtype="float32", always_2d=True)
    except (sf.LibsndfileError, RuntimeError, TypeError) as ex:
        raise InvalidAudioError(f"Could not decode the uploaded audio: {ex}") from ex

    if samples.shape[0] == 0:
        raise InvalidAudioError("The uploaded audio has no samples.")

    return AudioClip(samples=np.mean(samples, axis=1), sample_rate=int(sample_rate))
