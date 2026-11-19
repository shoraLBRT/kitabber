// Style lint (E02-02): colours and sizes come only from the design tokens. The rules live in brand/ and are
// shared with the other app; this file only adds the .vue syntax and the ignored output folders.
import rules from '../brand/stylelint-rules.mjs'

export default {
  customSyntax: 'postcss-html',
  ignoreFiles: ['**/node_modules/**', '**/.nuxt/**', '**/.output/**', '**/dist/**', '**/coverage/**'],
  rules,
}
