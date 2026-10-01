import Alert from '@mui/material/Alert';
import AlertTitle from '@mui/material/AlertTitle';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Divider from '@mui/material/Divider';
import LinearProgress from '@mui/material/LinearProgress';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { TERMINAL_STATUSES, type AnalysisJob, type AnalysisStatus } from '../models';
import { formatDuration } from '../format';
import AnalysisProgress from './AnalysisProgress';
import AudioVisuals from './AudioVisuals';
import SignalBreakdown from './SignalBreakdown';
import VerdictSummary from './VerdictSummary';

const STATUS_LABELS: Record<AnalysisStatus, string> = {
  Pending: 'Queued',
  Acquiring: 'Downloading audio',
  Preprocessing: 'Preprocessing audio',
  Analyzing: 'Running detectors',
  Completed: 'Done',
  Failed: 'Failed',
};

const FAILURES: Partial<Record<AnalysisStatus, { title: string; hint?: string }>> = {
  Acquiring: {
    title: 'Could not download the track',
    hint: 'Check that the link points to a public video that plays in your region, then try again.',
  },
  Preprocessing: { title: 'Could not process the audio' },
  Analyzing: { title: 'Analysis failed' },
};

function FailureAlert({ job }: { job: AnalysisJob }) {
  const failure = (job.failedStage && FAILURES[job.failedStage]) || { title: 'Analysis failed' };

  return (
    <Alert severity="error">
      <AlertTitle>{failure.title}</AlertTitle>
      {job.failureReason ?? 'The analysis failed for an unknown reason.'}
      {failure.hint && (
        <Typography variant="body2" sx={{ mt: 1 }}>
          {failure.hint}
        </Typography>
      )}
    </Alert>
  );
}

function JobCard({ job }: { job: AnalysisJob }) {
  const inProgress = !TERMINAL_STATUSES.includes(job.status);
  const track = job.track;

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={2}>
          <Typography variant="overline" color="text.secondary" aria-live="polite">
            {STATUS_LABELS[job.status]}
          </Typography>

          <AnalysisProgress status={job.status} failedStage={job.failedStage} />

          {inProgress && <LinearProgress aria-label="Analysis in progress" />}

          {track && (
            <Stack spacing={0.5}>
              <Typography variant="subtitle1">{track.title ?? 'Unknown title'}</Typography>
              <Typography variant="body2" color="text.secondary">
                {[
                  track.artist ?? track.channel,
                  track.durationSeconds === null ? null : formatDuration(track.durationSeconds),
                ]
                  .filter(Boolean)
                  .join(' · ')}
              </Typography>
            </Stack>
          )}

          {job.status === 'Completed' && (
            // A link analyzed before returns that stored result at once; the date says how fresh it is.
            <Typography variant="caption" color="text.secondary">
              {`Result from ${new Date(job.updatedAt).toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' })}`}
            </Typography>
          )}

          {job.status === 'Failed' && <FailureAlert job={job} />}

          {job.result && (
            <>
              <Divider />
              <VerdictSummary result={job.result} />
            </>
          )}

          {job.visualization && (
            <>
              <Divider />
              <AudioVisuals visualization={job.visualization} />
            </>
          )}

          {job.result && (
            <>
              <Divider />
              <SignalBreakdown signals={job.result.signals} />
            </>
          )}
        </Stack>
      </CardContent>
    </Card>
  );
}

export default JobCard;
