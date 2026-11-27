// Putting or taking out a bookmark from the browser (E10-07, ADR-0113): the browser sends the target and whether to
// keep it here, the server sends PUT or DELETE …/bookmark with the person's access token (it never leaves the server,
// ADR-0018). The answer is the outcome (the new state, the server's reason, signed out), not the API's.
import { changeBookmark, isBookmarkTarget, type BookmarkChange } from '~/api/bookmarks'

export default defineEventHandler(async (event): Promise<BookmarkChange> => {
  setResponseHeader(event, 'cache-control', 'no-store')
  if (!event.context.auth) return { status: 'unauthorized' }
  const body = (await readBody<Record<string, unknown>>(event)) ?? {}
  const id = typeof body.id === 'string' ? body.id : ''
  if (!isBookmarkTarget(body.target) || id === '' || typeof body.on !== 'boolean')
    throw createError({ statusCode: 400, message: 'The bookmark is missing its target or state' })
  return changeBookmark(ownApiClient(event), body.target, id, body.on)
})
