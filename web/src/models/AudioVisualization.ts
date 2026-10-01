export type Spectrogram = {
  frames: number;
  bands: number;
  minFrequency: number;
  maxFrequency: number;
  minDecibels: number;
  /** Base64 of frames x bands bytes, time-major and lowest band first; 0..255 = minDecibels..0 dB. */
  values: string;
};

export type AudioVisualization = {
  /** Where the analyzed excerpt starts within the full track. */
  startSeconds: number;
  durationSeconds: number;
  /** Peak amplitude per slice, 0..1. */
  waveform: number[];
  spectrogram: Spectrogram;
};
