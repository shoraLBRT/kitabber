// «Выйти со всех устройств» in the settings (E03-09, ADR-0078): POST /api/v1/account/sessions/sign-out-others with the
// person's access token (ADR-0018), so every device but this one is signed out. No session: 401.
import { ownSessions } from '~/api/sessions'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const result = await ownSessions(ownApiClient(event)).endOthers()
  setResponseStatus(event, result.status === 0 ? 502 : result.status)
  return result.body ?? null
})
