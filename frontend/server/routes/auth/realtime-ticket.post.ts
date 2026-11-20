// A ticket for the realtime hub (E01-11, ADR-0019): the browser asks here, the server asks the API with the
// person's access token, which never leaves the server (ADR-0018). 401 without a session or when the API refuses
// the token: the page stops reconnecting until the person signs in again.
import { createApiClient } from '~/api/client'
import { requestRealtimeTicket } from '~/api/realtime'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const session = event.context.auth
  if (!session) throw createError({ statusCode: 401, message: 'Not signed in' })
  const client = createApiClient({
    baseUrl: useRuntimeConfig().apiBase,
    headers: { authorization: `Bearer ${session.accessToken}` },
  })
  const ticket = await requestRealtimeTicket(client)
  if (!ticket) throw createError({ statusCode: 401, message: 'Not signed in' })
  return ticket
})
