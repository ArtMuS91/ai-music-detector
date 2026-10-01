import { describe, expect, it } from 'vitest';
import { leanOf, splitBySignalNames } from './signals';
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

describe('splitBySignalNames', () => {
  const fingerprint = signal({ name: 'Generator fingerprint' });
  const research = signal({ name: 'Web research' });

  it('marks each mention of a signal, whatever its case', () => {
    const parts = splitBySignalNames('The generator fingerprint agreed with Web research.', [fingerprint, research]);

    expect(parts).toEqual([
      { text: 'The ' },
      { text: 'generator fingerprint', signal: fingerprint },
      { text: ' agreed with ' },
      { text: 'Web research', signal: research },
      { text: '.' },
    ]);
  });

  it('prefers the longest name when one contains another', () => {
    const research2 = signal({ name: 'Web research v2' });

    const parts = splitBySignalNames('Web research v2 found it.', [research, research2]);

    expect(parts[0]).toEqual({ text: 'Web research v2', signal: research2 });
  });

  it('treats regex characters in names literally', () => {
    const odd = signal({ name: 'Detector (beta)' });

    expect(splitBySignalNames('Detector (beta) said so.', [odd])[0]).toEqual({ text: 'Detector (beta)', signal: odd });
  });

  it('returns the text whole when no signal is mentioned', () => {
    expect(splitBySignalNames('Nothing to see.', [fingerprint])).toEqual([{ text: 'Nothing to see.' }]);
  });
});
