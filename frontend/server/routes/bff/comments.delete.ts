// Deleting the person's own comment (E09-05, ADR-0059): the browser asks here, and the server deletes it with the
// person's access token (it never leaves the server, ADR-0018). The API's status comes back as is: 204, 401, 403 or 404.
import { deleteComment } from '~/api/comments'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const id = getQuery(event).id
  if (typeof id !== 'string' || id === '')
    throw createError({ statusCode: 400, message: 'The comment is missing' })
  if (!event.context.auth) throw createError({ statusCode: 401, message: 'Signed out' })
  const status = await deleteComment(ownApiClient(event), id)
  if (status === 0 || status >= 300)
    throw createError({ statusCode: status || 502, message: 'The comment was not deleted' })
  setResponseStatus(event, 204)
  return null
})
