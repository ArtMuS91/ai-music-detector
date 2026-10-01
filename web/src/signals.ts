import type { Signal } from './models';

/** Which way a signal points: toward AI, toward human, too close to call, or no evidence at all. */
export type Lean = 'ai' | 'human' | 'undecided' | 'none';

/** Within this distance of 0.5 a score is too close to call either way. */
const UNDECIDED_MARGIN = 0.1;

export function leanOf(signal: Signal): Lean {
  if (signal.weight === 0) {
    return 'none';
  }

  if (signal.score >= 0.5 + UNDECIDED_MARGIN) {
    return 'ai';
  }

  if (signal.score <= 0.5 - UNDECIDED_MARGIN) {
    return 'human';
  }

  return 'undecided';
}

export type TextPart = { text: string; signal?: Signal };

/**
 * Splits free text (the AI-written explanation) into plain parts and mentions of the given
 * signals' names, matched case-insensitively and longest name first, so they can be highlighted.
 */
export function splitBySignalNames(text: string, signals: Signal[]): TextPart[] {
  const named = signals.filter((signal) => signal.name.trim() !== '');
  if (named.length === 0) {
    return [{ text }];
  }

  const byName = new Map(named.map((signal) => [signal.name.toLowerCase(), signal]));
  const pattern = new RegExp(
    `(${[...byName.keys()]
      .sort((a, b) => b.length - a.length)
      .map(escapeRegExp)
      .join('|')})`,
    'gi',
  );

  return text
    .split(pattern)
    .filter((part) => part !== '')
    .map((part) => {
      const signal = byName.get(part.toLowerCase());
      return signal ? { text: part, signal } : { text: part };
    });
}

function escapeRegExp(value: string) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
