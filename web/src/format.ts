/** m:ss, e.g. 245 → "4:05". */
export function formatDuration(seconds: number) {
  const whole = Math.round(seconds);
  return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, '0')}`;
}

export function formatPercent(fraction: number) {
  return `${Math.round(fraction * 100)}%`;
}

export function formatFrequency(hertz: number) {
  return hertz >= 1000 ? `${hertz / 1000}k` : `${hertz}`;
}
