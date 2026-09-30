from dataclasses import dataclass
from typing import Protocol

import numpy as np


@dataclass(frozen=True)
class AudioClip:
    """Mono audio as float32 samples in [-1, 1]."""

    samples: np.ndarray
    sample_rate: int

    @property
    def duration_seconds(self) -> float:
        return self.samples.size / self.sample_rate


@dataclass(frozen=True)
class Detection:
    """Mirrors the API's Signal: score 0 = strongly human, 1 = strongly AI; weight 0 = nothing to go on."""

    score: float
    weight: float
    detail: str | None = None


class Detector(Protocol):
    """One independent signal. The API calls each detector separately, so one failing costs only its own signal."""

    id: str
    name: str

    def detect(self, clip: AudioClip) -> Detection: ...
