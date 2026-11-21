// The password after registration (E03-05, FR-AUTH, ADR-0036). The reset is anonymous: the browser calls the API on
// the provider's origin, as the sign-in does, so the answer to the code and the new password sets the provider's
// session cookie there and the site then signs in through /auth/sign-in. The change in the settings needs the
// person's access token, which only the site's server holds (ADR-0018), so the browser goes through /bff/password.
import type { ApiClient } from './client'
import { outcome, type RegistrationOutcome } from './registration'

export type PasswordOutcome = RegistrationOutcome

export interface PasswordResetApi {
  request(email: string): Promise<PasswordOutcome>
  confirm(resetId: string, code: string, password: string): Promise<PasswordOutcome>
}

export interface PasswordChangeApi {
  change(currentPassword: string, newPassword: string): Promise<PasswordOutcome>
}

/** The reset calls over the typed API client; `credentials: 'include'` lets the answer set the cookie. */
export function passwordResetApi(client: ApiClient): PasswordResetApi {
  return {
    request: (email) =>
      outcome(() => client.POST('/api/v1/password-reset', { body: { email }, credentials: 'include' })),
    confirm: (resetId, code, password) =>
      outcome(() =>
        client.POST('/api/v1/password-reset/{resetId}/confirmation', {
          params: { path: { resetId } },
          body: { code, password },
          credentials: 'include',
        }),
      ),
  }
}

/** PUT /api/v1/account/password with the person's token: the site's server makes the call (server/routes/bff). */
export async function changeOwnPassword(
  client: ApiClient,
  currentPassword: string | null,
  newPassword: string | null,
): Promise<PasswordOutcome> {
  return outcome(() => client.PUT('/api/v1/account/password', { body: { currentPassword, newPassword } }))
}

/** The change as the browser makes it: through the site's own server, which answers with the API's status. */
export function passwordChangeApi(): PasswordChangeApi {
  return {
    change: async (currentPassword, newPassword) => {
      try {
        const response = await $fetch.raw('/bff/password', {
          method: 'PUT',
          body: { currentPassword, newPassword },
          ignoreResponseError: true,
        })
        return { status: response.status, body: response._data }
      } catch {
        return { status: 0 }
      }
    },
  }
}
