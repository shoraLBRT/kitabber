// Writing a comment from the browser (E09-03, ADR-0056): the browser sends the post, the text and the comment replied
// to here, the server sends it on to POST /posts/{postId}/comments with the person's access token (it never leaves the
// server, ADR-0018). The answer is the form's outcome (the kept comment, the server's reason, signed out), not the API's.
import { sendComment, type CommentSend } from '~/api/comments'

const text = (value: unknown) => (typeof value === 'string' ? value : '')

export default defineEventHandler(async (event): Promise<CommentSend> => {
  setResponseHeader(event, 'cache-control', 'no-store')
  if (!event.context.auth) return { status: 'unauthorized' }
  const body = (await readBody<Record<string, unknown>>(event)) ?? {}
  const postId = text(body.postId)
  if (postId === '') throw createError({ statusCode: 400, message: 'The post is missing' })
  const parentId = text(body.parentId) || null
  return sendComment(ownApiClient(event), postId, text(body.text), parentId)
})
