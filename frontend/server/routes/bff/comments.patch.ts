// Changing the text of the person's own comment (E09-05, ADR-0059): the browser sends the comment and the new text
// here, the server sends it on to PATCH /comments/{id} with the person's access token (it never leaves the server,
// ADR-0018). The answer is the form's outcome (the kept comment, the server's reason, signed out), not the API's.
import { editComment, type CommentEdit } from '~/api/comments'

const text = (value: unknown) => (typeof value === 'string' ? value : '')

export default defineEventHandler(async (event): Promise<CommentEdit> => {
  setResponseHeader(event, 'cache-control', 'no-store')
  if (!event.context.auth) return { status: 'unauthorized' }
  const body = (await readBody<Record<string, unknown>>(event)) ?? {}
  const id = text(body.id)
  if (id === '') throw createError({ statusCode: 400, message: 'The comment is missing' })
  return editComment(ownApiClient(event), id, text(body.text))
})
