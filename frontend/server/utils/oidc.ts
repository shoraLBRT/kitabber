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
  /** Unix milliseconds: when the access token expires; it is renewed a little before (ADR-0024). */
  accessExpiresAt: number
  /** Renews the access token (scope offline_access); rotated by every refresh. */
  refreshToken?: string
  /** The latest ID token: the hint of the sign-out at the provider. */
  idToken?: string
  /** Unix milliseconds: the session ends with the refresh token, or with the access token without one. */
  expiresAt: number
}

/**
 * How long a session lives without a visit (ADR-0024): the refresh token's lifetime at the provider, started again by
 * every refresh.
 */
export const sessionLifetimeMs = 30 * 24 * 60 * 60 * 1000

/** The access token is renewed when less than this is left of it, so no request reaches the API with an expired one. */
export const refreshMarginMs = 30 * 1000

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
    // offline_access: a refresh token, so the session outlives the 15-minute access token (ADR-0024).
    scope: 'openid offline_access',
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
  return sessionOf(claims.sub, tokens, undefined, now)
}

function sessionOf(
  accountId: string,
  tokens: oidc.TokenEndpointResponse,
  previous: StoredSession | undefined,
  now: number,
): StoredSession {
  const accessExpiresAt = now + (tokens.expires_in ?? 900) * 1000
  const refreshToken = tokens.refresh_token ?? previous?.refreshToken
  return {
    accountId,
    accessToken: tokens.access_token,
    accessExpiresAt,
    refreshToken,
    idToken: tokens.id_token ?? previous?.idToken,
    expiresAt: refreshToken ? now + sessionLifetimeMs : accessExpiresAt,
  }
}

/** Whether the access token is about to expire and can be renewed. */
export function needsRefresh(stored: StoredSession, now: number): boolean {
  return !!stored.refreshToken && stored.accessExpiresAt - now < refreshMarginMs
}

/**
 * Renews the tokens with the refresh token (ADR-0024). `null` when the provider refuses it — revoked, expired, or a
 * whole chain revoked because a rotated token was reused: the session is over. Other failures (the provider is down)
 * throw, and the session is kept for the next request.
 */
export async function refreshSession(
  configuration: oidc.Configuration,
  stored: StoredSession,
  now: number,
): Promise<StoredSession | null> {
  if (!stored.refreshToken) return null
  try {
    const tokens = await oidc.refreshTokenGrant(configuration, stored.refreshToken)
    // A refreshed ID token must name the same person; a session never changes hands.
    const sub = tokens.claims()?.sub
    if (sub && sub !== stored.accountId) return null
    return sessionOf(stored.accountId, tokens, stored, now)
  } catch (error) {
    if (error instanceof oidc.ResponseBodyError && error.error === 'invalid_grant') return null
    throw error
  }
}

/** Revokes the refresh token at the provider (RFC 7009); a failure does not stop the sign-out. */
export async function revokeSession(
  configuration: oidc.Configuration,
  stored: StoredSession,
): Promise<boolean> {
  if (!stored.refreshToken || !configuration.serverMetadata().revocation_endpoint) return false
  try {
    await oidc.tokenRevocation(configuration, stored.refreshToken, { token_type_hint: 'refresh_token' })
    return true
  } catch {
    return false
  }
}

/**
 * Where the browser goes to end the person's session at the provider (RP-initiated logout, ADR-0024), coming back to
 * the site's home page; the home page itself when the provider has no end-session endpoint.
 */
export function endSessionUrl(
  configuration: oidc.Configuration,
  settings: OidcSettings,
  idToken: string | undefined,
): string {
  const home = new URL('/', settings.redirectUri).href
  if (!configuration.serverMetadata().end_session_endpoint) return '/'
  return oidc.buildEndSessionUrl(configuration, {
    post_logout_redirect_uri: home,
    ...(idToken ? { id_token_hint: idToken } : {}),
  }).href
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
