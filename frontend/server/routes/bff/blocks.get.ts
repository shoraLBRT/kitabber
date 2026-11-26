// The black list for the settings (E11-06, ADR-0091): GET /api/v1/preferences/me/blocks with the person's access token
// (ADR-0018), answered with the API's status and body. No session: 401.
import { ownBlocks } from '~/api/blocks'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const result = await ownBlocks(ownApiClient(event)).load()
  setResponseStatus(event, result.status === 0 ? 502 : result.status)
  return result.body ?? null
})
