import Box from '@mui/material/Box';

/** The YouTube Music mark (red disc, ring, play triangle), drawn inline at text size. */
function YouTubeMusicIcon() {
  return (
    <Box component="svg" viewBox="0 0 24 24" aria-hidden sx={{ width: 24, height: 24, display: 'block' }}>
      <circle cx="12" cy="12" r="12" fill="#FF0000" />
      <circle cx="12" cy="12" r="6.6" fill="none" stroke="#FFFFFF" strokeWidth="1.1" />
      <path d="M9.9 8.9v6.2L15.2 12z" fill="#FFFFFF" />
    </Box>
  );
}

export default YouTubeMusicIcon;
