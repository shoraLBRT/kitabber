// A change of a topic by its owner or by the person it is offered to (E11-12, ADR-0121): the browser names the topic
// and the change, the server sends it on to the API with the person's access token (ADR-0018). The answer is the page's
// outcome (saved, field errors, a refusal, a failure), not the API's.
import { changeTopic, managementActionOf } from '~/api/topic-management'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const body = await readBody<Record<string, unknown>>(event)
  const change = managementActionOf(body)
  if (!change || typeof body?.id !== 'string')
    throw createError({ statusCode: 400, message: 'Not a change of a topic' })
  return changeTopic(ownApiClient(event), body.id, change)
})
