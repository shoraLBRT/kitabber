// The settings of the signed-in account (E04-08, ADR-0070) for the browser: the server asks the API for
// /preferences/me with the person's access token (ADR-0018). 401 without a session, 502 when the API fails.
import { ownPreferences } from '~/api/preferences'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const preferences = await ownPreferences(ownApiClient(event))
  if (!preferences) throw createError({ statusCode: 502, message: 'The settings are unavailable' })
  return preferences
})
