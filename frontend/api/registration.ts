// Registration by email and password (E03-01, FR-AUTH): the three calls of /api/v1/registration as plain outcomes the
// form understands. The browser calls the API on the provider's origin (the same origin behind the stand's proxy), so
// the confirmation's answer sets the provider's session cookie there; the site then signs in through /auth/sign-in,
// the usual authorization code flow (ADR-0018).
import type { ApiClient } from './client'

/** What one call ended with: the HTTP status (0 when the API was not reached), the body and the Retry-After seconds. */
export interface RegistrationOutcome {
  status: number
  body?: unknown
  retryAfter?: number
}

export interface RegistrationApi {
  register(email: string, password: string): Promise<RegistrationOutcome>
  resend(registrationId: string): Promise<RegistrationOutcome>
  confirm(registrationId: string, code: string): Promise<RegistrationOutcome>
}

/** The password rule of the API (FR-AUTH): at least 6 characters. */
export const passwordMinLength = 6

/** Seconds of a Retry-After header, or undefined. */
export function retryAfterSeconds(response: Response | undefined): number | undefined {
  const value = Number(response?.headers.get('retry-after'))
  return Number.isFinite(value) && value > 0 ? Math.ceil(value) : undefined
}

/** "0:45", "1:00": the time left on the resend timer. */
export function formatCountdown(seconds: number): string {
  const s = Math.max(0, Math.ceil(seconds))
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`
}

export async function outcome(call: () => Promise<{ data?: unknown; error?: unknown; response: Response }>) {
  try {
    const { data, error, response } = await call()
    return { status: response.status, body: data ?? error, retryAfter: retryAfterSeconds(response) }
  } catch {
    return { status: 0 }
  }
}

/** The registration calls over the typed API client; `credentials: 'include'` lets the answer set the cookie. */
export function registrationApi(client: ApiClient): RegistrationApi {
  return {
    register: (email, password) =>
      outcome(() =>
        client.POST('/api/v1/registration', { body: { email, password }, credentials: 'include' }),
      ),
    resend: (registrationId) =>
      outcome(() =>
        client.POST('/api/v1/registration/{registrationId}/code', {
          params: { path: { registrationId } },
          credentials: 'include',
        }),
      ),
    confirm: (registrationId, code) =>
      outcome(() =>
        client.POST('/api/v1/registration/{registrationId}/confirmation', {
          params: { path: { registrationId } },
          body: { code },
          credentials: 'include',
        }),
      ),
  }
}
