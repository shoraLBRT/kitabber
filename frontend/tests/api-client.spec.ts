// @vitest-environment node
// openapi-typescript needs Node's URL; the DOM environment replaces it, and nothing here touches the DOM.
// Acceptance checks of E01-08 (#19): the API client is rebuilt by one command, a difference between the OpenAPI
// snapshot and the committed client fails the tests (and so `ci / verify`), and the app has no hand-written API
// calls or types outside the generated client. The same checks for the admin live in admin/tests.
import { readdirSync, readFileSync } from 'node:fs'
import { dirname, extname, join, relative } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import openapiTS, { astToString, COMMENT_HEADER } from 'openapi-typescript'
import { describe, expect, it } from 'vitest'
import { createApiClient } from '../api/client'

const app = join(dirname(fileURLToPath(import.meta.url)), '..')
const clientDir = 'api'
const command: string = JSON.parse(readFileSync(join(app, 'package.json'), 'utf8')).scripts['api:generate']
const skipped = new Set(['node_modules', '.nuxt', '.output', 'dist', 'coverage', 'tests', 'e2e', clientDir])

/**
 * Builds the client the way the `api:generate` command does and returns the generated text. It runs
 * openapi-typescript in this process (the same `openapiTS` + `astToString` + `COMMENT_HEADER` its CLI calls)
 * instead of starting the CLI: a second Node process with a cold module load took more than Vitest's 5 s
 * under the load of the full `verify.ps1` run (#307).
 */
async function generate(): Promise<string> {
  const [bin, input, flag, output, ...rest] = command.split(' ')
  // Only the input and the output are given, so the CLI passes no generation options and the defaults below
  // build the same text (the library adds one validation rule, unique operationIds, which never changes the output).
  expect([bin, flag, output, rest]).toEqual(['openapi-typescript', '-o', `${clientDir}/schema.d.ts`, []])
  const ast = await openapiTS(pathToFileURL(join(app, input!)), {
    cwd: pathToFileURL(`${app}/`),
    silent: true,
  })
  return `${COMMENT_HEADER}${astToString(ast)}`
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

  it('commits api/schema.d.ts exactly as `pnpm run api:generate` builds it', async () => {
    const fresh = await generate()
    const committed = readFileSync(join(app, clientDir, 'schema.d.ts'), 'utf8')
    // On failure: the API changed. Run `pnpm run api:generate` here and commit api/schema.d.ts.
    expect(committed.replace(/\r\n/g, '\n')).toBe(fresh.replace(/\r\n/g, '\n'))
  })

  it('has no API calls or API types outside the generated client', () => {
    const offenders = sources(app).filter((file) => {
      // The site's own server routes under /auth/ (sign-in, the hub ticket, E01-11) and /bff/ (the blog page's
      // profile, E04-02) are not the API: $fetch may call them.
      const text = readFileSync(file, 'utf8').replace(/\$fetch(?:<[^>]*>)?\(\s*['"`]\/(?:auth|bff)\//g, '')
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
