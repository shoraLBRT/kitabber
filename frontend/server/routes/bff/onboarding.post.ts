// The onboarding's answer (E03-10, ADR-0102): the browser sends the name, the avatar's media id and the chosen topics
// here, the server sends them on to POST /profiles/me/onboarding with the person's access token (ADR-0018). The answer
// is the page's outcome (saved, field errors, done already, a failure), not the API's.
import { finishOnboarding } from '~/api/onboarding'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const client = ownApiClient(event)
  const body = (await readBody<Record<string, unknown>>(event)) ?? {}
  const name = typeof body.name === 'string' && body.name.trim() ? body.name : null
  const avatarId = typeof body.avatarId === 'string' ? body.avatarId : null
  const topicIds = Array.isArray(body.topicIds)
    ? body.topicIds.filter((id): id is string => typeof id === 'string')
    : []
  return finishOnboarding(client, { name, avatarId, topicIds })
})
