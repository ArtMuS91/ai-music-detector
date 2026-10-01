import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import VerdictSummary from './VerdictSummary';
import { result, signal } from '../test/mockApi';

describe('VerdictSummary', () => {
  it.each([
    ['AiGenerated', 'AI-generated'],
    ['Human', 'Human-made'],
    ['Mixed', 'Mixed'],
    ['Inconclusive', 'Inconclusive'],
  ] as const)('headlines the %s verdict as "%s"', (verdict, label) => {
    render(<VerdictSummary result={result({ verdict })} />);

    expect(screen.getByRole('heading', { level: 2, name: label })).toBeInTheDocument();
  });

  it.each([
    [0.9, 'High confidence'],
    [0.5, 'Moderate confidence'],
    [0.1, 'Low confidence'],
  ])('describes confidence %s as "%s"', (confidence, words) => {
    render(<VerdictSummary result={result({ confidence })} />);

    expect(screen.getByText(words)).toBeInTheDocument();
  });

  it('shows confidence and AI likelihood as meters', () => {
    render(<VerdictSummary result={result({ confidence: 0.74, aiProbability: 0.912 })} />);

    const confidence = screen.getByRole('meter', { name: 'Confidence' });
    expect(confidence).toHaveAttribute('aria-valuenow', '74');
    expect(confidence).toHaveAttribute('aria-valuetext', '74%');
    expect(screen.getByRole('meter', { name: 'AI likelihood' })).toHaveAttribute('aria-valuenow', '91');
  });

  it('sorts the signals into columns by which way they lean, heaviest first', () => {
    render(
      <VerdictSummary
        result={result({
          signals: [
            signal({ name: 'Metadata clues', score: 0.9, weight: 0.6 }),
            signal({ name: 'Generator fingerprint', score: 0.99, weight: 0.8 }),
            signal({ name: 'Web research', score: 0.1, weight: 0.7 }),
            signal({ name: 'Lyrics', score: 0.5, weight: 0 }),
          ],
        })}
      />,
    );

    const names = (column: string) =>
      within(screen.getByRole('list', { name: column }))
        .getAllByRole('listitem')
        .map((item) => item.firstChild?.textContent);
    expect(names('Points to AI')).toEqual(['Generator fingerprint', 'Metadata clues']);
    expect(names('Points to human')).toEqual(['Web research']);
    expect(names('No clear lean')).toEqual(['Lyrics']);
    expect(within(screen.getByRole('list', { name: 'No clear lean' })).getByText('Found nothing')).toBeInTheDocument();
  });

  it('says "None" for a column no signal falls into', () => {
    render(<VerdictSummary result={result({ signals: [signal({ score: 0.99, weight: 0.8 })] })} />);

    expect(screen.queryByRole('list', { name: 'Points to human' })).not.toBeInTheDocument();
    expect(screen.getAllByText('None')).toHaveLength(2);
  });

  it('highlights the signals and figures the explanation mentions', () => {
    render(
      <VerdictSummary
        result={result({
          signals: [signal({ name: 'Generator fingerprint' }), signal({ name: 'Web research', score: 0.1 })],
          explanation: 'The Generator fingerprint (0.99) outweighed web research at 74% confidence.',
        })}
      />,
    );

    const explanation = screen.getByRole('heading', { name: 'Explanation' }).nextElementSibling as HTMLElement;
    expect(explanation).toHaveTextContent('The Generator fingerprint (0.99) outweighed web research at 74% confidence.');
    for (const highlighted of ['Generator fingerprint', 'web research', '0.99', '74%']) {
      expect(within(explanation).getByText(highlighted).tagName).toBe('STRONG');
    }
  });

  it('leaves out the explanation when there is none', () => {
    render(<VerdictSummary result={result({ explanation: null })} />);

    expect(screen.queryByRole('heading', { name: 'Explanation' })).not.toBeInTheDocument();
  });
});
