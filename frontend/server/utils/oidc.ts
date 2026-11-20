// Sign-in of the site through the platform's OpenID Connect provider (E18-02, ADR-0002, ADR-0018): the Nuxt server is
// the client (a BFF). It runs the authorization code flow with PKCE (S256), keeps the tokens on the server and gives
// the browser only an encrypted, HttpOnly session cookie: no token ever reaches the page, localStorage or a script.
// This file holds the parts without a request, so the unit tests reach them; the routes are server/routes/auth/.
import * as oidc from 'openid-client'

/** The client settings: runtimeConfig.oidc, set by NUXT_OIDC_* (docs/CONFIG.md). */
export interface OidcSettings {
  /** The provider's issuer, e.g. https://example.org/ (NUXT_OIDC_ISSUER). */
  issuer: string
  /** The client registered at the provider, `web` (NUXT_OIDC_CLIENT_ID). */
  clientId: string
  /** This site's /auth/callback, exactly as registered at the provider (NUXT_OIDC_REDIRECT_URI). */
  redirectUri: string
}

/** What the browser keeps between /auth/sign-in and /auth/callback, sealed in a short-lived cookie. */
export interface SignInFlow {
  verifier: string
  state: string
  nonce: string
  returnTo: string
}

/** The person's session as the server keeps it; the cookie holds only its random id. */
export interface StoredSession {
  accountId: string
  accessToken: string
  /** Unix milliseconds: the session ends with the access token, until refresh tokens arrive (#29). */
  expiresAt: number
}

export const flowCookie = 'auth.flow'
export const sessionCookie = 'auth.session'
export const flowMaxAgeSeconds = 10 * 60

export function readOidcSettings(config: Partial<OidcSettings> | undefined): OidcSettings {
  const settings = {
    issuer: config?.issuer ?? '',
    clientId: config?.clientId || 'web',
    redirectUri: config?.redirectUri ?? '',
  }
  const problems = [
    !isHttpUrl(settings.issuer) && 'NUXT_OIDC_ISSUER must be the absolute http(s) address of the provider',
    !isHttpUrl(settings.redirectUri) &&
      'NUXT_OIDC_REDIRECT_URI must be the absolute http(s) address of /auth/callback',
  ].filter(Boolean)
  if (problems.length > 0) throw new Error(`Sign-in is not configured: ${problems.join('; ')}`)
  return settings
}

/**
 * Where to send the person after signing in: a path of this site only. Anything else — another origin, a
 * protocol-relative `//host`, a backslash trick — falls back to the home page, so the sign-in cannot become an open
 * redirect.
 */
export function safeReturnTo(value: unknown): string {
  if (typeof value !== 'string' || !value.startsWith('/') || value.startsWith('//') || /[\\\s]/.test(value))
    return '/'
  const parsed = new URL(value, 'http://site.invalid')
  return parsed.origin === 'http://site.invalid' ? parsed.pathname + parsed.search + parsed.hash : '/'
}

/** A new authorization request: the address to send the person to and the flow to remember. */
export async function startSignIn(
  configuration: oidc.Configuration,
  settings: OidcSettings,
  returnTo: unknown,
): Promise<{ url: URL; flow: SignInFlow }> {
  const verifier = oidc.randomPKCECodeVerifier()
  const flow: SignInFlow = {
    verifier,
    state: oidc.randomState(),
    nonce: oidc.randomNonce(),
    returnTo: safeReturnTo(returnTo),
  }
  const url = oidc.buildAuthorizationUrl(configuration, {
    redirect_uri: settings.redirectUri,
    scope: 'openid',
    response_type: 'code',
    code_challenge: await oidc.calculatePKCECodeChallenge(verifier),
    code_challenge_method: 'S256',
    state: flow.state,
    nonce: flow.nonce,
  })
  return { url, flow }
}

/**
 * Exchanges the code of the callback for tokens, checking state, nonce, the issuer and PKCE. The callback address is
 * rebuilt from the registered redirect URI, not from the request's host, which a proxy may rewrite.
 */
export async function finishSignIn(
  configuration: oidc.Configuration,
  settings: OidcSettings,
  flow: SignInFlow,
  callbackQuery: string,
  now: number,
): Promise<StoredSession> {
  const callback = new URL(settings.redirectUri)
  callback.search = callbackQuery
  const tokens = await oidc.authorizationCodeGrant(configuration, callback, {
    pkceCodeVerifier: flow.verifier,
    expectedState: flow.state,
    expectedNonce: flow.nonce,
    idTokenExpected: true,
  })
  const claims = tokens.claims()
  if (!claims?.sub) throw new Error('The ID token has no subject')
  return {
    accountId: claims.sub,
    accessToken: tokens.access_token,
    expiresAt: now + (tokens.expires_in ?? 900) * 1000,
  }
}

let discovered: { key: string; configuration: Promise<oidc.Configuration> } | undefined

/** The provider's metadata from its discovery document, fetched once per process and settings. */
export function providerConfiguration(settings: OidcSettings): Promise<oidc.Configuration> {
  const key = `${settings.issuer} ${settings.clientId}`
  if (discovered?.key !== key) {
    const insecure = new URL(settings.issuer).protocol === 'http:'
    const configuration = oidc
      .discovery(new URL(settings.issuer), settings.clientId, undefined, oidc.None(), {
        // Plain HTTP only for a local provider (development, the e2e fake); the stands are HTTPS.
        execute: insecure ? [oidc.allowInsecureRequests] : [],
      })
      .catch((error: unknown) => {
        discovered = undefined
        throw error
      })
    discovered = { key, configuration }
  }
  return discovered.configuration
}

function isHttpUrl(value: string): boolean {
  try {
    const url = new URL(value)
    return url.protocol === 'https:' || url.protocol === 'http:'
  } catch {
    return false
  }
}
