// Design tokens (E02-02, ADR-0012): brand.json gives the brand colours, tokens.json everything else.
// tokensCss() turns both into CSS custom properties; brand/tokens.css is its committed output, and
// frontend/tests/tokens.spec.ts fails when the two differ (`pnpm test -u` in frontend/ regenerates it).
// Framework-free, so both apps and the tests import it.
import brand from './brand.json'
import tokens from './tokens.json'

export type Theme = 'light' | 'dark'
export const themes: readonly Theme[] = ['light', 'dark']

const kebab = (key: string) => key.replace(/[A-Z]/g, (c) => `-${c.toLowerCase()}`)

/** Colour roles of one theme: the brand palette plus the extra roles of tokens.json, as CSS names. */
export function colors(theme: Theme): Record<string, string> {
  const all: Record<string, string> = { ...brand.palette[theme], ...tokens.color[theme] }
  return Object.fromEntries(Object.entries(all).map(([role, hex]) => [`--color-${kebab(role)}`, hex]))
}

/** Every theme-independent token (typography, spacing, radii, sizes) as CSS names. */
export function scales(): Record<string, string> {
  const { family, size, weight, ...rest } = tokens.font
  const groups: Record<string, Record<string, string>> = {
    'font-family': family,
    'font-size': size,
    'font-weight': weight,
    ...rest,
    space: tokens.space,
    radius: tokens.radius,
    size: tokens.size,
  }
  return Object.fromEntries(
    Object.entries(groups).flatMap(([group, values]) =>
      Object.entries(values).map(([name, value]) => [`--${group}-${name}`, value]),
    ),
  )
}

/** Window widths where the layouts change (docs/spec.md, section 5). CSS variables cannot be used in media queries. */
export const breakpoints = tokens.breakpoint

const block = (selector: string, vars: Record<string, string>, indent = '') =>
  `${indent}${selector} {\n` +
  Object.entries(vars)
    .map(([name, value]) => `${indent}  ${name}: ${value};\n`)
    .join('') +
  `${indent}}\n`

export function tokensCss(): string {
  const light = { 'color-scheme': 'light', ...colors('light') }
  const dark = { 'color-scheme': 'dark', ...colors('dark') }
  return (
    '/* Generated from brand/brand.json and brand/tokens.json by brand/tokens.ts. Do not edit by hand. */\n' +
    block(':root', scales()) +
    '\n' +
    block(":root,\n[data-theme='light']", light) +
    '\n' +
    block("[data-theme='dark']", dark) +
    '\n' +
    '@media (prefers-color-scheme: dark) {\n' +
    block(":root:not([data-theme='light'])", dark, '  ') +
    '}\n'
  )
}
