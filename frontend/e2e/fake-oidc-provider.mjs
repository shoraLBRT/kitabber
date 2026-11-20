// A fake OpenID Connect provider for the e2e tests (docs/TESTING.md: external services only as fakes). It stands for
// the platform's provider (E18-02) the way the site sees it: discovery, the authorization endpoint and the token
// endpoint of the authorization code flow, with the same rules — the registered client and redirect address only,
// PKCE with S256 only, a code once and only with its verifier. A person "signs in" by reaching the authorization
// endpoint; the fake answers as the account FAKE_ACCOUNT. GET /__last tells a test what the site asked for.
import { createHash, generateKeyPairSync, randomBytes, sign } from 'node:crypto'
import { createServer } from 'node:http'

const port = Number(process.env.FAKE_OIDC_PORT ?? 3101)
const issuer = `http://127.0.0.1:${port}/`
const clientId = 'web'
const redirectUri = process.env.FAKE_OIDC_REDIRECT_URI ?? 'http://127.0.0.1:3100/auth/callback'
const account = '0195f3a0-0000-7000-8000-000000000001'
const { privateKey } = generateKeyPairSync('rsa', { modulusLength: 2048 })
const codes = new Map()
let last = null

const b64 = (value) => Buffer.from(value).toString('base64url')

function jwt(claims) {
  const head = b64(JSON.stringify({ alg: 'RS256', typ: 'JWT', kid: 'fake' }))
  const body = b64(JSON.stringify(claims))
  return `${head}.${body}.${sign('sha256', Buffer.from(`${head}.${body}`), privateKey).toString('base64url')}`
}

function json(response, status, body) {
  response.writeHead(status, { 'content-type': 'application/json', 'cache-control': 'no-store' })
  response.end(JSON.stringify(body))
}

function authorize(url, response) {
  const q = url.searchParams
  last = Object.fromEntries(q)
  if (q.get('client_id') !== clientId || q.get('redirect_uri') !== redirectUri) {
    return json(response, 400, {
      error: 'invalid_request',
      error_description: 'unknown client or redirect address',
    })
  }
  if (
    q.get('response_type') !== 'code' ||
    q.get('code_challenge_method') !== 'S256' ||
    !q.get('code_challenge')
  ) {
    return json(response, 400, { error: 'invalid_request', error_description: 'code with PKCE S256 only' })
  }
  const code = randomBytes(24).toString('base64url')
  codes.set(code, { challenge: q.get('code_challenge'), nonce: q.get('nonce') })
  const back = new URL(redirectUri)
  back.searchParams.set('code', code)
  back.searchParams.set('state', q.get('state') ?? '')
  back.searchParams.set('iss', issuer)
  response.writeHead(302, { location: back.href })
  response.end()
}

function token(form, response) {
  const grant = codes.get(form.get('code'))
  codes.delete(form.get('code'))
  const verifier = form.get('code_verifier') ?? ''
  if (
    form.get('grant_type') !== 'authorization_code' ||
    form.get('client_id') !== clientId ||
    form.get('redirect_uri') !== redirectUri ||
    !grant ||
    createHash('sha256').update(verifier).digest('base64url') !== grant.challenge
  ) {
    return json(response, 400, { error: 'invalid_grant' })
  }
  const now = Math.floor(Date.now() / 1000)
  json(response, 200, {
    token_type: 'Bearer',
    expires_in: 900,
    access_token: jwt({ iss: issuer, aud: 'platform-api', sub: account, iat: now, exp: now + 900 }),
    id_token: jwt({ iss: issuer, aud: clientId, sub: account, nonce: grant.nonce, iat: now, exp: now + 900 }),
  })
}

createServer((request, response) => {
  const url = new URL(request.url ?? '/', issuer)
  if (url.pathname === '/.well-known/openid-configuration') {
    return json(response, 200, {
      issuer,
      authorization_endpoint: `${issuer}connect/authorize`,
      token_endpoint: `${issuer}connect/token`,
      response_types_supported: ['code'],
      grant_types_supported: ['authorization_code'],
      code_challenge_methods_supported: ['S256'],
      authorization_response_iss_parameter_supported: true,
      id_token_signing_alg_values_supported: ['RS256'],
      subject_types_supported: ['public'],
    })
  }
  if (url.pathname === '/connect/authorize') return authorize(url, response)
  if (url.pathname === '/__last') return json(response, 200, last ?? {})
  if (url.pathname === '/connect/token' && request.method === 'POST') {
    let body = ''
    request.on('data', (chunk) => (body += chunk))
    request.on('end', () => token(new URLSearchParams(body), response))
    return
  }
  if (url.pathname === '/') return json(response, 200, { fake: 'oidc' })
  json(response, 404, { error: 'not_found' })
}).listen(port, '127.0.0.1')
