// The code from the letter to the new email (E03-06, ADR-0020): POST /api/v1/account/email/confirmation with the
// person's access token (ADR-0018); answered with the API's status and problem. No session: 401.
import { ownEmail } from '~/api/email'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const client = ownApiClient(event)
  const body = (await readBody<{ code?: unknown }>(event)) ?? {}
  const result = await ownEmail(client).confirm(typeof body.code === 'string' ? body.code : null)
  setResponseStatus(event, result.status === 0 ? 502 : result.status)
  return result.body ?? null
})
