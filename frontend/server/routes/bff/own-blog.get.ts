// The person's own blog (E04-02): «Профиль» of the menu leads here through pages/profile.vue. The server asks the API
// for /profiles/me with the person's access token (ADR-0018; the call creates the blog when it is missing, ADR-0025)
// and redirects to the blog's address. A guest signs in first and comes back here.
import { createApiClient } from '~/api/client'
import { blogPath, ownProfile } from '~/api/profiles'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const session = event.context.auth
  if (!session) return sendRedirect(event, '/auth/sign-in?returnTo=/profile', 302)
  const client = createApiClient({
    baseUrl: useRuntimeConfig().apiBase,
    headers: { authorization: `Bearer ${session.accessToken}` },
  })
  const profile = await ownProfile(client)
  if (!profile) throw createError({ statusCode: 502, message: 'The own blog is unavailable' })
  return sendRedirect(event, blogPath(profile), 302)
})
