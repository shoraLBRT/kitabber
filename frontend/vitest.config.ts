import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vitest/config'

// Unit and component tests (Vitest); end-to-end tests are in e2e/ (playwright.config.ts, E01-16).
export default defineConfig({
  plugins: [vue()],
  // The pictures of reactions are imported from ../brand/reactions (ADR-0095), outside the app.
  server: { fs: { allow: ['..'] } },
  test: {
    environment: 'happy-dom',
    include: ['tests/**/*.spec.ts'],
  },
})
