// Removing the cover from the blog page (E04-04, ADR-0079) through DELETE /profiles/me/cover with the person's access
// token (ADR-0018).
import { removeOwnCover } from '~/api/profiles'

export default defineEventHandler((event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  return removeOwnCover(ownApiClient(event))
})
