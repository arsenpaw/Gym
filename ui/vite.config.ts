/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react';
import { defineConfig, loadEnv } from 'vite';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');

  return {
    plugins: [react()],
    build: { chunkSizeWarningLimit: 1000 },
    server: {
      port: 5173,
      proxy: {
        '/api': { target: env.API_PROXY_TARGET ?? 'http://localhost:5080', changeOrigin: true },
      },
    },
    test: {
      environment: 'jsdom',
      globals: true,
      setupFiles: ['./src/test/setup.ts'],
      testTimeout: 20000,
      css: { include: [/calendarTheme\.css/] },
    },
  };
});
