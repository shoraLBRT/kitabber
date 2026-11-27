// The external providers linked to the account (#119, FR-AUTH, ADR-0098, ADR-0115), for «Способы входа» in the
// settings: the list and the unlinking. Linking is the provider's sign-in itself (api/external-sign-in.ts, `link`).
// The calls need the person's access token, which only the site's server holds (ADR-0018), so the browser goes
// through /bff/identities (server/routes/bff/identities*.ts).
import type { ApiClient } from './client'
import { outcome, type RegistrationOutcome } from './registration'
import type { components } from './schema'

export type IdentitiesOutcome = RegistrationOutcome

/** GET /api/v1/account/identities: one linked provider. */
export type ExternalIdentity = components['schemas']['ExternalIdentityView']

export interface IdentitiesApi {
  load(): Promise<IdentitiesOutcome>
  unlink(id: string): Promise<IdentitiesOutcome>
}

/** The API calls with the person's token: the site's server makes them (server/routes/bff). */
export function ownIdentities(client: ApiClient) {
  return {
    load: () => outcome(() => client.GET('/api/v1/account/identities')),
    unlink: (identityId: string) =>
      outcome(() =>
        client.DELETE('/api/v1/account/identities/{identityId}', { params: { path: { identityId } } }),
      ),
  }
}

async function bff(path: string, method: 'GET' | 'DELETE'): Promise<IdentitiesOutcome> {
  try {
    const response = await $fetch.raw(path, { method, ignoreResponseError: true })
    return { status: response.status, body: response._data }
  } catch {
    return { status: 0 }
  }
}

/** The calls as the browser makes them: through the site's own server, which answers with the API's status. */
export function identitiesApi(): IdentitiesApi {
  return {
    load: () => bff('/bff/identities', 'GET'),
    unlink: (id) => bff(`/bff/identities/${encodeURIComponent(id)}`, 'DELETE'),
  }
}
