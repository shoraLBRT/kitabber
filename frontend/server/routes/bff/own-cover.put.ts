// Setting or moving the cover from the blog page (E04-04, ADR-0079): the browser sends the media id of the uploaded
// picture and its position here, the server sends them on to PUT /profiles/me/cover with the person's access token
// (ADR-0018). The answer is the page's outcome (saved, field errors, a failure), not the API's.
import { saveOwnCover } from '~/api/profiles'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const body = (await readBody<{ mediaId?: unknown; position?: unknown }>(event)) ?? {}
  return saveOwnCover(ownApiClient(event), {
    mediaId: typeof body.mediaId === 'string' ? body.mediaId : '',
    position: typeof body.position === 'number' ? body.position : 50,
  })
})
