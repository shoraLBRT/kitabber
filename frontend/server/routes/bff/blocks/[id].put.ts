// Blocking an account (E11-06, ADR-0091): PUT /api/v1/preferences/me/blocks/{id} with the person's access token
// (ADR-0018), answered with the API's status and problem. No session: 401.
import { ownBlocks } from '~/api/blocks'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const result = await ownBlocks(ownApiClient(event)).block(getRouterParam(event, 'id') ?? '')
  setResponseStatus(event, result.status === 0 ? 502 : result.status)
  return result.body ?? null
})
