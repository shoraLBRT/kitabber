// Reposting from the browser (E10-08, ADR-0117): the browser sends the post and the text, the server sends
// POST /api/v1/posts/{id}/reposts with the person's access token (it never leaves the server, ADR-0018). The answer is
// the outcome (the new repost, the server's reason, signed out), not the API's.
import { createRepost, repostTextMaxLength, type RepostOutcome } from '~/api/reposts'

export default defineEventHandler(async (event): Promise<RepostOutcome> => {
  setResponseHeader(event, 'cache-control', 'no-store')
  if (!event.context.auth) return { status: 'unauthorized' }
  const body = (await readBody<Record<string, unknown>>(event)) ?? {}
  const postId = typeof body.postId === 'string' ? body.postId : ''
  const text = typeof body.text === 'string' ? body.text : ''
  if (!/^[0-9a-fA-F-]{36}$/.test(postId) || text.length > repostTextMaxLength * 2)
    throw createError({ statusCode: 400, message: 'The repost is missing its post or its text is too long' })
  return createRepost(ownApiClient(event), postId, text)
})
