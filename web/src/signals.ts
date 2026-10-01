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

export type TextPart = { text: string; kind?: 'signal' | 'number' };

/**
 * A standalone number, optionally a percentage: "0.8", "1,000", "99%", "99 %" (the model often
 * puts a narrow no-break space before %). Digits inside words ("gpt-oss-120b") are left alone.
 */
const NUMBER = /((?<![\p{L}\p{N}_.])\d+(?:[.,]\d+)?(?:[\s  ]?%)?(?![\p{L}\p{N}_%]))/gu;

/**
 * Splits free text (the AI-written explanation) into plain parts, mentions of the given signals'
 * names (matched case-insensitively, longest name first) and numbers, so both can be highlighted.
 */
export function highlightParts(text: string, signals: Signal[]): TextPart[] {
  return splitBySignalNames(text, signals).flatMap((part) =>
    part.kind
      ? [part]
      : part.text
          .split(NUMBER)
          // With a capturing split, the matched numbers are exactly the odd-indexed pieces.
          .map((piece, index): TextPart => (index % 2 === 1 ? { text: piece, kind: 'number' } : { text: piece }))
          .filter((piece) => piece.text !== ''),
  );
}

function splitBySignalNames(text: string, signals: Signal[]): TextPart[] {
  const names = [...new Set(signals.map((signal) => signal.name.trim().toLowerCase()).filter((name) => name !== ''))];
  if (names.length === 0) {
    return [{ text }];
  }

  const pattern = new RegExp(
    `(${names
      .sort((a, b) => b.length - a.length)
      .map(escapeRegExp)
      .join('|')})`,
    'gi',
  );

  return text
    .split(pattern)
    .filter((part) => part !== '')
    .map((part) => (names.includes(part.toLowerCase()) ? { text: part, kind: 'signal' } : { text: part }));
}

function escapeRegExp(value: string) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
