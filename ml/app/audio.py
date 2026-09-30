from typing import BinaryIO

import numpy as np
import soundfile as sf

from app.detectors.base import AudioClip

# Five minutes of 48 kHz stereo (~115 MB once decoded to float32). Preprocessing already trims
# tracks to a three-minute mono window, so this only turns away uploads that would exhaust memory.
MAX_SAMPLES = 48_000 * 60 * 5 * 2


class InvalidAudioError(ValueError):
    pass


def load_clip(file: BinaryIO) -> AudioClip:
    """Decodes an uploaded WAV (or any format libsndfile reads) and downmixes it to mono."""
    try:
        # Reads only the header, so an oversized upload is rejected before it is decoded.
        info = sf.info(file)
        if info.frames * info.channels > MAX_SAMPLES:
            raise InvalidAudioError(
                f"The uploaded audio is too long ({info.duration:.0f} s, {info.channels} channel(s))."
            )

        file.seek(0)
        samples, sample_rate = sf.read(file, dtype="float32", always_2d=True)
    except (sf.LibsndfileError, RuntimeError, TypeError) as ex:
        raise InvalidAudioError(f"Could not decode the uploaded audio: {ex}") from ex

    if samples.shape[0] == 0:
        raise InvalidAudioError("The uploaded audio has no samples.")

    return AudioClip(samples=np.mean(samples, axis=1), sample_rate=int(sample_rate))
