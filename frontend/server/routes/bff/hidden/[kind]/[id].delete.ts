// Bringing a hidden post or topic back to the feeds (E11-07, ADR-0119): DELETE
// /api/v1/preferences/me/hidden/{posts|topics}/{id} with the person's access token (ADR-0018), answered with the API's
// status. No session: 401.
import { isHiddenKind, ownHidden } from '~/api/hidden'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const kind = getRouterParam(event, 'kind')
  if (!isHiddenKind(kind)) throw createError({ statusCode: 404, statusMessage: 'Not Found' })
  const result = await ownHidden(ownApiClient(event)).show(kind, getRouterParam(event, 'id') ?? '')
  setResponseStatus(event, result.status === 0 ? 502 : result.status)
  return result.body ?? null
})
