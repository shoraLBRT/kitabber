// Setting or moving the cover of a topic from its management page (E11-12, ADR-0121): the browser sends the topic, the
// media id of the uploaded picture and its position, the server sends them on to PUT /api/v1/topics/{id}/cover with
// the person's access token (ADR-0018).
import { saveTopicCover } from '~/api/topic-management'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const id = getQuery(event).id
  if (typeof id !== 'string') throw createError({ statusCode: 400, message: 'The topic is missing' })
  const body = (await readBody<{ mediaId?: unknown; position?: unknown }>(event)) ?? {}
  return saveTopicCover(ownApiClient(event), id, {
    mediaId: typeof body.mediaId === 'string' ? body.mediaId : '',
    position: typeof body.position === 'number' ? body.position : 50,
  })
})
