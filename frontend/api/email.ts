// The account's email in the settings (E03-06, FR-AUTH, ADR-0020): read it, send a code to a new address, confirm the
// address with the code. A confirmed email stays until the code from the new address is entered, and the old address
// then gets a letter about the change. The calls need the person's access token, which only the site's server holds
// (ADR-0018), so the browser goes through /bff/email (server/routes/bff/email*.ts).
import type { ApiClient } from './client'
import { outcome, type RegistrationOutcome } from './registration'

export type EmailOutcome = RegistrationOutcome

/** GET /api/v1/account/email. */
export interface AccountEmailState {
  email: string | null
  confirmedAt: string | null
  pendingEmail: string | null
}

export interface EmailChangeApi {
  load(): Promise<EmailOutcome>
  change(email: string): Promise<EmailOutcome>
  confirm(code: string): Promise<EmailOutcome>
}

/** The API calls with the person's token: the site's server makes them (server/routes/bff). */
export function ownEmail(client: ApiClient) {
  return {
    load: () => outcome(() => client.GET('/api/v1/account/email')),
    change: (email: string | null) => outcome(() => client.PUT('/api/v1/account/email', { body: { email } })),
    confirm: (code: string | null) =>
      outcome(() => client.POST('/api/v1/account/email/confirmation', { body: { code } })),
  }
}

async function bff(path: string, method: 'GET' | 'PUT' | 'POST', body?: object): Promise<EmailOutcome> {
  try {
    const response = await $fetch.raw(path, { method, body, ignoreResponseError: true })
    return { status: response.status, body: response._data }
  } catch {
    return { status: 0 }
  }
}

/** The calls as the browser makes them: through the site's own server, which answers with the API's status. */
export function emailChangeApi(): EmailChangeApi {
  return {
    load: () => bff('/bff/email', 'GET'),
    change: (email) => bff('/bff/email', 'PUT', { email }),
    confirm: (code) => bff('/bff/email-confirmation', 'POST', { code }),
  }
}
