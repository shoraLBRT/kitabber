import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import brand from '../../brand/brand.json'

// The rest of the brand checks live in frontend/tests/brand.spec.ts and cover both apps.
describe('icons', () => {
  it('installs the icon set under a compatible open licence', () => {
    const here = dirname(fileURLToPath(import.meta.url))
    const url = join(here, '..', 'node_modules', brand.icons.package, 'package.json')
    const { license } = JSON.parse(readFileSync(url, 'utf8'))
    expect(['MIT', 'ISC']).toContain(license)
    expect(license).toBe(brand.icons.license)
  })
})
