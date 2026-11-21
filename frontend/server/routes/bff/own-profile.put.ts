// Saving the own profile from the blog settings (E04-03, ADR-0034): the browser sends the name, the description and
// the avatar's media id here, the server sends them on to PUT /profiles/me with the person's access token (ADR-0018).
// The answer is the form's outcome (saved, field errors, a failure), not the API's.
import { saveOwnProfile, type ProfileInput } from '~/api/profiles'

const text = (value: unknown) => (typeof value === 'string' ? value : null)

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const client = ownApiClient(event)
  const body = (await readBody<Partial<Record<keyof ProfileInput, unknown>>>(event)) ?? {}
  return saveOwnProfile(client, {
    name: text(body.name),
    description: text(body.description),
    avatarId: text(body.avatarId),
  })
})
