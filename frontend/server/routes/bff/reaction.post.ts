// Putting, changing or taking back a reaction from the browser (E10-01, E10-02, ADR-0061, ADR-0101): the browser sends
// the target and the wanted kind (`null` takes the reaction back) here, the server sends PUT or DELETE …/reaction with
// the person's access token (it never leaves the server, ADR-0018). The answer is the outcome (the new counts, the
// server's reason, signed out), not the API's.
import { changeReaction, isReactionKind, isReactionTarget, type ReactionChange } from '~/api/reactions'

export default defineEventHandler(async (event): Promise<ReactionChange> => {
  setResponseHeader(event, 'cache-control', 'no-store')
  if (!event.context.auth) return { status: 'unauthorized' }
  const body = (await readBody<Record<string, unknown>>(event)) ?? {}
  const id = typeof body.id === 'string' ? body.id : ''
  if (!isReactionTarget(body.target) || id === '' || !(body.kind === null || isReactionKind(body.kind)))
    throw createError({ statusCode: 400, message: 'The reaction is missing its target or kind' })
  return changeReaction(ownApiClient(event), body.target, id, body.kind)
})
