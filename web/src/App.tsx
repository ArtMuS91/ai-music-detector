import { useEffect, useState } from 'react'
import Container from '@mui/material/Container'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Button from '@mui/material/Button'
import Typography from '@mui/material/Typography'
import Chip from '@mui/material/Chip'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5214'

type ApiStatus = 'checking' | 'online' | 'offline'

function App() {
  const [url, setUrl] = useState('')
  const [apiStatus, setApiStatus] = useState<ApiStatus>('checking')

  useEffect(() => {
    fetch(`${API_BASE_URL}/health`)
      .then((res) => setApiStatus(res.ok ? 'online' : 'offline'))
      .catch(() => setApiStatus('offline'))
  }, [])

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
          placeholder="https://www.youtube.com/watch?v=..."
          value={url}
          onChange={(e) => setUrl(e.target.value)}
          fullWidth
        />
        <Button variant="contained" disabled={!url}>
          Analyze
        </Button>

        <Chip
          label={`API: ${apiStatus}`}
          color={apiStatus === 'online' ? 'success' : apiStatus === 'offline' ? 'error' : 'default'}
          sx={{ alignSelf: 'flex-start' }}
        />
      </Stack>
    </Container>
  )
}

export default App
