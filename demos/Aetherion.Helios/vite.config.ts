import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import { aetherisCad } from '@aetheris/cad/vite';

export default defineConfig({
  plugins: [aetherisCad(), react()],
  server: { port: 4173 },
  test: { environment: 'jsdom', setupFiles: './src/test/setup.ts', css: true }
});
