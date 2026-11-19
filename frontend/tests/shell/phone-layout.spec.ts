// Acceptance of E02-06 (#16) through the style contract: the bottom bar is shown below breakpoint.md (the left menu
// is hidden there, docs/spec.md section 5), the floating "Написать" below breakpoint.sm, the safe areas and the
// notch are respected, tabs are at least 44 px tall and nothing in the bar can widen the page at 390 px.
// happy-dom does not lay pages out, so the real 390 px page (no horizontal scroll, the bar on screen) is checked by
// Playwright (#17).
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import { breakpoints } from '../../../brand/tokens'
import ShellWriteButton from '../../components/shell/ShellWriteButton.vue'
import { writePath } from '../../utils/navigation'
import { global } from './fixtures'

const frontend = join(dirname(fileURLToPath(import.meta.url)), '..', '..')
const read = (...path: string[]) => readFileSync(join(frontend, ...path), 'utf8')
const styleOf = (...path: string[]) => read(...path).split('<style scoped>')[1]!

const tabBar = styleOf('components', 'shell', 'ShellTabBar.vue')
const writeButton = styleOf('components', 'shell', 'ShellWriteButton.vue')
const layout = styleOf('components', 'shell', 'ShellLayout.vue')
const header = styleOf('components', 'shell', 'ShellHeader.vue')
const toaster = styleOf('components', 'ui', 'UiToaster.vue')
const menuScreen = styleOf('components', 'shell', 'ShellMenuScreen.vue')

const below = (px: number) => `max-width: ${px - 1}px`

/** Declarations of a selector at the top level of a style or inside one media query. */
function rule(style: string, selector: string, media?: string): string {
  let scope: string
  if (media) {
    const start = style.indexOf(`@media (${media})`)
    expect(start, `@media (${media})`).toBeGreaterThan(-1)
    const next = style.indexOf('@media', start + 1)
    scope = style.slice(start, next === -1 ? undefined : next)
  } else {
    scope = style.split('@media')[0]!
  }
  const escaped = selector.replace(/[.[\]'=]/g, (c) => `\\${c}`)
  const match = new RegExp(`(^|\\n|,)\\s*${escaped}\\s*(,[^{]*)?\\{([^}]*)\\}`).exec(scope)
  expect(match, `${selector} in ${media ?? 'base'}`).not.toBeNull()
  // One line, without the spaces Prettier puts inside long calc() expressions.
  return match![3]!.replace(/\s+/g, ' ').replace(/\( /g, '(').replace(/ \)/g, ')')
}

/** Height of the bottom bar: a 44 px tab, its padding and the top border. Everything clearing the bar uses it. */
const barHeight = 'var(--size-touch) + 2 * var(--space-1) + var(--size-border)'

describe('Phone shell layout', () => {
  it('has a 44 px touch target token (docs/spec.md section 5)', () => {
    expect(read('..', 'brand', 'tokens.css')).toContain('--size-touch: 44px;')
  })

  it(`shows the bottom bar below ${breakpoints.md} px only, fixed to the bottom edge`, () => {
    const base = rule(tabBar, '.tab-bar')
    expect(base).toContain('position: fixed')
    expect(base).toContain('bottom: 0')
    expect(base).toContain('display: none')
    expect(rule(tabBar, '.tab-bar', below(breakpoints.md))).toContain('display: block')
  })

  it(`shows the floating "Написать" below ${breakpoints.sm} px only, as a round accent button above the bar`, () => {
    const base = rule(writeButton, '.write-button')
    expect(base).toContain('display: none')
    expect(base).toContain('border-radius: var(--radius-full)')
    expect(base).toContain('background: var(--color-accent)')
    expect(base).toContain(`bottom: calc(${barHeight} + var(--space-4) + env(safe-area-inset-bottom))`)
    expect(rule(writeButton, '.write-button', below(breakpoints.sm))).toContain('display: inline-flex')
  })

  it('names the floating button and links it to the editor', () => {
    const w = mount(ShellWriteButton, { global })
    expect(w.get('a').attributes('aria-label')).toBe('Написать')
    expect(w.get('a').attributes('href')).toBe(writePath)
    expect(w.get('svg').attributes('aria-hidden')).toBe('true')
    w.unmount()
  })

  it('keeps the bar, the button and the header clear of the safe areas and the notch', () => {
    expect(read('nuxt.config.ts')).toContain('viewport-fit=cover')
    const bar = rule(tabBar, '.tab-bar')
    for (const side of ['bottom', 'left', 'right']) expect(bar).toContain(`env(safe-area-inset-${side})`)
    expect(rule(writeButton, '.write-button')).toContain('env(safe-area-inset-right)')
    expect(rule(header, '.shell-header')).toContain('padding-top: env(safe-area-inset-top)')
    const phoneHeader = rule(header, '.shell-header__inner', below(breakpoints.sm))
    for (const side of ['left', 'right']) expect(phoneHeader).toContain(`env(safe-area-inset-${side})`)
  })

  it('makes every tab and every "Меню" row at least 44 px tall', () => {
    expect(rule(tabBar, '.tab-bar__link')).toContain('min-height: var(--size-touch)')
    expect(rule(menuScreen, '.menu-screen__row')).toContain('min-height: var(--size-touch)')
    expect(rule(writeButton, '.write-button')).toContain('width: var(--space-12)')
  })

  it('lets no tab widen the page: five equal shrinking tabs with clipped labels', () => {
    expect(rule(tabBar, '.tab-bar__item')).toContain('flex: 1 1 0')
    expect(rule(tabBar, '.tab-bar__item')).toContain('min-width: 0')
    const label = rule(tabBar, '.tab-bar__label')
    expect(label).toContain('overflow: hidden')
    expect(label).toContain('text-overflow: ellipsis')
    expect(rule(tabBar, '.tab-bar')).toMatch(/right: 0.*left: 0/)
  })

  it('leaves room under the content for the bar and above it for toasts', () => {
    const clear = `calc(${barHeight} + var(--space-16) + env(safe-area-inset-bottom))`
    expect(rule(layout, '.shell__body', below(breakpoints.md))).toContain(clear)
    expect(rule(layout, '.shell__body', below(breakpoints.sm))).toContain(clear)
    expect(rule(toaster, '.ui-toaster', below(breakpoints.md))).toContain(
      `bottom: calc(${barHeight} + var(--space-2) + env(safe-area-inset-bottom))`,
    )
  })

  it('puts the bottom bar and the floating button into every page of the default layout', () => {
    const shell = read('components', 'shell', 'ShellLayout.vue')
    expect(shell).toMatch(/<slot name="mobile">\s*<ShellWriteButton \/>\s*<ShellTabBar/)
    expect(read('layouts', 'default.vue')).toContain('<ShellLayout')
  })

  it('uses only the breakpoint tokens in media queries', () => {
    const allowed = new Set([breakpoints.lg, breakpoints.md, breakpoints.sm])
    for (const style of [tabBar, writeButton, toaster, menuScreen])
      for (const m of style.matchAll(/max-width: (\d+)px/g)) expect(allowed).toContain(Number(m[1]) + 1)
  })
})
