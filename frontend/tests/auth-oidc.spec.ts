import { createHash } from 'node:crypto'
import * as oidc from 'openid-client'
import { describe, expect, it } from 'vitest'
import {
  finishSignIn,
  readOidcSettings,
  safeReturnTo,
  startSignIn,
  type OidcSettings,
} from '../server/utils/oidc'

// The site as an OpenID Connect client (E18-02, ADR-0018): the authorization request carries PKCE with S256, the
// callback is checked (state, nonce, verifier) before a session exists, and the return address stays on the site.
const settings: OidcSettings = {
  issuer: 'https://id.example.test/',
  clientId: 'web',
  redirectUri: 'https://site.example.test/auth/callback',
}

const base64url = (value: string | Buffer) => Buffer.from(value).toString('base64url')

function idToken(claims: Record<string, unknown>): string {
  // openid-client checks the claims of an ID token received from the token endpoint over TLS; the signature is not
  // checked there (OpenID Connect Core 3.1.3.7), so the test need not sign it.
  return [
    base64url(JSON.stringify({ alg: 'RS256', typ: 'JWT' })),
    base64url(JSON.stringify(claims)),
    'signature',
  ].join('.')
}

/** A provider configuration whose token endpoint answers with `respond`; the request it got is kept. */
function provider(respond: (body: URLSearchParams) => Record<string, unknown>) {
  const requests: URLSearchParams[] = []
  const configuration = new oidc.Configuration(
    {
      issuer: settings.issuer,
      authorization_endpoint: 'https://id.example.test/connect/authorize',
      token_endpoint: 'https://id.example.test/connect/token',
    },
    settings.clientId,
    undefined,
    oidc.None(),
  )
  configuration[oidc.customFetch] = async (_url, options) => {
    const body = new URLSearchParams(options.body as string)
    requests.push(body)
    return new Response(JSON.stringify(respond(body)), { headers: { 'content-type': 'application/json' } })
  }
  return { configuration, requests }
}

describe('sign-in of the site', () => {
  it('asks for a code with an S256 challenge of a fresh verifier, a state and a nonce', async () => {
    const { configuration } = provider(() => ({}))

    const first = await startSignIn(configuration, settings, '/popular')
    const second = await startSignIn(configuration, settings, '/popular')

    const query = first.url.searchParams
    expect(first.url.origin + first.url.pathname).toBe('https://id.example.test/connect/authorize')
    expect(query.get('client_id')).toBe('web')
    expect(query.get('response_type')).toBe('code')
    expect(query.get('scope')).toBe('openid')
    expect(query.get('redirect_uri')).toBe(settings.redirectUri)
    expect(query.get('code_challenge_method')).toBe('S256')
    expect(query.get('code_challenge')).toBe(
      createHash('sha256').update(first.flow.verifier).digest('base64url'),
    )
    expect(query.get('state')).toBe(first.flow.state)
    expect(query.get('nonce')).toBe(first.flow.nonce)
    // The verifier never leaves the server.
    expect(first.url.href).not.toContain(first.flow.verifier)
    expect(first.flow.returnTo).toBe('/popular')
    expect(second.flow.verifier).not.toBe(first.flow.verifier)
    expect(second.flow.state).not.toBe(first.flow.state)
  })

  it('exchanges the code with the verifier and keeps the account and the access token', async () => {
    const { configuration, requests } = provider(() => ({
      access_token: 'access-1',
      token_type: 'Bearer',
      expires_in: 900,
      id_token: idToken({
        iss: settings.issuer,
        aud: 'web',
        sub: 'account-1',
        nonce: 'nonce-1',
        iat: now(),
        exp: now() + 900,
      }),
    }))
    const flow = {
      verifier: oidc.randomPKCECodeVerifier(),
      state: 'state-1',
      nonce: 'nonce-1',
      returnTo: '/',
    }

    const session = await finishSignIn(configuration, settings, flow, '?code=code-1&state=state-1', 1_000)

    expect(session).toEqual({ accountId: 'account-1', accessToken: 'access-1', expiresAt: 1_000 + 900_000 })
    const sent = requests[0]!
    expect(sent.get('grant_type')).toBe('authorization_code')
    expect(sent.get('code')).toBe('code-1')
    expect(sent.get('code_verifier')).toBe(flow.verifier)
    expect(sent.get('redirect_uri')).toBe(settings.redirectUri)
    expect(sent.get('client_id')).toBe('web')
  })

  it('refuses a callback with a foreign state, a foreign nonce or another issuer', async () => {
    const tokens = (claims: Record<string, unknown>) => ({
      access_token: 'access-1',
      token_type: 'Bearer',
      id_token: idToken({
        iss: settings.issuer,
        aud: 'web',
        sub: 'a',
        nonce: 'nonce-1',
        iat: now(),
        exp: now() + 900,
        ...claims,
      }),
    })
    const flow = {
      verifier: oidc.randomPKCECodeVerifier(),
      state: 'state-1',
      nonce: 'nonce-1',
      returnTo: '/',
    }

    await expect(
      finishSignIn(provider(() => tokens({})).configuration, settings, flow, '?code=c&state=forged', 0),
    ).rejects.toThrow()
    await expect(
      finishSignIn(
        provider(() => tokens({ nonce: 'other' })).configuration,
        settings,
        flow,
        '?code=c&state=state-1',
        0,
      ),
    ).rejects.toThrow()
    await expect(
      finishSignIn(
        provider(() => tokens({ iss: 'https://evil.example.test/' })).configuration,
        settings,
        flow,
        '?code=c&state=state-1',
        0,
      ),
    ).rejects.toThrow()
  })

  it.each([
    ['/popular?sort=new', '/popular?sort=new'],
    ['/', '/'],
    ['https://evil.example.test/', '/'],
    ['//evil.example.test/', '/'],
    ['/\\evil.example.test', '/'],
    ['javascript:alert(1)', '/'],
    [undefined, '/'],
    [['/a', '/b'], '/'],
  ])('returns only to a page of the site: %s', (value, expected) => {
    expect(safeReturnTo(value)).toBe(expected)
  })

  it('needs the provider and the callback address as absolute addresses', () => {
    expect(() => readOidcSettings({ issuer: '', redirectUri: '' })).toThrow(
      /NUXT_OIDC_ISSUER.*NUXT_OIDC_REDIRECT_URI/,
    )
    expect(readOidcSettings({ issuer: settings.issuer, redirectUri: settings.redirectUri })).toEqual(settings)
  })
})

function now(): number {
  return Math.floor(Date.now() / 1000)
}
