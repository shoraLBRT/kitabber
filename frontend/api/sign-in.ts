// Signing in by email (E03-02, FR-AUTH): the calls of /api/v1/sign-in as plain outcomes the form understands. As with
// the registration, the browser calls the API on the provider's origin, so a successful answer sets the provider's
// session cookie there and the site then signs in through /auth/sign-in, the usual authorization code flow (ADR-0018).
import type { ApiClient } from './client'
import { outcome, type RegistrationOutcome } from './registration'

export type SignInOutcome = RegistrationOutcome

export interface SignInApi {
  withPassword(email: string, password: string): Promise<SignInOutcome>
  requestCode(email: string): Promise<SignInOutcome>
  withCode(challengeId: string, code: string): Promise<SignInOutcome>
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
    withPassword: (email, password) =>
      outcome(() =>
        client.POST('/api/v1/sign-in/password', { body: { email, password }, credentials: 'include' }),
      ),
    requestCode: (email) =>
      outcome(() => client.POST('/api/v1/sign-in/code', { body: { email }, credentials: 'include' })),
    withCode: (challengeId, code) =>
      outcome(() =>
        client.POST('/api/v1/sign-in/code/{challengeId}/confirmation', {
          params: { path: { challengeId } },
          body: { code },
          credentials: 'include',
        }),
      ),
  }
}
