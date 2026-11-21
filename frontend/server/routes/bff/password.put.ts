// Changing the password in the settings (E03-05, ADR-0036): the browser sends the current and the new password here,
// the server sends them on to PUT /api/v1/account/password with the person's access token (ADR-0018) and answers
// with the API's status and problem, so the form can show the error at its field. No session: 401.
import { changeOwnPassword } from '~/api/password'

const text = (value: unknown) => (typeof value === 'string' ? value : null)

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const client = ownApiClient(event)
  const body = (await readBody<{ currentPassword?: unknown; newPassword?: unknown }>(event)) ?? {}
  const result = await changeOwnPassword(client, text(body.currentPassword), text(body.newPassword))
  setResponseStatus(event, result.status === 0 ? 502 : result.status)
  return result.body ?? null
})
