// Deleting a post from the «…» menu of its page (E07-05, ADR-0053): the browser asks here, and the server deletes it
// with the person's access token (it never leaves the server, ADR-0018). The API deletes softly and only for the author
// (posts.not_author otherwise); its status comes back as is: 204, 401, 403 or 404.
import { deletePost } from '~/api/posts'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const id = getQuery(event).id
  if (typeof id !== 'string' || id === '')
    throw createError({ statusCode: 400, message: 'The post is missing' })
  const status = await deletePost(ownApiClient(event), id)
  if (status >= 300) throw createError({ statusCode: status, message: 'The post was not deleted' })
  setResponseStatus(event, 204)
  return null
})
