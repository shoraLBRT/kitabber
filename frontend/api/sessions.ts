// The devices of the account in the settings (E03-09, FR-AUTH, ADR-0078): every sign-in is a device with the time it
// was last active; one can be signed out, or every one but this («Выйти со всех устройств»). The calls need the
// person's access token, which only the site's server holds (ADR-0018), so the browser goes through /bff/sessions
// (server/routes/bff/sessions*.ts).
import type { ApiClient } from './client'
import { outcome, type RegistrationOutcome } from './registration'
import type { components } from './schema'

export type SessionsOutcome = RegistrationOutcome

/** GET /api/v1/account/sessions: one device. */
export type AccountSession = components['schemas']['AccountSessionView']

export interface SessionsApi {
  load(): Promise<SessionsOutcome>
  end(id: string): Promise<SessionsOutcome>
  endOthers(): Promise<SessionsOutcome>
}

/** The API calls with the person's token: the site's server makes them (server/routes/bff). */
export function ownSessions(client: ApiClient) {
  return {
    load: () => outcome(() => client.GET('/api/v1/account/sessions')),
    end: (sessionId: string) =>
      outcome(() =>
        client.DELETE('/api/v1/account/sessions/{sessionId}', { params: { path: { sessionId } } }),
      ),
    endOthers: () => outcome(() => client.POST('/api/v1/account/sessions/sign-out-others')),
  }
}

async function bff(path: string, method: 'GET' | 'DELETE' | 'POST'): Promise<SessionsOutcome> {
  try {
    const response = await $fetch.raw(path, { method, ignoreResponseError: true })
    return { status: response.status, body: response._data }
  } catch {
    return { status: 0 }
  }
}

/** The calls as the browser makes them: through the site's own server, which answers with the API's status. */
export function sessionsApi(): SessionsApi {
  return {
    load: () => bff('/bff/sessions', 'GET'),
    end: (id) => bff(`/bff/sessions/${encodeURIComponent(id)}`, 'DELETE'),
    endOthers: () => bff('/bff/sessions/sign-out-others', 'POST'),
  }
}
