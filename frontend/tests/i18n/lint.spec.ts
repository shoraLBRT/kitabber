import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { ESLint } from 'eslint'
import { beforeAll, describe, expect, it } from 'vitest'

// The lint check of the interface strings (E02-10, ADR-0063, eslint/i18n.mjs) with the app's own config: a text in a
// template or a Cyrillic string in the code fails `pnpm lint`, a text from the dictionary passes.
const eslint = new ESLint({ cwd: join(dirname(fileURLToPath(import.meta.url)), '../..') })

async function rawTexts(code: string, filePath = 'components/Sample.vue'): Promise<string[]> {
  const [result] = await eslint.lintText(code, { filePath })
  return result!.messages.filter((message) => message.ruleId === 'i18n/no-raw-text').map((m) => m.message)
}

const component = (template: string, script = '') =>
  `<script setup lang="ts">\nimport { t } from '../i18n'\n${script}\n</script>\n\n<template>\n  ${template}\n</template>\n`

// The first lint loads the config and the parsers of the app: seconds on a busy machine.
describe('i18n/no-raw-text', { timeout: 30_000 }, () => {
  beforeAll(() => rawTexts(component('')), 120_000)

  it('reports text between the tags, in any language', async () => {
    expect(await rawTexts(component('<p>Привет</p>'))).toHaveLength(1)
    expect(await rawTexts(component('<p>Hello</p>'))).toHaveLength(1)
  })

  it('reports a text attribute and a Cyrillic attribute', async () => {
    expect(await rawTexts(component('<input placeholder="Search" />'))).toHaveLength(1)
    expect(await rawTexts(component('<button aria-label="Закрыть" />'))).toHaveLength(1)
    expect(await rawTexts(component('<UiState hint="Пусто" />'))).toHaveLength(1)
  })

  it('reports a Cyrillic string in the script and in a template expression', async () => {
    expect(await rawTexts(component('<p>{{ label }}</p>', "const label = 'Сохранить'"))).toHaveLength(1)
    expect(
      await rawTexts(component("<p>{{ ok ? 'Да' : t('common.retry') }}</p>", 'const ok = true')),
    ).toHaveLength(1)
    expect(await rawTexts('export const label = `Всего: ${1}`\n', 'utils/sample.ts')).toHaveLength(1)
  })

  it('passes texts from the dictionary, symbols and non-text attributes', async () => {
    const template = [
      '<section class="x" data-testid="feed" aria-labelledby="t">',
      '<h1 id="t">{{ t(\'nav.new\') }}</h1>',
      '<span aria-hidden="true"> · </span>',
      '<input :placeholder="t(\'editor.title\')" type="text" />',
      '</section>',
    ].join('')
    expect(await rawTexts(component(template))).toEqual([])
  })

  it('leaves out the dictionaries and the tests', async () => {
    expect(await rawTexts("export default { a: 'Привет' }\n", 'i18n/locales/xx.ts')).toEqual([])
    expect(await rawTexts("export const name = 'Анна'\n", 'tests/sample.spec.ts')).toEqual([])
  })
})
