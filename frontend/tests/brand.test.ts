// Acceptance checks of E02-01 (#11): brand/brand.json is the only place of the name and the logo,
// icons come from an open set with a compatible licence, and no DTF graphics or colours are used.
import { readdirSync, readFileSync } from 'node:fs'
import { dirname, extname, join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import brand from '../../brand/brand.json'

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..')
const apps = ['frontend', 'admin']
const skipped = new Set([
  'node_modules',
  '.nuxt',
  '.output',
  'dist',
  'coverage',
  'test-results',
  'playwright-report',
])

function appFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    if (skipped.has(entry.name)) return []
    const path = join(dir, entry.name)
    return entry.isDirectory() ? appFiles(path) : [path]
  })
}

const files = apps.flatMap((app) => appFiles(join(root, app)))

type Rgb = [number, number, number]
const rgb = (hex: string): Rgb => [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255) as Rgb
const linear = (c: number) => (c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4)

// WCAG 2.x relative luminance and contrast ratio.
function contrast(a: string, b: string): number {
  const lum = (hex: string) => {
    const [r, g, bl] = rgb(hex).map(linear) as Rgb
    return 0.2126 * r + 0.7152 * g + 0.0722 * bl
  }
  const [hi, lo] = [lum(a), lum(b)].sort((x, y) => y - x) as [number, number]
  return (hi + 0.05) / (lo + 0.05)
}

// CIE76 colour difference in CIELAB (D65).
function deltaE(a: string, b: string): number {
  const lab = (hex: string) => {
    const [r, g, bl] = rgb(hex).map(linear) as Rgb
    const xyz = [
      (0.4124 * r + 0.3576 * g + 0.1805 * bl) / 0.95047,
      0.2126 * r + 0.7152 * g + 0.0722 * bl,
      (0.0193 * r + 0.1192 * g + 0.9505 * bl) / 1.08883,
    ].map((t) => (t > 216 / 24389 ? Math.cbrt(t) : (t * 24389) / 27 / 116 + 16 / 116)) as Rgb
    return [116 * xyz[1] - 16, 500 * (xyz[0] - xyz[1]), 200 * (xyz[1] - xyz[2])]
  }
  const [x, y] = [lab(a), lab(b)]
  return Math.hypot(...x.map((v, i) => v - y[i]!))
}

// Package names of icon sets; only brand.icons.package may be a dependency.
const iconPackage = /icon|lucide|heroicons|fortawesome|phosphor|tabler|feather|@mdi\//i

// Approximate DTF accent colours (its blue links and buttons); the brand must stay clearly apart.
const dtfAccents = ['#4683D9', '#3B7BD4', '#5B9BE6', '#2F6FC9']

describe('brand', () => {
  it('writes the product name only in brand/brand.json', () => {
    const name = brand.name.toLowerCase()
    const offenders = files
      .filter((f) => !f.endsWith('pnpm-lock.yaml') && readFileSync(f, 'utf8').toLowerCase().includes(name))
      .map((f) => relative(root, f))
    expect(offenders).toEqual([])
  })

  it('keeps every graphic in brand.json or the icon set: the apps hold no image files', () => {
    const images = files.filter((f) =>
      ['.svg', '.png', '.jpg', '.jpeg', '.gif', '.webp', '.ico'].includes(extname(f)),
    )
    expect(images.map((f) => relative(root, f))).toEqual([])
  })

  it.each(apps)('%s declares no icon package except the open set named in brand.json', (app) => {
    const manifest = JSON.parse(readFileSync(join(root, app, 'package.json'), 'utf8'))
    const deps = Object.keys({ ...manifest.dependencies, ...manifest.devDependencies })
    expect(deps.filter((d) => iconPackage.test(d))).toEqual([brand.icons.package])
  })

  it('installs the icon set under a compatible open licence', () => {
    // Each app checks its own install; admin/tests/icons.test.ts does the same for the admin panel.
    const installed = join(root, 'frontend', 'node_modules', brand.icons.package, 'package.json')
    const { license } = JSON.parse(readFileSync(installed, 'utf8'))
    expect(['MIT', 'ISC']).toContain(license)
    expect(license).toBe(brand.icons.license)
  })

  it.each(['light', 'dark'] as const)('meets WCAG AA contrast in the %s palette', (theme) => {
    const p = brand.palette[theme]
    for (const bg of [p.background, p.surface]) {
      expect(contrast(p.text, bg)).toBeGreaterThanOrEqual(7)
      expect(contrast(p.muted, bg)).toBeGreaterThanOrEqual(4.5)
      expect(contrast(p.accent, bg)).toBeGreaterThanOrEqual(4.5)
    }
    expect(contrast(p.onAccent, p.accent)).toBeGreaterThanOrEqual(4.5)
  })

  it.each(['light', 'dark'] as const)('keeps the %s accent away from DTF colours', (theme) => {
    for (const dtf of dtfAccents) expect(deltaE(brand.palette[theme].accent, dtf)).toBeGreaterThan(30)
  })
})
