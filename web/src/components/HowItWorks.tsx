import type { ReactElement } from 'react';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Divider from '@mui/material/Divider';
import Link from '@mui/material/Link';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import CollapsibleSection from './CollapsibleSection';
import { signalNameSx } from '../theme';
import DownloadOutlined from '@mui/icons-material/DownloadOutlined';
import GraphicEqOutlined from '@mui/icons-material/GraphicEqOutlined';
import ManageSearchOutlined from '@mui/icons-material/ManageSearchOutlined';
import GavelOutlined from '@mui/icons-material/GavelOutlined';
import DeleteOutlined from '@mui/icons-material/DeleteOutlined';

const STEPS: { icon: ReactElement; title: string; text: string; listsSignals?: boolean }[] = [
  {
    icon: <DownloadOutlined fontSize="small" />,
    title: 'Download',
    text: 'Only the audio stream is fetched from YouTube.',
  },
  {
    icon: <GraphicEqOutlined fontSize="small" />,
    title: 'Prepare',
    text: 'A 3-minute excerpt from the middle of the track is converted to mono.',
  },
  {
    icon: <ManageSearchOutlined fontSize="small" />,
    title: 'Detect',
    text: 'Independent signals each look for evidence:',
    listsSignals: true,
  },
  {
    icon: <GavelOutlined fontSize="small" />,
    title: 'Decide',
    text: 'Fixed rules weigh the signals into a verdict. An AI model then explains it, using the transcribed lyrics as context.',
  },
];

/**
 * What each stage runs on. Kept by hand: the model ids mirror the `Groq` section of the API's
 * appsettings.json and the fakeprint weights vendored in ml/app/models, so update both together.
 */
const SOURCES: { name: string; use: string; href: string }[] = [
  { name: 'yt-dlp', use: 'audio download', href: 'https://github.com/yt-dlp/yt-dlp' },
  { name: 'FFmpeg', use: 'decoding and the excerpt', href: 'https://ffmpeg.org' },
  {
    name: 'lofcz/ai-music-detector',
    use: 'fakeprint model on Hugging Face, after Afchar et al. (ISMIR 2025)',
    href: 'https://huggingface.co/lofcz/ai-music-detector',
  },
  { name: 'openai/gpt-oss-120b on Groq', use: 'web research and the explanation', href: 'https://console.groq.com/docs/models' },
  { name: 'whisper-large-v3-turbo on Groq', use: 'lyrics transcription', href: 'https://console.groq.com/docs/speech-to-text' },
];

const SIGNALS: { name: string; text: string }[] = [
  { name: 'Generator fingerprint', text: 'traces that AI music generators leave in the audio' },
  { name: 'Web research', text: 'reports about the track or artist online' },
  { name: 'Metadata clues', text: 'AI mentions in the title, tags or description, and the upload date' },
];

/** A short, static explainer of the pipeline and what happens to the audio. */
function HowItWorks() {
  return (
    <Card variant="outlined" component="aside" aria-labelledby="how-it-works-title">
      <CardContent>
        <CollapsibleSection
          title="How it works"
          component="h2"
          variant="h6"
          headingId="how-it-works-title"
          storageKey="aimd.howItWorks.expanded"
        >
          <Stack spacing={2} sx={{ pt: 1.5 }}>
            <Stack component="ol" spacing={1.5} sx={{ listStyle: 'none', p: 0, m: 0 }}>
              {STEPS.map((step) => (
                <Stack component="li" key={step.title} direction="row" spacing={1.5}>
                  <Box sx={{ color: 'primary.main', display: 'flex', pt: 0.25 }} aria-hidden>
                    {step.icon}
                  </Box>
                  <Box>
                    <Typography variant="subtitle2" component="h3">
                      {step.title}
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                      {step.text}
                    </Typography>
                    {step.listsSignals && (
                      <Box component="ul" sx={{ pl: 2, my: 0.5 }}>
                        {SIGNALS.map((signal) => (
                          <Typography component="li" variant="body2" color="text.secondary" key={signal.name}>
                            <Box component="strong" sx={signalNameSx}>
                              {signal.name}
                            </Box>
                            {` — ${signal.text}`}
                          </Typography>
                        ))}
                      </Box>
                    )}
                  </Box>
                </Stack>
              ))}
            </Stack>

            <Divider />

            <Stack spacing={1}>
              <Typography variant="subtitle2" component="h3">
                Built with
              </Typography>
              <Stack component="ul" spacing={0.75} sx={{ listStyle: 'none', p: 0, m: 0 }}>
                {SOURCES.map((source) => (
                  <Typography component="li" variant="body2" color="text.secondary" key={source.name}>
                    <Link href={source.href} target="_blank" rel="noopener noreferrer" sx={{ fontWeight: 600 }}>
                      {source.name}
                    </Link>
                    {` — ${source.use}`}
                  </Typography>
                ))}
              </Stack>
            </Stack>

            <Divider />

            <Stack direction="row" spacing={1.5}>
              <Box sx={{ color: 'text.secondary', display: 'flex', pt: 0.25 }} aria-hidden>
                <DeleteOutlined fontSize="small" />
              </Box>
              <Typography variant="body2" color="text.secondary">
                The audio is deleted as soon as the analysis finishes. Only the results are kept.
              </Typography>
            </Stack>

            <Typography variant="caption" color="text.secondary">
              Every signal can be wrong, so treat the verdict as an estimate rather than proof.
            </Typography>
          </Stack>
        </CollapsibleSection>
      </CardContent>
    </Card>
  );
}

export default HowItWorks;
