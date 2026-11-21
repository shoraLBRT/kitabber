// The API client of a signed-in person's request to the site's own server (BFF, ADR-0018): the access token stays
// here and goes to the API as a bearer token, the client address goes on for the rate limits (ADR-0016). Throws 401
// when there is no session, so the browser sends the person to sign in.
import type { H3Event } from 'h3'
import { createApiClient, type ApiClient } from '~/api/client'
import { forwardedHeaders } from '~/api/forwarded'

export function ownApiClient(event: H3Event): ApiClient {
  const session = event.context.auth
  if (!session) throw createError({ statusCode: 401, message: 'Not signed in' })
  return createApiClient({
    baseUrl: useRuntimeConfig().apiBase,
    headers: {
      ...forwardedHeaders(getRequestHeader(event, 'x-forwarded-for')),
      authorization: `Bearer ${session.accessToken}`,
    },
  })
}
