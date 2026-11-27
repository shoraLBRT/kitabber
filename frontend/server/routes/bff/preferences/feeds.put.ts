// Saving «Ленты» of the signed-in account's settings (E08-07, ADR-0106): the browser sends the rating filter of
// «Свежее» here, the server sends it on to PUT /preferences/me/feeds with the person's access token (ADR-0018) and
// answers with the API's status (204 when it is saved).
import { saveOwnFeeds } from '~/api/preferences'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const client = ownApiClient(event)
  const body = (await readBody<{ freshMinRating?: unknown }>(event)) ?? {}
  const rating = typeof body.freshMinRating === 'number' ? body.freshMinRating : null
  const status = await saveOwnFeeds(client, rating)
  if (status >= 200 && status < 300) return sendNoContent(event)
  throw createError({ statusCode: status === 0 ? 502 : status, message: 'The settings were not saved' })
})
