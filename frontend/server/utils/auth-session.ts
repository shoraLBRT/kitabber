// The person's session on the Nuxt server (BFF, ADR-0018, ADR-0024): the tokens stay in server storage under a random
// id — Redis on the stands (server/plugins/auth-storage.ts), so all instances share sessions and a restart signs nobody
// out — and the browser holds only that id in an encrypted, HttpOnly, SameSite=Lax cookie. The access token is renewed
// by the refresh token before it expires; the cookie lives as long as the refresh token and is renewed with it.
import { randomBytes } from 'node:crypto'
import {
  flowCookie,
  flowMaxAgeSeconds,
  providerConfiguration,
  readOidcSettings,
  refreshSession,
  sessionCookie,
  type SignInFlow,
  type StoredSession,
} from './oidc'
import { SessionStore } from './session-store'

// h3 is Nitro's own dependency; its types are taken from the auto-imported functions.
type H3Event = Parameters<typeof getCookie>[0]
type SessionConfig = Parameters<typeof useSession>[1]

export const sessionStorageMount = 'auth-sessions'

const store = new SessionStore(
  () => useStorage<StoredSession>(sessionStorageMount),
  async (stored) => refreshSession(await providerConfiguration(oidcSettings()), stored, Date.now()),
)

let generatedPassword: string | undefined

/** The key that encrypts the cookies (NUXT_SESSION_PASSWORD, at least 32 characters). */
function password(): string {
  const configured = useRuntimeConfig().sessionPassword
  if (typeof configured === 'string' && configured.length >= 32) return configured
  if (import.meta.dev) return (generatedPassword ??= randomBytes(32).toString('base64url'))
  throw createError({
    statusCode: 500,
    message: 'NUXT_SESSION_PASSWORD must be set (at least 32 characters)',
  })
}

function cookieConfig(name: string, maxAge: number | undefined): SessionConfig {
  const secure = new URL(oidcSettings().redirectUri).protocol === 'https:'
  return {
    name,
    password: password(),
    maxAge,
    cookie: { httpOnly: true, secure, sameSite: 'lax', path: '/' },
  }
}

export function oidcSettings() {
  return readOidcSettings(useRuntimeConfig().oidc)
}

export async function rememberFlow(event: H3Event, flow: SignInFlow): Promise<void> {
  const session = await useSession<{ flow?: SignInFlow }>(event, cookieConfig(flowCookie, flowMaxAgeSeconds))
  await session.update({ flow })
}

/** The flow of this browser, once: it is cleared as it is read, so a callback cannot be replayed. */
export async function takeFlow(event: H3Event): Promise<SignInFlow | undefined> {
  if (!getCookie(event, flowCookie)) return undefined
  const session = await useSession<{ flow?: SignInFlow }>(event, cookieConfig(flowCookie, flowMaxAgeSeconds))
  const flow = session.data.flow
  await session.clear()
  return flow
}

function cookieMaxAge(stored: StoredSession): number {
  return Math.max(1, Math.floor((stored.expiresAt - Date.now()) / 1000))
}

async function sessionId(event: H3Event): Promise<string | undefined> {
  if (!getCookie(event, sessionCookie)) return undefined
  const session = await useSession<{ sid?: string }>(event, cookieConfig(sessionCookie, undefined))
  return session.data.sid
}

async function writeCookie(event: H3Event, id: string, stored: StoredSession): Promise<void> {
  const session = await useSession<{ sid?: string }>(event, cookieConfig(sessionCookie, cookieMaxAge(stored)))
  await session.update({ sid: id })
}

export async function startSession(event: H3Event, stored: StoredSession): Promise<void> {
  const id = randomBytes(32).toString('base64url')
  await store.save(id, stored)
  await writeCookie(event, id, stored)
}

/**
 * The session of the request, or null; its access token is renewed if it is about to expire, and a session the
 * provider no longer accepts is removed. Without the cookie no session is created.
 */
export async function readSession(event: H3Event): Promise<StoredSession | null> {
  const id = await sessionId(event)
  if (!id) return null
  const result = await store.read(id)
  if (result?.renewed) await writeCookie(event, id, result.session)
  return result?.session ?? null
}

/** Renews the request's tokens now: the API has refused the access token. The renewed session, or null. */
export async function renewSession(event: H3Event): Promise<StoredSession | null> {
  const id = await sessionId(event)
  if (!id) return null
  const result = await store.renewNow(id, event.context.auth?.accessToken ?? '')
  if (result?.renewed) await writeCookie(event, id, result.session)
  event.context.auth = result?.session ?? null
  return event.context.auth
}

/** Ends the request's session on the site: removes it from storage and the cookie. Its tokens, for the provider. */
export async function endSession(event: H3Event): Promise<StoredSession | null> {
  const id = await sessionId(event)
  const stored = id ? await useStorage<StoredSession>(sessionStorageMount).getItem(id) : null
  if (id) await store.remove(id)
  deleteCookie(event, sessionCookie, { path: '/' })
  event.context.auth = null
  return stored
}
