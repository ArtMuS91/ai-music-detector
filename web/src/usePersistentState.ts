import { useState } from 'react';

/**
 * Like useState, but remembered in localStorage under `key`, for per-viewer preferences such as
 * which sections are collapsed; with no key it is plain useState. Storage can be unavailable
 * (private windows, blocked site data), so every access falls back quietly to the in-memory value.
 */
export function usePersistentState<T>(key: string | undefined, initial: T): [T, (value: T) => void] {
  const [value, setValue] = useState<T>(() => {
    if (key === undefined) {
      return initial;
    }

    try {
      const stored = window.localStorage.getItem(key);
      return stored === null ? initial : (JSON.parse(stored) as T);
    } catch {
      return initial;
    }
  });

  function update(next: T) {
    setValue(next);
    if (key === undefined) {
      return;
    }

    try {
      window.localStorage.setItem(key, JSON.stringify(next));
    } catch {
      // Not remembered across reloads; the toggle still works for this visit.
    }
  }

  return [value, update];
}
