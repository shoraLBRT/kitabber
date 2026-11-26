// Saving «Основные» of the signed-in account's settings (E04-08, ADR-0070): the browser sends the theme here, the
// server sends it on to PUT /preferences/me/general with the person's access token (ADR-0018) and answers with the
// API's status (204 when it is saved).
import { saveOwnGeneral } from '~/api/preferences'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const client = ownApiClient(event)
  const body = (await readBody<{ theme?: unknown }>(event)) ?? {}
  const status = await saveOwnGeneral(client, typeof body.theme === 'string' ? body.theme : null)
  if (status >= 200 && status < 300) return sendNoContent(event)
  throw createError({ statusCode: status === 0 ? 502 : status, message: 'The settings were not saved' })
})
