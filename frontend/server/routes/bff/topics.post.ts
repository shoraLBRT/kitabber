// Creating a topic (E11-11, ADR-0084): the browser sends the name, the address and the description here, the server
// sends them on to POST /api/v1/topics with the person's access token (ADR-0018). The answer is the form's outcome
// (created, field errors, the limit, an unconfirmed email, a failure), not the API's.
import { createTopic, type TopicInput } from '~/api/topics'

const text = (value: unknown) => (typeof value === 'string' ? value : null)

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const client = ownApiClient(event)
  const body = (await readBody<Partial<Record<keyof TopicInput, unknown>>>(event)) ?? {}
  return createTopic(client, {
    name: text(body.name),
    nickname: text(body.nickname),
    description: text(body.description),
  })
})
