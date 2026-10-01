import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Lets the dev server be shared through a VS Code / dev tunnels forwarded port.
    allowedHosts: ['.devtunnels.ms'],
    // Same-origin API calls when VITE_API_BASE_URL is empty, so only port 5173 needs forwarding.
    proxy: {
      '/api': 'http://localhost:5214',
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
});
