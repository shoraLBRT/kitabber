// A page of the notification feed (E12-02, ADR-0093): GET /api/v1/notifications/me?cursor= with the person's access
// token (ADR-0018), answered with the API's status and body. No session: 401.
import { ownNotifications } from '~/api/notifications'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const cursor = getQuery(event).cursor
  const result = await ownNotifications(ownApiClient(event)).load(typeof cursor === 'string' ? cursor : null)
  setResponseStatus(event, result.status === 0 ? 502 : result.status)
  return result.body ?? null
})
