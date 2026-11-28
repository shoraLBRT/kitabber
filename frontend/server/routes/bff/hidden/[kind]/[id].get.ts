// Whether the person hid a topic (E11-07, ADR-0119): GET /bff/hidden/topics/{id} → GET
// /api/v1/preferences/me/hidden/topics/{id} with the person's access token (ADR-0018), answered with the API's status
// and body. No session: 401. Only topics have a state; the segment is shared with PUT and DELETE, which take posts too.
import { ownHidden } from '~/api/hidden'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  if (getRouterParam(event, 'kind') !== 'topics')
    throw createError({ statusCode: 404, statusMessage: 'Not Found' })
  const result = await ownHidden(ownApiClient(event)).topicState(getRouterParam(event, 'id') ?? '')
  setResponseStatus(event, result.status === 0 ? 502 : result.status)
  return result.body ?? null
})
