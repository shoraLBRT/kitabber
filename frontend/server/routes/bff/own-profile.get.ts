// The person's own profile for the blog settings (E04-03) when the page opens in the browser: the server asks the
// API for /profiles/me with the person's access token (ADR-0018). 401 without a session, 502 when the API fails.
import { ownProfile } from '~/api/profiles'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const profile = await ownProfile(ownApiClient(event))
  if (!profile) throw createError({ statusCode: 502, message: 'The own profile is unavailable' })
  return profile
})
