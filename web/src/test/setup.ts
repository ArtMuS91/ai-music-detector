import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';

// jsdom has no canvas and logs "not implemented" for getContext; components treat null as "can't draw".
HTMLCanvasElement.prototype.getContext = (() => null) as typeof HTMLCanvasElement.prototype.getContext;

/** Minimal Web Storage kept in memory. */
class MemoryStorage implements Storage {
  private items = new Map<string, string>();

  get length() {
    return this.items.size;
  }

  clear() {
    this.items.clear();
  }

  getItem(key: string) {
    return this.items.get(key) ?? null;
  }

  key(index: number) {
    return [...this.items.keys()][index] ?? null;
  }

  removeItem(key: string) {
    this.items.delete(key);
  }

  setItem(key: string, value: string) {
    this.items.set(key, String(value));
  }
}

// Node 25 ships an experimental global localStorage that, without --localstorage-file, has no
// working methods and shadows jsdom's; tests get a real in-memory one instead.
Object.defineProperty(window, 'localStorage', { value: new MemoryStorage(), configurable: true });

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.useRealTimers();
  // The app writes the analyzed link into the address; jsdom keeps it between tests otherwise.
  window.history.replaceState(null, '', '/');
  // Collapsed sections are remembered per viewer; one test's choice must not leak into the next.
  window.localStorage.clear();
});
