// The settings of the signed-in account that every device applies (E04-08, ADR-0070): for now the colour theme of
// «Основные». They need the person's access token, which only the site's server holds (ADR-0018), so the server calls
// the API (server/routes/bff/preferences) and the browser asks the server.
import type { ApiClient, components } from './client'
import type { ThemeChoice } from '../composables/useTheme'

export type Preferences = components['schemas']['PreferencesView']

/** What the browser uses: the saved settings (null when they could not be read) and the save of a section. */
export interface PreferencesApi {
  load(): Promise<Preferences | null>
  saveGeneral(theme: ThemeChoice): Promise<boolean>
}

/** GET /api/v1/preferences/me with the person's token; null on a failure. */
export async function ownPreferences(client: ApiClient): Promise<Preferences | null> {
  try {
    const { data } = await client.GET('/api/v1/preferences/me')
    return data ?? null
  } catch {
    return null
  }
}

/** PUT /api/v1/preferences/me/general with the person's token: the API's status, 0 when it could not be reached. */
export async function saveOwnGeneral(client: ApiClient, theme: string | null): Promise<number> {
  try {
    const { response } = await client.PUT('/api/v1/preferences/me/general', { body: { theme } })
    return response.status
  } catch {
    return 0
  }
}

/** The calls as the browser makes them: through the site's own server. */
export function preferencesApi(): PreferencesApi {
  return {
    load: () => $fetch<Preferences>('/bff/preferences').catch(() => null),
    saveGeneral: (theme) =>
      $fetch('/bff/preferences/general', { method: 'PUT', body: { theme } }).then(
        () => true,
        () => false,
      ),
  }
}

/**
 * PUT /api/v1/preferences/me/feeds with the person's token (E08-07, ADR-0106): the rating filter of «Свежее», `null`
 * for every post. The API's status, 0 when it could not be reached.
 */
export async function saveOwnFeeds(client: ApiClient, freshMinRating: number | null): Promise<number> {
  try {
    const { response } = await client.PUT('/api/v1/preferences/me/feeds', { body: { freshMinRating } })
    return response.status
  } catch {
    return 0
  }
}
