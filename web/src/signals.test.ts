import { describe, expect, it } from 'vitest';
import { highlightParts, leanOf } from './signals';
import { signal } from './test/mockApi';

describe('leanOf', () => {
  it.each([
    [0.95, 0.8, 'ai'],
    [0.05, 0.8, 'human'],
    [0.55, 0.8, 'undecided'],
    [0.95, 0, 'none'],
  ] as const)('reads score %s at weight %s as %s', (score, weight, lean) => {
    expect(leanOf(signal({ score, weight }))).toBe(lean);
  });
});

describe('highlightParts', () => {
  const signals = [signal({ name: 'Generator fingerprint' }), signal({ name: 'Web research' })];

  it('marks each mention of a signal, whatever its case', () => {
    expect(highlightParts('The generator fingerprint agreed with Web research.', signals)).toEqual([
      { text: 'The ' },
      { text: 'generator fingerprint', kind: 'signal' },
      { text: ' agreed with ' },
      { text: 'Web research', kind: 'signal' },
      { text: '.' },
    ]);
  });

  it('prefers the longest name when one contains another', () => {
    const parts = highlightParts('Web research v2 found it.', [signal({ name: 'Web research' }), signal({ name: 'Web research v2' })]);

    expect(parts[0]).toEqual({ text: 'Web research v2', kind: 'signal' });
  });

  it('treats regex characters in names literally', () => {
    expect(highlightParts('Detector (beta) said so.', [signal({ name: 'Detector (beta)' })])[0]).toEqual({
      text: 'Detector (beta)',
      kind: 'signal',
    });
  });

  it('marks numbers and percentages, including a spaced percent sign', () => {
    const numbers = highlightParts('A score of 0.8, a likelihood of 99 % and 98% confidence, in 1987.', [])
      .filter((part) => part.kind === 'number')
      .map((part) => part.text);

    expect(numbers).toEqual(['0.8', '99 %', '98%', '1987']);
  });

  it('leaves digits inside words alone', () => {
    const parts = highlightParts('Made with gpt-oss-120b and 3D audio, model v2.', []);

    expect(parts.filter((part) => part.kind === 'number')).toEqual([]);
    expect(parts.map((part) => part.text).join('')).toBe('Made with gpt-oss-120b and 3D audio, model v2.');
  });

  it('marks numbers next to signal names without breaking either', () => {
    expect(highlightParts('Web research scored 0.9.', signals)).toEqual([
      { text: 'Web research', kind: 'signal' },
      { text: ' scored ' },
      { text: '0.9', kind: 'number' },
      { text: '.' },
    ]);
  });

  it('returns the text whole when there is nothing to mark', () => {
    expect(highlightParts('Nothing to see.', signals)).toEqual([{ text: 'Nothing to see.' }]);
  });
});
