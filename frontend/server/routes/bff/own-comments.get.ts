// Which comments under a post are the person's own (E09-05, ADR-0059): they take «Редактировать» and «Удалить». The
// server asks GET /posts/{postId}/comments/mine with the person's access token (ADR-0018); the page gets the ids only,
// never the account. A guest, or an API that fails, has none: the menu of the own comments is then not shown.
import { loadOwnComments } from '~/api/comments'

export default defineEventHandler(async (event): Promise<{ ids: string[] }> => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const postId = getQuery(event).postId
  if (typeof postId !== 'string' || postId === '')
    throw createError({ statusCode: 400, message: 'The post is missing' })
  if (!event.context.auth) return { ids: [] }
  return { ids: await loadOwnComments(ownApiClient(event), postId) }
})
