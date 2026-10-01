import { describe, expect, it } from 'vitest';
import { colorFor, decodeBase64, frequencyTicks, spectrogramPixels, waveformPath } from './visualization';

describe('decodeBase64', () => {
  it('decodes bytes across the full range', () => {
    expect(Array.from(decodeBase64('AAH//g=='))).toEqual([0, 1, 255, 254]);
  });
});

describe('colorFor', () => {
  it('runs from near black for silence to bright for the loudest cell', () => {
    expect(colorFor(0)).toEqual([0, 0, 4]);
    expect(colorFor(255)).toEqual([252, 255, 164]);
  });

  it('clamps out-of-range values', () => {
    expect(colorFor(-5)).toEqual(colorFor(0));
    expect(colorFor(300)).toEqual(colorFor(255));
  });
});

describe('spectrogramPixels', () => {
  it('puts the highest band on the top row', () => {
    // Two frames of two bands: frame 0 = [low 0, high 255], frame 1 = [low 255, high 0].
    const pixels = spectrogramPixels({
      frames: 2,
      bands: 2,
      minFrequency: 30,
      maxFrequency: 22050,
      minDecibels: -80,
      values: btoa(String.fromCharCode(0, 255, 255, 0)),
    });

    const pixel = (x: number, y: number) => Array.from(pixels.slice((y * 2 + x) * 4, (y * 2 + x) * 4 + 4));
    expect(pixel(0, 0)).toEqual([...colorFor(255), 255]);
    expect(pixel(0, 1)).toEqual([...colorFor(0), 255]);
    expect(pixel(1, 0)).toEqual([...colorFor(0), 255]);
    expect(pixel(1, 1)).toEqual([...colorFor(255), 255]);
  });
});

describe('waveformPath', () => {
  it('mirrors each peak around the middle line', () => {
    expect(waveformPath([0, 1])).toBe('M0.5,50L1.5,0L1.5,100L0.5,50Z');
  });

  it('is empty without peaks', () => {
    expect(waveformPath([])).toBe('');
  });
});

describe('frequencyTicks', () => {
  it('places decade ticks on a log scale within the range', () => {
    const ticks = frequencyTicks(10, 100000);

    expect(ticks.map((tick) => tick.hertz)).toEqual([100, 1000, 10000]);
    expect(ticks.map((tick) => tick.position)).toEqual([0.25, 0.5, 0.75].map((p) => expect.closeTo(p)));
  });

  it('leaves out ticks outside the range', () => {
    expect(frequencyTicks(30, 8000).map((tick) => tick.hertz)).toEqual([100, 1000]);
  });
});
