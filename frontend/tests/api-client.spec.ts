// Acceptance checks of E01-08 (#19): the API client is rebuilt by one command, a difference between the OpenAPI
// snapshot and the committed client fails the tests (and so `ci / verify`), and the app has no hand-written API
// calls or types outside the generated client. The same checks for the admin live in admin/tests.
import { execFileSync } from 'node:child_process'
import { mkdtempSync, readdirSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, extname, join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { createApiClient } from '../api/client'

const app = join(dirname(fileURLToPath(import.meta.url)), '..')
const clientDir = 'api'
const command: string = JSON.parse(readFileSync(join(app, 'package.json'), 'utf8')).scripts['api:generate']
const skipped = new Set(['node_modules', '.nuxt', '.output', 'dist', 'coverage', 'tests', 'e2e', clientDir])

/** Runs the `api:generate` command with its output redirected to `out`, and returns the generated text. */
function generate(out: string): string {
  const [bin, ...args] = command.split(' ')
  expect(bin).toBe('openapi-typescript')
  const outIndex = args.indexOf('-o') + 1
  args[outIndex] = out
  const cli = join(app, 'node_modules', 'openapi-typescript', 'bin', 'cli.js')
  execFileSync(process.execPath, [cli, ...args], { cwd: app, stdio: 'pipe' })
  return readFileSync(out, 'utf8')
}

function sources(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    if (skipped.has(entry.name) || entry.name.startsWith('.')) return []
    const path = join(dir, entry.name)
    if (entry.isDirectory()) return sources(path)
    return ['.ts', '.vue', '.mts', '.js', '.mjs'].includes(extname(path)) ? [path] : []
  })
}

describe('API client', () => {
  it('generates the client from the OpenAPI snapshot of the backend', () => {
    expect(command).toBe(`openapi-typescript ../backend/src/Api/openapi/v1.json -o ${clientDir}/schema.d.ts`)
  })

  it('commits api/schema.d.ts exactly as `pnpm run api:generate` builds it', () => {
    const dir = mkdtempSync(join(tmpdir(), 'api-client-'))
    try {
      const fresh = generate(join(dir, 'schema.d.ts'))
      const committed = readFileSync(join(app, clientDir, 'schema.d.ts'), 'utf8')
      // On failure: the API changed. Run `pnpm run api:generate` here and commit api/schema.d.ts.
      expect(committed.replace(/\r\n/g, '\n')).toBe(fresh.replace(/\r\n/g, '\n'))
    } finally {
      rmSync(dir, { recursive: true, force: true })
    }
  })

  it('has no API calls or API types outside the generated client', () => {
    const offenders = sources(app).filter((file) => {
      // The site's own server routes under /auth/ (sign-in, the hub ticket, E01-11) are not the API: $fetch may
      // call them.
      const text = readFileSync(file, 'utf8').replace(/\$fetch(?:<[^>]*>)?\(\s*['"`]\/auth\//g, '')
      return /['"`]\/api\/|\$fetch\s*[(<]|useFetch\s*[(<]|useLazyFetch\s*[(<]|\bfetch\s*\(/.test(text)
    })
    expect(offenders.map((file) => relative(app, file))).toEqual([])
  })

  it('sends requests to the base URL and returns problem details as the error', async () => {
    const calls: Request[] = []
    const problem = { type: 'about:blank', title: 'Not Found', status: 404, code: 'not_found', traceId: 't' }
    const client = createApiClient({
      baseUrl: 'https://api.example.test',
      fetch: async (request: Request) => {
        calls.push(request)
        return new Response(JSON.stringify(problem), {
          status: 404,
          headers: { 'content-type': 'application/problem+json' },
        })
      },
    })
    // The snapshot has no paths yet, so the call is untyped here; real calls are checked by the compiler.
    const get = client.GET as unknown as (path: string) => Promise<{ error?: unknown; response: Response }>
    const { error, response } = await get('/api/v1/missing')
    expect(calls[0]!.url).toBe('https://api.example.test/api/v1/missing')
    expect(response.status).toBe(404)
    expect(error).toEqual(problem)
  })
})
