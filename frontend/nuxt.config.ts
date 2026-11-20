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
  // Sign-in (E18-02, ADR-0018): the provider and this client (NUXT_OIDC_ISSUER, NUXT_OIDC_CLIENT_ID,
  // NUXT_OIDC_REDIRECT_URI) and the key of the session cookies (NUXT_SESSION_PASSWORD); server-side only.
  runtimeConfig: {
    apiBase: '',
    oidc: { issuer: '', clientId: 'web', redirectUri: '' },
    sessionPassword: '',
    public: { apiBase: '' },
  },
  // `nuxt dev` signs in at the API of `dotnet run` (backend/src/Api, launchSettings.json); its key is made at start.
  // The realtime hub (E01-11) is reached on the same origin, as behind the stand's proxy: the dev server forwards it.
  $development: {
    runtimeConfig: {
      apiBase: 'http://localhost:5103',
      oidc: { issuer: 'http://localhost:5103/', redirectUri: 'http://localhost:3000/auth/callback' },
    },
    nitro: { devProxy: { '/hubs': { target: 'http://localhost:5103/hubs', ws: true } } },
  },
  // brand/ (ADR-0011) lies outside the app; let the dev server read it.
  vite: { server: { fs: { allow: [fileURLToPath(new URL('../brand', import.meta.url))] } } },
})
