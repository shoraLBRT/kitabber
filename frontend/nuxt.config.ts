// https://nuxt.com/docs/api/configuration/nuxt-config
import { fileURLToPath } from 'node:url'
import brand from '../brand/brand.json'
import { faviconHref } from '../brand/favicon'

export default defineNuxtConfig({
  compatibilityDate: '2026-11-01',
  devtools: { enabled: false },
  modules: ['@nuxt/eslint'],
  typescript: {
    strict: true,
  },
  // Design tokens as CSS custom properties, generated from brand/ (ADR-0012).
  css: [fileURLToPath(new URL('../brand/tokens.css', import.meta.url))],
  app: {
    head: {
      htmlAttrs: { lang: 'ru' },
      title: brand.name,
      // viewport-fit=cover lets the phone shell lay out under the notch and keep clear of it through the safe-area
      // insets (E02-06).
      viewport: 'width=device-width, initial-scale=1, viewport-fit=cover',
      meta: [{ name: 'description', content: brand.tagline }],
      link: [{ rel: 'icon', type: 'image/svg+xml', href: faviconHref() }],
    },
  },
  // The API origin for plugins/api.ts: apiBase (NUXT_API_BASE) for server rendering, which reaches the API inside
  // the network; public.apiBase (NUXT_PUBLIC_API_BASE) for the browser, '' for the same origin behind the proxy.
  runtimeConfig: { apiBase: '', public: { apiBase: '' } },
  // brand/ (ADR-0011) lies outside the app; let the dev server read it.
  vite: { server: { fs: { allow: [fileURLToPath(new URL('../brand', import.meta.url))] } } },
})
