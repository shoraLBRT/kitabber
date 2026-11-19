// Acceptance checks of E02-02 (#12): the design tokens are generated from one source, text meets WCAG AA in both
// palettes, the style linter keeps raw colours and sizes out of components, and components use only known tokens.
import { readdirSync, readFileSync } from 'node:fs'
import { dirname, extname, join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import stylelint from 'stylelint'
import { describe, expect, it } from 'vitest'
import rules from '../../brand/stylelint-rules.mjs'
import { colors, scales, themes, tokensCss } from '../../brand/tokens'
import { contrast } from './color'

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..')
const apps = ['frontend', 'admin']
const skipped = new Set(['node_modules', '.nuxt', '.output', 'dist', 'coverage', 'tests'])

function styleFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    if (skipped.has(entry.name)) return []
    const path = join(dir, entry.name)
    if (entry.isDirectory()) return styleFiles(path)
    return ['.vue', '.css'].includes(extname(path)) ? [path] : []
  })
}

const lint = async (code: string) => (await stylelint.lint({ code, config: { rules } })).results[0]!.warnings

describe('design tokens', () => {
  it('commits brand/tokens.css exactly as brand/tokens.ts generates it', async () => {
    // On a change of brand.json or tokens.json run `pnpm test -u` in frontend/ to regenerate the file.
    await expect(tokensCss()).toMatchFileSnapshot('../../brand/tokens.css')
  })

  it('defines the same colour roles in both palettes', () => {
    expect(Object.keys(colors('dark'))).toEqual(Object.keys(colors('light')))
  })

  it.each(themes)('meets WCAG AA contrast for every text colour in the %s palette', (theme) => {
    const c = colors(theme)
    const backgrounds = ['--color-background', '--color-surface', '--color-surface-subtle']
    const text = ['--color-text', '--color-muted', '--color-accent', '--color-accent-hover']
    const status = ['--color-danger', '--color-success', '--color-warning']
    for (const bg of backgrounds) {
      for (const fg of [...text, ...status]) {
        expect(contrast(c[fg]!, c[bg]!), `${fg} on ${bg}`).toBeGreaterThanOrEqual(4.5)
      }
      // Non-text contrast (WCAG 1.4.11) for the focus ring and strong borders of controls.
      for (const ui of ['--color-focus', '--color-border-strong']) {
        expect(contrast(c[ui]!, c[bg]!), `${ui} on ${bg}`).toBeGreaterThanOrEqual(3)
      }
    }
    for (const bg of ['--color-accent', '--color-accent-hover']) {
      expect(contrast(c['--color-on-accent']!, c[bg]!), `on-accent on ${bg}`).toBeGreaterThanOrEqual(4.5)
    }
  })

  it('sets the reading text for long reads: larger size, airy leading, limited line length', () => {
    const s = scales()
    expect(parseFloat(s['--font-size-reading']!)).toBeGreaterThan(parseFloat(s['--font-size-md']!))
    expect(Number(s['--line-height-reading'])).toBeGreaterThanOrEqual(1.5)
    // About 60–75 characters per line.
    expect(parseFloat(s['--size-measure']!)).toBeGreaterThanOrEqual(30)
    expect(parseFloat(s['--size-measure']!)).toBeLessThanOrEqual(45)
  })

  it.each([
    ['a hex colour', 'a { color: #fff; }'],
    ['a colour function', 'a { background: rgb(0 0 0 / 50%); }'],
    ['a named colour', 'a { border-color: red; }'],
    ['a raw spacing', 'a { padding: 4px 0; }'],
    ['a raw font size', 'a { font-size: 0.875rem; }'],
    ['a raw radius', 'a { border-radius: 8px; }'],
    ['a raw font weight', 'a { font-weight: 700; }'],
    ['a raw line height', 'a { line-height: 1.5; }'],
    ['a raw font family', 'a { font-family: Arial, sans-serif; }'],
    ['a raw custom property', 'a { --gap: 12px; }'],
  ])('lints away %s', async (_, code) => {
    expect(await lint(code)).not.toEqual([])
  })

  it('accepts styles built from tokens', async () => {
    const code = `a {
      margin: 0 auto;
      padding: var(--space-2) var(--space-4);
      border: var(--size-border) solid var(--color-border);
      border-radius: var(--radius-md);
      background: var(--color-surface);
      color: var(--color-text);
      font-family: var(--font-family-sans);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-bold);
      line-height: var(--line-height-normal);
      width: calc(100% - var(--size-menu));
    }`
    expect(await lint(code)).toEqual([])
  })

  it.each(apps)('%s runs the shared style linter on every .vue and .css file', (app) => {
    const config = readFileSync(join(root, app, 'stylelint.config.mjs'), 'utf8')
    expect(config).toBe(readFileSync(join(root, 'frontend', 'stylelint.config.mjs'), 'utf8'))
    const { scripts } = JSON.parse(readFileSync(join(root, app, 'package.json'), 'utf8'))
    expect(scripts.lint).toContain('stylelint --allow-empty-input "**/*.{css,vue}"')
  })

  it('uses only defined tokens in the styles of both apps', () => {
    const defined = new Set(Object.keys({ ...scales(), ...colors('light') }))
    const unknown = apps
      .flatMap((app) => styleFiles(join(root, app)))
      .flatMap((file) =>
        [...readFileSync(file, 'utf8').matchAll(/var\((--[\w-]+)/g)]
          .map((m) => m[1]!)
          .filter((name) => !defined.has(name))
          .map((name) => `${relative(root, file)}: ${name}`),
      )
    expect(unknown).toEqual([])
  })
})
