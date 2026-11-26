// The devices of the account for the settings (E03-09, ADR-0078): GET /api/v1/account/sessions with the person's
// access token (ADR-0018), answered with the API's status and body. No session: 401.
import { ownSessions } from '~/api/sessions'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const result = await ownSessions(ownApiClient(event)).load()
  setResponseStatus(event, result.status === 0 ? 502 : result.status)
  return result.body ?? null
})
