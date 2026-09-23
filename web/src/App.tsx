import { useEffect, useRef, useState } from 'react';
import Container from '@mui/material/Container';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import Chip from '@mui/material/Chip';
import Alert from '@mui/material/Alert';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import LinearProgress from '@mui/material/LinearProgress';
import { checkHealth, getAnalysis, submitAnalysis } from './api';
import { TERMINAL_STATUSES, type AnalysisJob } from './models';

const POLL_INTERVAL_MS = 1500;

const STATUS_LABELS: Record<AnalysisJob['status'], string> = {
  Pending: 'Queued',
  Acquiring: 'Downloading audio',
  Preprocessing: 'Audio acquired — analysis not implemented yet',
  Analyzing: 'Analyzing',
  Completed: 'Done',
  Failed: 'Failed',
};

type ApiStatus = 'checking' | 'online' | 'offline';

function formatDuration(seconds: number | null) {
  if (seconds === null) {
    return null;
  }

  const minutes = Math.floor(seconds / 60);
  return `${minutes}:${String(Math.round(seconds % 60)).padStart(2, '0')}`;
}

function App() {
  const [url, setUrl] = useState('');
  const [apiStatus, setApiStatus] = useState<ApiStatus>('checking');
  const [job, setJob] = useState<AnalysisJob | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const jobIdRef = useRef<string | null>(null);

  const isOnline = apiStatus === 'online';

  useEffect(() => {
    checkHealth().then((ok) => setApiStatus(ok ? 'online' : 'offline'));
  }, []);

  useEffect(() => {
    if (!job || TERMINAL_STATUSES.includes(job.status)) {
      return;
    }

    const timer = setInterval(async () => {
      try {
        const next = await getAnalysis(job.id);
        // A newer submission may have landed while this request was in flight.
        if (jobIdRef.current === next.id) {
          setJob(next);
        }
      } catch (e) {
        setError(e instanceof Error ? e.message : String(e));
      }
    }, POLL_INTERVAL_MS);

    return () => clearInterval(timer);
  }, [job]);

  async function handleSubmit() {
    setSubmitting(true);
    setError(null);
    setJob(null);

    try {
      const created = await submitAnalysis(url);
      jobIdRef.current = created.id;
      setJob(created);
    } catch (e) {
      jobIdRef.current = null;
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setSubmitting(false);
    }
  }

  const inProgress = job !== null && !TERMINAL_STATUSES.includes(job.status);
  const canSubmit = url !== '' && !submitting && isOnline;

  return (
    <Container maxWidth="sm" sx={{ py: 8 }}>
      <Stack spacing={3}>
        <Stack spacing={1}>
          <Typography variant="h4" component="h1">
            AI Music Detector
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Paste a YouTube / YouTube Music link to analyze the track.
          </Typography>
        </Stack>

        <TextField
          label="YouTube URL"
          placeholder="https://music.youtube.com/watch?v="
          value={url}
          onChange={(e) => setUrl(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && canSubmit) {
              handleSubmit();
            }
          }}
          fullWidth
        />
        <Button variant="contained" disabled={!canSubmit} onClick={handleSubmit}>
          Analyze
        </Button>

        {error && <Alert severity="error">{error}</Alert>}

        {job && (
          <Card variant="outlined">
            <CardContent>
              <Stack spacing={1.5}>
                <Typography variant="overline" color="text.secondary">
                  {STATUS_LABELS[job.status]}
                </Typography>

                {inProgress && <LinearProgress />}

                {job.track && (
                  <Stack spacing={0.5}>
                    <Typography variant="subtitle1">{job.track.title ?? 'Unknown title'}</Typography>
                    <Typography variant="body2" color="text.secondary">
                      {[job.track.artist ?? job.track.channel, formatDuration(job.track.durationSeconds)]
                        .filter(Boolean)
                        .join(' · ')}
                    </Typography>
                  </Stack>
                )}

                {job.status === 'Failed' && job.failureReason && (
                  <Alert severity="error">{job.failureReason}</Alert>
                )}
              </Stack>
            </CardContent>
          </Card>
        )}

        <Chip
          label={`API: ${apiStatus}`}
          color={apiStatus === 'online' ? 'success' : apiStatus === 'offline' ? 'error' : 'default'}
          sx={{ alignSelf: 'flex-start' }}
        />
      </Stack>
    </Container>
  );
}

export default App;
