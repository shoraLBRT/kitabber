// «Прочитать все» (E12-03, ADR-0122): POST /api/v1/notifications/me/read-all with the person's access token (ADR-0018),
// answered with the API's status and the new unread number. No session: 401.
import { ownNotifications } from '~/api/notifications'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const result = await ownNotifications(ownApiClient(event)).markAllRead()
  setResponseStatus(event, result.status === 0 ? 502 : result.status)
  return result.body ?? null
})
