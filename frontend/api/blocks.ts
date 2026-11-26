// The black list of the account (E11-06, FR-SUB, ADR-0091): whom the person blocked, read and changed in the settings.
// The calls need the person's access token, which only the site's server holds (ADR-0018), so the browser goes through
// /bff/blocks (server/routes/bff/blocks*.ts). The names and avatars come from the public profiles.
import type { ApiClient } from './client'
import { outcome, type RegistrationOutcome } from './registration'
import type { components } from './schema'

export type BlocksOutcome = RegistrationOutcome

/** GET /api/v1/preferences/me/blocks: the list, the latest blocked first. */
export type BlockList = components['schemas']['BlockListView']

/** One blocked account. */
export type BlockedAccount = components['schemas']['BlockedAccountView']

export interface BlocksApi {
  load(): Promise<BlocksOutcome>
  block(accountId: string): Promise<BlocksOutcome>
  unblock(accountId: string): Promise<BlocksOutcome>
}

/** The API calls with the person's token: the site's server makes them (server/routes/bff). */
export function ownBlocks(client: ApiClient) {
  return {
    load: () => outcome(() => client.GET('/api/v1/preferences/me/blocks')),
    block: (accountId: string) =>
      outcome(() =>
        client.PUT('/api/v1/preferences/me/blocks/{accountId}', { params: { path: { accountId } } }),
      ),
    unblock: (accountId: string) =>
      outcome(() =>
        client.DELETE('/api/v1/preferences/me/blocks/{accountId}', { params: { path: { accountId } } }),
      ),
  }
}

async function bff(path: string, method: 'GET' | 'PUT' | 'DELETE'): Promise<BlocksOutcome> {
  try {
    const response = await $fetch.raw(path, { method, ignoreResponseError: true })
    return { status: response.status, body: response._data }
  } catch {
    return { status: 0 }
  }
}

/** The calls as the browser makes them: through the site's own server, which answers with the API's status. */
export function blocksApi(): BlocksApi {
  return {
    load: () => bff('/bff/blocks', 'GET'),
    block: (id) => bff(`/bff/blocks/${encodeURIComponent(id)}`, 'PUT'),
    unblock: (id) => bff(`/bff/blocks/${encodeURIComponent(id)}`, 'DELETE'),
  }
}
