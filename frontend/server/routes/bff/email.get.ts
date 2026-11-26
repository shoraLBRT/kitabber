// The account's email for the settings (E03-06, ADR-0020): GET /api/v1/account/email with the person's access token
// (ADR-0018), answered with the API's status and body. No session: 401.
import { ownEmail } from '~/api/email'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const result = await ownEmail(ownApiClient(event)).load()
  setResponseStatus(event, result.status === 0 ? 502 : result.status)
  return result.body ?? null
})
