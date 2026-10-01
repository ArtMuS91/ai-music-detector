import { useEffect, useRef, useState } from 'react';
import Box from '@mui/material/Box';
import Container from '@mui/material/Container';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import Alert from '@mui/material/Alert';
import Grid from '@mui/material/Grid';
import { getAnalysis, InvalidUrlError, JobNotFoundError, submitAnalysis } from './api';
import { TERMINAL_STATUSES, type AnalysisJob } from './models';
import HowItWorks from './components/HowItWorks';
import JobCard from './components/JobCard';

const POLL_INTERVAL_MS = 1500;

function messageOf(e: unknown) {
  return e instanceof Error ? e.message : String(e);
}

function App() {
  const [url, setUrl] = useState('');
  const [job, setJob] = useState<AnalysisJob | null>(null);
  const [urlError, setUrlError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  // A failed poll is usually transient, so it is shown as a warning and polling carries on.
  const [pollError, setPollError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const jobIdRef = useRef<string | null>(null);

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
          setPollError(null);
        }
      } catch (e) {
        if (jobIdRef.current !== job.id) {
          return;
        }

        if (e instanceof JobNotFoundError) {
          jobIdRef.current = null;
          setJob(null);
          setPollError(null);
          setError(e.message);
        } else {
          setPollError(`Could not refresh the analysis (${messageOf(e)}). Retrying…`);
        }
      }
    }, POLL_INTERVAL_MS);

    return () => clearInterval(timer);
  }, [job]);

  async function handleSubmit() {
    setSubmitting(true);
    setUrlError(null);
    setError(null);
    setPollError(null);
    setJob(null);

    try {
      const created = await submitAnalysis(url);
      jobIdRef.current = created.id;
      setJob(created);
    } catch (e) {
      jobIdRef.current = null;
      if (e instanceof InvalidUrlError) {
        setUrlError(e.message);
      } else {
        setError(messageOf(e));
      }
    } finally {
      setSubmitting(false);
    }
  }

  const canSubmit = url !== '' && !submitting;

  return (
    <Container maxWidth="lg" sx={{ py: { xs: 4, md: 8 } }}>
      <Grid container spacing={4}>
        <Grid size={{ xs: 12, md: 8 }}>
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
              onChange={(e) => {
                setUrl(e.target.value);
                setUrlError(null);
              }}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && canSubmit) {
                  handleSubmit();
                }
              }}
              error={urlError !== null}
              helperText={urlError}
              fullWidth
            />
            <Button variant="contained" disabled={!canSubmit} onClick={handleSubmit}>
              Analyze
            </Button>

            {error && <Alert severity="error">{error}</Alert>}

            {pollError && <Alert severity="warning">{pollError}</Alert>}

            {job && <JobCard job={job} />}

          </Stack>
        </Grid>
        <Grid size={{ xs: 12, md: 4 }}>
          {/* Stays in view beside a long result on wide screens; drops below the form on narrow ones. */}
          <Box sx={{ position: { md: 'sticky' }, top: { md: 32 } }}>
            <HowItWorks />
          </Box>
        </Grid>
      </Grid>
    </Container>
  );
}

export default App;
