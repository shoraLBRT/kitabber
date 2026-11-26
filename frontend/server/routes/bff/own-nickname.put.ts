// Changing the own nickname from the blog settings (E04-05, ADR-0090): the browser sends the nickname here, the server
// sends it on to PUT /profiles/me/nickname with the person's access token (ADR-0018). The answer is the form's outcome
// (saved, a field error, too soon, a failure), not the API's.
import { saveOwnNickname } from '~/api/profiles'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const body = (await readBody<{ nickname?: unknown }>(event)) ?? {}
  return saveOwnNickname(ownApiClient(event), typeof body.nickname === 'string' ? body.nickname : '')
})
