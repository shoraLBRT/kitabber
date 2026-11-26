// A new email in the settings (E03-06, ADR-0020): PUT /api/v1/account/email with the person's access token
// (ADR-0018), which sends a code to the new address; answered with the API's status and problem. No session: 401.
import { ownEmail } from '~/api/email'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const client = ownApiClient(event)
  const body = (await readBody<{ email?: unknown }>(event)) ?? {}
  const result = await ownEmail(client).change(typeof body.email === 'string' ? body.email : null)
  setResponseStatus(event, result.status === 0 ? 502 : result.status)
  return result.body ?? null
})
