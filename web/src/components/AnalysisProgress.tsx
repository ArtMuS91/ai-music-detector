import Step from '@mui/material/Step';
import StepLabel from '@mui/material/StepLabel';
import Stepper from '@mui/material/Stepper';
import type { AnalysisStatus } from '../models';

const STEPS: { status: AnalysisStatus; label: string }[] = [
  { status: 'Pending', label: 'Queue' },
  { status: 'Acquiring', label: 'Download' },
  { status: 'Preprocessing', label: 'Prepare' },
  { status: 'Analyzing', label: 'Analyze' },
  { status: 'Completed', label: 'Result' },
];

type AnalysisProgressProps = {
  status: AnalysisStatus;
  /** For a Failed job: the stage it failed in, if known. */
  failedStage: AnalysisStatus | null;
};

/** Where a job is in the pipeline; a failed job marks the stage it failed in. */
function AnalysisProgress({ status, failedStage }: AnalysisProgressProps) {
  const current = status === 'Failed' ? failedStage : status;
  const activeStep = STEPS.findIndex((step) => step.status === current);

  if (activeStep === -1) {
    return null;
  }

  const done = status === 'Completed';

  return (
    <Stepper
      activeStep={done ? STEPS.length : activeStep}
      alternativeLabel
      aria-label="Analysis progress"
      // Five labels share a phone-width row; the default size runs neighbours together.
      sx={{ '& .MuiStepLabel-label': { fontSize: { xs: '0.75rem', sm: '0.875rem' } } }}
    >
      {STEPS.map((step, index) => (
        <Step key={step.status}>
          <StepLabel error={status === 'Failed' && index === activeStep}>{step.label}</StepLabel>
        </Step>
      ))}
    </Stepper>
  );
}

export default AnalysisProgress;
