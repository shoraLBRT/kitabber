// Saving «Ленты» of the signed-in account's settings (E08-07, ADR-0106; E08-13, ADR-0110): the browser sends the
// rating filter of «Свежее» and, from the settings, the home feed and the video autoplay here; the server sends them on
// to PUT /preferences/me/feeds with the person's access token (ADR-0018) and answers with the API's status (204 when it
// is saved). What is left out stays as saved.
import { saveOwnFeeds } from '~/api/preferences'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const client = ownApiClient(event)
  const body =
    (await readBody<{ freshMinRating?: unknown; homeFeed?: unknown; autoplayVideo?: unknown }>(event)) ?? {}
  const status = await saveOwnFeeds(client, {
    freshMinRating: typeof body.freshMinRating === 'number' ? body.freshMinRating : null,
    ...(typeof body.homeFeed === 'string' ? { homeFeed: body.homeFeed } : {}),
    ...(typeof body.autoplayVideo === 'boolean' ? { autoplayVideo: body.autoplayVideo } : {}),
  })
  if (status >= 200 && status < 300) return sendNoContent(event)
  throw createError({ statusCode: status === 0 ? 502 : status, message: 'The settings were not saved' })
})
