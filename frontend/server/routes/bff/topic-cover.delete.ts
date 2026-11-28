// Removing the cover of a topic from its management page (E11-12) through DELETE /api/v1/topics/{id}/cover with the
// person's access token (ADR-0018).
import { removeTopicCover } from '~/api/topic-management'

export default defineEventHandler((event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const id = getQuery(event).id
  if (typeof id !== 'string') throw createError({ statusCode: 400, message: 'The topic is missing' })
  return removeTopicCover(ownApiClient(event), id)
})
