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
      meta: [{ name: 'description', content: brand.tagline }],
      link: [{ rel: 'icon', type: 'image/svg+xml', href: faviconHref() }],
    },
  },
  // brand/ (ADR-0011) lies outside the app; let the dev server read it.
  vite: { server: { fs: { allow: [fileURLToPath(new URL('../brand', import.meta.url))] } } },
})
