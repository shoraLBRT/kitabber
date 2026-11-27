// The external providers linked to the account, for the settings (#91, ADR-0098): GET /api/v1/account/identities with
// the person's access token (ADR-0018), answered with the API's status and body. No session: 401.
import { ownIdentities } from '~/api/identities'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const result = await ownIdentities(ownApiClient(event)).load()
  setResponseStatus(event, result.status === 0 ? 502 : result.status)
  return result.body ?? null
})
