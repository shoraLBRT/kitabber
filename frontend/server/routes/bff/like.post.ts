// Putting or taking back a like from the browser (E10-01, ADR-0061): the browser sends the target and the wanted mark
// here, the server sends PUT or DELETE …/like on with the person's access token (it never leaves the server,
// ADR-0018). The answer is the outcome (the new count, the server's reason, signed out), not the API's.
import { changeLike, isLikeTarget, type LikeChange } from '~/api/likes'

export default defineEventHandler(async (event): Promise<LikeChange> => {
  setResponseHeader(event, 'cache-control', 'no-store')
  if (!event.context.auth) return { status: 'unauthorized' }
  const body = (await readBody<Record<string, unknown>>(event)) ?? {}
  const id = typeof body.id === 'string' ? body.id : ''
  if (!isLikeTarget(body.target) || id === '' || typeof body.liked !== 'boolean')
    throw createError({ statusCode: 400, message: 'The like is missing its target' })
  return changeLike(ownApiClient(event), body.target, id, body.liked)
})
