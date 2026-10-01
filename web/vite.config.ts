import react from '@vitejs/plugin-react';
import { loadEnv } from 'vite';
import { defineConfig } from 'vitest/config';

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // Where the dev server forwards /api; same default as API_BASE_URL in src/api.ts.
  const apiProxyTarget = loadEnv(mode, process.cwd(), 'VITE_').VITE_API_PROXY_TARGET || 'http://localhost:5214';

  return {
    plugins: [react()],
    server: {
      // Lets the dev server be shared through a VS Code / dev tunnels forwarded port.
      allowedHosts: ['.devtunnels.ms'],
      // Same-origin API calls when VITE_API_BASE_URL is empty, so only port 5173 needs forwarding.
      proxy: {
        '/api': apiProxyTarget,
      },
    },
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
    },
  };
});
