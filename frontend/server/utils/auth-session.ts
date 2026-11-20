// The person's session on the Nuxt server (BFF, ADR-0018): the tokens stay in server storage under a random id, the
// browser holds only that id in an encrypted, HttpOnly, SameSite=Lax cookie. Server storage is in memory for now:
// a restart signs everyone out of the site (not out of the provider), sessions shared between instances and refresh
// tokens come with #29.
import { randomBytes } from 'node:crypto'
import {
  flowCookie,
  flowMaxAgeSeconds,
  readOidcSettings,
  sessionCookie,
  type SignInFlow,
  type StoredSession,
} from './oidc'

// h3 is Nitro's own dependency; its types are taken from the auto-imported functions.
type H3Event = Parameters<typeof getCookie>[0]
type SessionConfig = Parameters<typeof useSession>[1]

const storage = () => useStorage<StoredSession>('auth-sessions')

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

export async function startSession(event: H3Event, stored: StoredSession): Promise<void> {
  const id = randomBytes(32).toString('base64url')
  await storage().setItem(id, stored)
  const maxAge = Math.max(1, Math.floor((stored.expiresAt - Date.now()) / 1000))
  const session = await useSession<{ sid?: string }>(event, cookieConfig(sessionCookie, maxAge))
  await session.update({ sid: id })
}

/** The session of the request, or null; an expired one is removed. Without the cookie no session is created. */
export async function readSession(event: H3Event): Promise<StoredSession | null> {
  if (!getCookie(event, sessionCookie)) return null
  const session = await useSession<{ sid?: string }>(event, cookieConfig(sessionCookie, undefined))
  const id = session.data.sid
  const stored = id ? await storage().getItem(id) : null
  if (stored && stored.expiresAt > Date.now()) return stored
  if (id) await storage().removeItem(id)
  return null
}
