import type { Spectrogram } from './models';

/** Magma-like stops: dark for quiet, bright for loud; readable on light and dark backgrounds alike. */
const COLOR_STOPS: [number, number, number][] = [
  [0, 0, 4],
  [40, 11, 84],
  [101, 21, 110],
  [159, 42, 99],
  [212, 72, 66],
  [245, 125, 21],
  [250, 193, 39],
  [252, 255, 164],
];

/** 256-entry RGB lookup table over COLOR_STOPS. */
const COLORMAP: [number, number, number][] = Array.from({ length: 256 }, (_, value) => {
  const position = (value / 255) * (COLOR_STOPS.length - 1);
  const index = Math.min(Math.floor(position), COLOR_STOPS.length - 2);
  const t = position - index;
  const [from, to] = [COLOR_STOPS[index], COLOR_STOPS[index + 1]];
  return [0, 1, 2].map((channel) => Math.round(from[channel] + (to[channel] - from[channel]) * t)) as [
    number,
    number,
    number,
  ];
});

export function colorFor(value: number): [number, number, number] {
  return COLORMAP[Math.max(0, Math.min(255, Math.round(value)))];
}

export function decodeBase64(base64: string): Uint8Array {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }

  return bytes;
}

/**
 * RGBA pixels for a canvas `frames` wide and `bands` tall: time runs left to right and the
 * highest band is the top row, while the data itself is time-major with the lowest band first.
 */
export function spectrogramPixels(spectrogram: Spectrogram): Uint8ClampedArray {
  const { frames, bands } = spectrogram;
  const values = decodeBase64(spectrogram.values);
  const pixels = new Uint8ClampedArray(frames * bands * 4);

  for (let frame = 0; frame < frames; frame++) {
    for (let band = 0; band < bands; band++) {
      const [r, g, b] = colorFor(values[frame * bands + band] ?? 0);
      const offset = ((bands - 1 - band) * frames + frame) * 4;
      pixels[offset] = r;
      pixels[offset + 1] = g;
      pixels[offset + 2] = b;
      pixels[offset + 3] = 255;
    }
  }

  return pixels;
}

/**
 * A closed SVG path mirroring the peaks around the middle line, in a viewBox that is
 * `peaks.length` wide and 100 tall.
 */
export function waveformPath(peaks: number[]): string {
  if (peaks.length === 0) {
    return '';
  }

  const clamp = (peak: number) => Math.max(0, Math.min(1, peak));
  const top = peaks.map((peak, i) => `${i + 0.5},${round(50 - clamp(peak) * 50)}`);
  const bottom = peaks.map((peak, i) => `${i + 0.5},${round(50 + clamp(peak) * 50)}`).reverse();

  return `M${top.join('L')}L${bottom.join('L')}Z`;
}

/** Round frequencies on the spectrogram's log axis, each with its distance from the bottom as 0..1. */
export function frequencyTicks(minFrequency: number, maxFrequency: number): { hertz: number; position: number }[] {
  const span = Math.log(maxFrequency / minFrequency);

  return [100, 1000, 10000]
    .filter((hertz) => hertz > minFrequency && hertz < maxFrequency)
    .map((hertz) => ({ hertz, position: Math.log(hertz / minFrequency) / span }));
}

function round(value: number) {
  return Math.round(value * 100) / 100;
}
