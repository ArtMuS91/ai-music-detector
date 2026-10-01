import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import { useColorScheme } from '@mui/material/styles';
import DarkModeOutlined from '@mui/icons-material/DarkModeOutlined';
import LightModeOutlined from '@mui/icons-material/LightModeOutlined';

/**
 * Switches between the light and dark scheme. Until it is first used the page follows the OS
 * setting; after that MUI remembers the choice in localStorage.
 */
export default function ThemeToggle() {
  const { mode, systemMode, setMode } = useColorScheme();

  // No mode means there is no color-scheme provider above (e.g. a component rendered on its own).
  if (!mode) {
    return null;
  }

  const current = mode === 'system' ? systemMode : mode;
  const next = current === 'dark' ? 'light' : 'dark';
  const label = `Switch to ${next} theme`;

  return (
    <Tooltip title={label}>
      <IconButton aria-label={label} onClick={() => setMode(next)}>
        {next === 'dark' ? <DarkModeOutlined /> : <LightModeOutlined />}
      </IconButton>
    </Tooltip>
  );
}
