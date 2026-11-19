// Acceptance checks of E02-01 (#11): brand/brand.json is the only place of the name and the logo,
// icons come from an open set with a compatible licence, and no DTF graphics or colours are used.
import { readdirSync, readFileSync } from 'node:fs'
import { dirname, extname, join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import brand from '../../brand/brand.json'
import { contrast, deltaE } from './color'

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

  it('keeps the two BrandLogo copies identical apart from the path to brand.json', () => {
    const read = (path: string) =>
      readFileSync(join(root, path), 'utf8').replace(/(\.\.\/)+brand\/brand\.json/, 'brand.json')
    expect(read('admin/src/components/BrandLogo.vue')).toBe(read('frontend/components/BrandLogo.vue'))
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
    // Each app checks its own install; admin/tests/icons.spec.ts does the same for the admin panel.
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
