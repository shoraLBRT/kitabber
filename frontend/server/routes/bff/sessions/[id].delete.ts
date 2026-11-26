// Signing one device out from the settings (E03-09, ADR-0078): DELETE /api/v1/account/sessions/{id} with the person's
// access token (ADR-0018), answered with the API's status and problem. No session: 401.
import { ownSessions } from '~/api/sessions'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const result = await ownSessions(ownApiClient(event)).end(getRouterParam(event, 'id') ?? '')
  setResponseStatus(event, result.status === 0 ? 502 : result.status)
  return result.body ?? null
})
