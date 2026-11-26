// Marking the shown notifications read (E12-02, ADR-0093): POST /api/v1/notifications/me/read with the person's access
// token (ADR-0018), answered with the API's status and the new unread number or the problem. No session: 401.
import { ownNotifications } from '~/api/notifications'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const body = await readBody<{ ids?: unknown }>(event).catch(() => null)
  const ids = Array.isArray(body?.ids) ? body.ids.filter((id): id is string => typeof id === 'string') : []
  const result = await ownNotifications(ownApiClient(event)).markRead(ids)
  setResponseStatus(event, result.status === 0 ? 502 : result.status)
  return result.body ?? null
})
