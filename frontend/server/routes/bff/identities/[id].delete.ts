// Unlinking an external provider from the settings (#91, ADR-0098): DELETE /api/v1/account/identities/{id} with the
// person's access token (ADR-0018), answered with the API's status and problem (409 last_sign_in_method). No session:
// 401.
import { ownIdentities } from '~/api/identities'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const result = await ownIdentities(ownApiClient(event)).unlink(getRouterParam(event, 'id') ?? '')
  setResponseStatus(event, result.status === 0 ? 502 : result.status)
  return result.body ?? null
})
