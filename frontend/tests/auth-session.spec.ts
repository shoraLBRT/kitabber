import * as oidc from 'openid-client'
import { createStorage } from 'unstorage'
import { describe, expect, it } from 'vitest'
import {
  endSessionUrl,
  finishSignIn,
  needsRefresh,
  refreshSession,
  revokeSession,
  sessionLifetimeMs,
  type OidcSettings,
  type StoredSession,
} from '../server/utils/oidc'
import { SessionStore } from '../server/utils/session-store'

// The site's session over OpenID Connect (E03-03, ADR-0024): the access token is renewed by the refresh token before
// it expires, once at a time; a refused refresh ends the session; sign-out revokes the refresh token and ends the
// session at the provider.
const settings: OidcSettings = {
  issuer: 'https://id.example.test/',
  clientId: 'web',
  redirectUri: 'https://site.example.test/auth/callback',
}
const account = '0195f3a0-0000-7000-8000-000000000001'
const now = Date.UTC(2026, 10, 21, 12)

function jwt(claims: Record<string, unknown>): string {
  const b64 = (value: string) => Buffer.from(value).toString('base64url')
  return [b64(JSON.stringify({ alg: 'RS256', typ: 'JWT' })), b64(JSON.stringify(claims)), 'signature'].join(
    '.',
  )
}

const idToken = (sub = account) =>
  jwt({ iss: settings.issuer, aud: 'web', sub, iat: now / 1000, exp: now / 1000 + 900 })

/** A provider whose endpoints answer with `respond`; the requests it got are kept. */
function provider(
  respond: (url: string, body: URLSearchParams) => Response,
  metadata: Partial<oidc.ServerMetadata> = {},
) {
  const requests: { url: string; body: URLSearchParams }[] = []
  const configuration = new oidc.Configuration(
    {
      issuer: settings.issuer,
      authorization_endpoint: 'https://id.example.test/connect/authorize',
      token_endpoint: 'https://id.example.test/connect/token',
      revocation_endpoint: 'https://id.example.test/connect/revoke',
      end_session_endpoint: 'https://id.example.test/connect/endsession',
      ...metadata,
    },
    settings.clientId,
    undefined,
    oidc.None(),
  )
  configuration[oidc.customFetch] = async (url, options) => {
    const body = new URLSearchParams(options.body as string)
    requests.push({ url: String(url), body })
    return respond(String(url), body)
  }
  return { configuration, requests }
}

const json = (body: Record<string, unknown>, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })

const session = (overrides: Partial<StoredSession> = {}): StoredSession => ({
  accountId: account,
  accessToken: 'access-1',
  accessExpiresAt: now + 10_000,
  refreshToken: 'refresh-1',
  idToken: 'id-1',
  expiresAt: now + sessionLifetimeMs,
  ...overrides,
})

describe('the tokens of a session', () => {
  it('asks for a refresh token and keeps it; the session lives as long as the refresh token', async () => {
    const flow = { verifier: 'v'.repeat(43), state: 's', nonce: 'n', returnTo: '/' }
    const { configuration } = provider(() =>
      json({
        token_type: 'Bearer',
        access_token: 'a',
        expires_in: 900,
        refresh_token: 'r',
        id_token: jwt({
          iss: settings.issuer,
          aud: 'web',
          sub: account,
          nonce: 'n',
          iat: now / 1000,
          exp: now / 1000 + 900,
        }),
      }),
    )

    const stored = await finishSignIn(
      configuration,
      settings,
      flow,
      `?code=c&state=s&iss=${settings.issuer}`,
      now,
    )

    expect(stored).toMatchObject({ accessToken: 'a', refreshToken: 'r', accessExpiresAt: now + 900_000 })
    expect(stored.expiresAt).toBe(now + sessionLifetimeMs)
  })

  it('is renewed only when the access token is about to expire and a refresh token exists', () => {
    expect(needsRefresh(session({ accessExpiresAt: now + 60_000 }), now)).toBe(false)
    expect(needsRefresh(session({ accessExpiresAt: now + 10_000 }), now)).toBe(true)
    expect(needsRefresh(session({ accessExpiresAt: now - 1 }), now)).toBe(true)
    expect(needsRefresh(session({ accessExpiresAt: now - 1, refreshToken: undefined }), now)).toBe(false)
  })

  it('renews with the refresh token and takes the rotated one', async () => {
    const { configuration, requests } = provider(() =>
      json({
        token_type: 'Bearer',
        access_token: 'access-2',
        expires_in: 900,
        refresh_token: 'refresh-2',
        id_token: idToken(),
      }),
    )

    const renewed = await refreshSession(configuration, session(), now)

    expect(requests[0]!.body.get('grant_type')).toBe('refresh_token')
    expect(requests[0]!.body.get('refresh_token')).toBe('refresh-1')
    expect(renewed).toMatchObject({
      accessToken: 'access-2',
      refreshToken: 'refresh-2',
      accessExpiresAt: now + 900_000,
    })
    expect(renewed!.expiresAt).toBe(now + sessionLifetimeMs)
  })

  it('ends the session when the provider refuses the refresh token or names another person', async () => {
    const refused = provider(() => json({ error: 'invalid_grant' }, 400))
    const other = provider(() =>
      json({
        token_type: 'Bearer',
        access_token: 'x',
        expires_in: 900,
        refresh_token: 'y',
        id_token: idToken('someone-else'),
      }),
    )

    expect(await refreshSession(refused.configuration, session(), now)).toBeNull()
    expect(await refreshSession(other.configuration, session(), now)).toBeNull()
  })

  it('keeps the session when the provider is unreachable', async () => {
    const { configuration } = provider(() => json({ error: 'server_error' }, 503))

    await expect(refreshSession(configuration, session(), now)).rejects.toThrow()
  })

  it('revokes the refresh token on sign-out and sends the browser to the end-session endpoint', async () => {
    const { configuration, requests } = provider(() => new Response(null, { status: 200 }))

    expect(await revokeSession(configuration, session())).toBe(true)
    const url = new URL(endSessionUrl(configuration, settings, 'id-1'))

    expect(requests[0]!.url).toBe('https://id.example.test/connect/revoke')
    expect(requests[0]!.body.get('token')).toBe('refresh-1')
    expect(requests[0]!.body.get('token_type_hint')).toBe('refresh_token')
    expect(url.origin + url.pathname).toBe('https://id.example.test/connect/endsession')
    expect(url.searchParams.get('post_logout_redirect_uri')).toBe('https://site.example.test/')
    expect(url.searchParams.get('id_token_hint')).toBe('id-1')
  })

  it('signs out of the site alone when the provider has no end-session endpoint', () => {
    const { configuration } = provider(() => json({}), { end_session_endpoint: undefined })

    expect(endSessionUrl(configuration, settings, 'id-1')).toBe('/')
  })
})

describe('the session store', () => {
  function store(refresh: (stored: StoredSession) => Promise<StoredSession | null>, clock = { now }) {
    const storage = createStorage<StoredSession>()
    const calls: StoredSession[] = []
    const sessions = new SessionStore(
      () => storage,
      async (stored) => {
        calls.push(stored)
        return refresh(stored)
      },
      () => clock.now,
    )
    return { sessions, storage, calls, clock }
  }

  const renewed = () =>
    Promise.resolve(
      session({ accessToken: 'access-2', refreshToken: 'refresh-2', accessExpiresAt: now + 900_000 }),
    )

  it('gives a fresh session as it is', async () => {
    const { sessions, calls } = store(renewed)
    await sessions.save('sid', session({ accessExpiresAt: now + 600_000 }))

    const result = await sessions.read('sid')

    expect(result).toMatchObject({ session: { accessToken: 'access-1' }, renewed: false })
    expect(calls).toHaveLength(0)
  })

  it('renews an access token about to expire, once for requests that come at once, and keeps the new tokens', async () => {
    const { sessions, storage, calls } = store(renewed)
    await sessions.save('sid', session())

    const results = await Promise.all([sessions.read('sid'), sessions.read('sid'), sessions.read('sid')])

    expect(calls).toHaveLength(1)
    expect(results.every((r) => r?.session.accessToken === 'access-2' && r.renewed)).toBe(true)
    expect((await storage.getItem('sid'))?.refreshToken).toBe('refresh-2')
  })

  it('renews at once when the API refused an access token that looks valid', async () => {
    const { sessions, calls } = store(renewed)
    await sessions.save('sid', session({ accessExpiresAt: now + 600_000 }))

    const result = await sessions.renewNow('sid', 'access-1')

    expect(calls).toHaveLength(1)
    expect(result?.session.accessToken).toBe('access-2')
  })

  it('takes the tokens another instance has just renewed instead of renewing again', async () => {
    const { sessions, storage, calls } = store(renewed)
    await sessions.save('sid', session())
    const stale = (await storage.getItem('sid'))!
    await storage.setItem('sid', { ...stale, accessToken: 'access-other', accessExpiresAt: now + 900_000 })

    const result = await sessions.renewNow('sid', 'access-1')

    expect(calls).toHaveLength(0)
    expect(result?.session.accessToken).toBe('access-other')
  })

  it('forgets the session when the refresh token is refused', async () => {
    const { sessions, storage } = store(() => Promise.resolve(null))
    await sessions.save('sid', session())

    expect(await sessions.read('sid')).toBeNull()
    expect(await storage.hasItem('sid')).toBe(false)
  })

  it('keeps the session while its access token is valid when the provider is down', async () => {
    const { sessions, storage, clock } = store(() => Promise.reject(new Error('down')))
    await sessions.save('sid', session())

    expect((await sessions.read('sid'))?.session.accessToken).toBe('access-1')
    clock.now = now + 20_000
    expect(await sessions.read('sid')).toBeNull()
    expect(await storage.hasItem('sid')).toBe(true)
  })

  it('ends a session past its lifetime', async () => {
    const { sessions, storage, clock } = store(renewed)
    await sessions.save('sid', session())
    clock.now = now + sessionLifetimeMs + 1

    expect(await sessions.read('sid')).toBeNull()
    expect(await storage.hasItem('sid')).toBe(false)
  })

  it('does not bring back a session signed out while it was being renewed', async () => {
    const { sessions } = store(renewed)

    expect(await sessions.renewNow('gone', 'access-1')).toBeNull()
  })
})
