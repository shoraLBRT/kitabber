// Signing in by email (E03-02, FR-AUTH): the calls of /api/v1/sign-in as plain outcomes the form understands. As with
// the registration, the browser calls the API on the provider's origin, so a successful answer sets the provider's
// session cookie there and the site then signs in through /auth/sign-in, the usual authorization code flow (ADR-0018).
import type { ApiClient } from './client'
import { solveCaptcha } from './captcha'
import { outcome, type RegistrationOutcome } from './registration'

export type SignInOutcome = RegistrationOutcome

export interface SignInApi {
  withPassword(email: string, password: string, captcha?: string): Promise<SignInOutcome>
  requestCode(email: string): Promise<SignInOutcome>
  withCode(challengeId: string, code: string, captcha?: string): Promise<SignInOutcome>
  /** Fetches a captcha challenge and solves it (E03-12): the solution, or `undefined` when that failed. */
  captcha(): Promise<string | undefined>
}

/**
 * Where to go once the provider has signed the person in: back into the authorization request that sent them to the
 * sign-in page (the provider's `ReturnUrl`, only its own /connect/authorize), otherwise the site's own sign-in.
 */
export function afterSignIn(returnUrl: unknown): string {
  return typeof returnUrl === 'string' && /^\/connect\/authorize(\?|$)/.test(returnUrl)
    ? returnUrl
    : '/auth/sign-in?returnTo=/'
}

/** The sign-in calls over the typed API client; `credentials: 'include'` lets the answer set the cookie. */
export function signInApi(client: ApiClient): SignInApi {
  return {
    withPassword: (email, password, captcha) =>
      outcome(() =>
        client.POST('/api/v1/sign-in/password', {
          body: { email, password, captcha },
          credentials: 'include',
        }),
      ),
    requestCode: (email) =>
      outcome(() => client.POST('/api/v1/sign-in/code', { body: { email }, credentials: 'include' })),
    withCode: (challengeId, code, captcha) =>
      outcome(() =>
        client.POST('/api/v1/sign-in/code/{challengeId}/confirmation', {
          params: { path: { challengeId } },
          body: { code, captcha },
          credentials: 'include',
        }),
      ),
    captcha: async () => {
      try {
        const { data } = await client.GET('/api/v1/sign-in/captcha', { credentials: 'include' })
        return data ? await solveCaptcha(data) : undefined
      } catch {
        return undefined
      }
    },
  }
}
