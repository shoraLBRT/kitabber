// Acceptance of E02-05 (#15) through the style contract: column widths come from the size tokens, the right
// column is hidden below breakpoint.lg, the left menu below breakpoint.md, and both side columns are sticky.
// happy-dom does not lay pages out, so the real widths at 1440 and 925–1240 px are checked by Playwright (#17).
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { breakpoints } from '../../../brand/tokens'

const here = dirname(fileURLToPath(import.meta.url))
const frontend = join(here, '..', '..')
const read = (...path: string[]) => readFileSync(join(frontend, ...path), 'utf8')

const style = read('components', 'shell', 'ShellLayout.vue').split('<style scoped>')[1]!
const tokensCss = read('..', 'brand', 'tokens.css')

/** Declarations of a selector at the top level of the style or inside one media query. */
function rule(selector: string, media?: string): string {
  let scope: string
  if (media) {
    const start = style.indexOf(`@media (${media})`)
    expect(start, `@media (${media})`).toBeGreaterThan(-1)
    const next = style.indexOf('@media', start + 1)
    scope = style.slice(start, next === -1 ? undefined : next)
  } else {
    scope = style.split('@media')[0]!
  }
  const match = new RegExp(`(^|\\n|,)\\s*${selector.replace('.', '\\.')}\\s*(,[^{]*)?\\{([^}]*)\\}`).exec(
    scope,
  )
  expect(match, `${selector} in ${media ?? 'base'}`).not.toBeNull()
  return match![3]!
}

describe('Desktop shell layout', () => {
  it('has size tokens for the three columns of the spec (220, 640, 320 px)', () => {
    expect(tokensCss).toContain('--size-menu: 220px;')
    expect(tokensCss).toContain('--size-feed: 640px;')
    expect(tokensCss).toContain('--size-aside: 320px;')
  })

  it('lays out three centred columns of the token widths', () => {
    const body = rule('.shell__body')
    expect(body).toContain('display: grid')
    expect(body).toContain('grid-template-columns: var(--size-menu) var(--size-feed) var(--size-aside)')
    expect(body).toContain('justify-content: center')
  })

  it('fits the three columns with their gaps into the widest breakpoint', () => {
    expect(220 + 640 + 320 + 2 * 24).toBeLessThanOrEqual(breakpoints.lg)
    expect(rule('.shell__body')).toContain('column-gap: var(--space-6)')
  })

  it('keeps the left menu and the right column in place while the page scrolls', () => {
    const sticky = rule('.shell__aside')
    expect(sticky).toContain('position: sticky')
    expect(sticky).toContain('top: calc(var(--space-16) + var(--space-6))')
    expect(style).toMatch(/\.shell__menu,\s*\.shell__aside\s*\{[^}]*position: sticky/)
    expect(read('components', 'shell', 'ShellHeader.vue')).toMatch(/\.shell-header \{[^}]*position: sticky/)
  })

  it(`hides the right column below ${breakpoints.lg} px`, () => {
    const media = `max-width: ${breakpoints.lg - 1}px`
    expect(rule('.shell__aside', media)).toContain('display: none')
    expect(rule('.shell__body', media)).toContain(
      'grid-template-columns: var(--size-menu) minmax(0, var(--size-feed))',
    )
  })

  it(`hides the left menu below ${breakpoints.md} px and keeps one column`, () => {
    const media = `max-width: ${breakpoints.md - 1}px`
    expect(rule('.shell__menu', media)).toContain('display: none')
    expect(rule('.shell__body', media)).toContain('grid-template-columns: minmax(0, var(--size-feed))')
  })

  it(`uses the full width below ${breakpoints.sm} px`, () => {
    expect(rule('.shell__body', `max-width: ${breakpoints.sm - 1}px`)).toContain(
      'grid-template-columns: minmax(0, 1fr)',
    )
  })

  it('uses only the breakpoint tokens in media queries', () => {
    const widths = [...style.matchAll(/max-width: (\d+)px/g)].map((m) => Number(m[1]) + 1)
    expect(widths).toEqual([breakpoints.lg, breakpoints.md, breakpoints.sm])
  })

  it('wraps every page in the shell, except the design showcases', () => {
    expect(read('app.vue')).toContain('<NuxtLayout>')
    expect(read('layouts', 'default.vue')).toContain('<ShellLayout')
    for (const page of ['brand', 'tokens', 'ui'])
      expect(read('pages', `${page}.vue`)).toContain("definePageMeta({ layout: 'bare' })")
    expect(read('pages', 'index.vue')).not.toContain('<main')
  })
})
