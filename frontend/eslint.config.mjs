import prettier from 'eslint-config-prettier'
import withNuxt from './.nuxt/eslint.config.mjs'
import i18n from './eslint/i18n.mjs'

export default withNuxt(prettier, {
  // Interface strings only from the dictionary (E02-10, ADR-0063). Left out: the dictionaries themselves; the tests,
  // which read the texts the person sees; the developer showcases of the design system (/ui, /tokens, /brand,
  // /reactions), whose sample texts are not the product's interface; and the server routes, whose error messages are
  // never shown (the error page draws its own texts from the dictionary).
  files: ['**/*.{ts,mts,vue}'],
  ignores: [
    'i18n/locales/**',
    'tests/**',
    'e2e/**',
    'server/**',
    'pages/ui.vue',
    'pages/tokens.vue',
    'pages/brand.vue',
    'pages/reactions.vue',
    'components/UiShowcase.vue',
    'components/TokenShowcase.vue',
  ],
  plugins: { i18n },
  rules: { 'i18n/no-raw-text': 'error' },
})
