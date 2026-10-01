import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import AnalysisProgress from './AnalysisProgress';

describe('AnalysisProgress', () => {
  it('shows every stage of the pipeline', () => {
    render(<AnalysisProgress status="Preprocessing" failedStage={null} />);

    for (const label of ['Queue', 'Download', 'Preprocess', 'Analyze', 'Result']) {
      expect(screen.getByText(label)).toBeInTheDocument();
    }
  });

  it('marks the stage a failed job failed in', () => {
    render(<AnalysisProgress status="Failed" failedStage="Acquiring" />);

    expect(screen.getByText('Download')).toHaveClass('Mui-error');
    expect(screen.getByText('Queue')).not.toHaveClass('Mui-error');
  });

  it('is left out for a failure in an unknown stage', () => {
    const { container } = render(<AnalysisProgress status="Failed" failedStage={null} />);

    expect(container).toBeEmptyDOMElement();
  });
});
