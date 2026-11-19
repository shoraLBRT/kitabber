import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vitest/config'

// Unit and component tests (Vitest). #17 extends this setup with Playwright e2e.
export default defineConfig({
  plugins: [vue()],
  test: {
    environment: 'happy-dom',
    include: ['tests/**/*.spec.ts'],
  },
})
