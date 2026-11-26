// A page of a subscriber or subscription list (E04-06, ADR-0099) for the browser. The lists are public, but each row
// says whether the reader follows that blog, so a signed-in person's request goes with their access token (it never
// leaves the server, ADR-0018) and the answer is never cached; a guest's goes without one.
import { createApiClient } from '~/api/client'
import { forwardedHeaders } from '~/api/forwarded'
import { loadSubscriptionList, type SubscriptionListLookup } from '~/api/subscriptions'

export default defineEventHandler(async (event): Promise<SubscriptionListLookup> => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const { blogId, kind, cursor } = getQuery(event)
  if (typeof blogId !== 'string' || blogId === '' || (kind !== 'subscribers' && kind !== 'subscriptions'))
    throw createError({ statusCode: 400, message: 'The list is missing its blog or kind' })
  const client = event.context.auth
    ? ownApiClient(event)
    : createApiClient({
        baseUrl: useRuntimeConfig().apiBase,
        headers: forwardedHeaders(getRequestHeader(event, 'x-forwarded-for')),
      })
  return loadSubscriptionList(
    client,
    blogId,
    kind,
    typeof cursor === 'string' && cursor !== '' ? cursor : null,
  )
})
