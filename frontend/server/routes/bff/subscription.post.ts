// Subscribing to a blog or a topic from the browser (E11-03, ADR-0085): the browser sends the blog and the wanted
// state here, the server sends PUT or DELETE …/subscription with the person's access token (it never leaves the server,
// ADR-0018). The answer is the outcome (the new numbers, the server's reason, signed out), not the API's.
import { changeSubscription, type SubscriptionChange } from '~/api/subscriptions'

export default defineEventHandler(async (event): Promise<SubscriptionChange> => {
  setResponseHeader(event, 'cache-control', 'no-store')
  if (!event.context.auth) return { status: 'unauthorized' }
  const body = (await readBody<Record<string, unknown>>(event)) ?? {}
  const blogId = typeof body.blogId === 'string' ? body.blogId : ''
  if (blogId === '' || typeof body.subscribed !== 'boolean')
    throw createError({ statusCode: 400, message: 'The subscription is missing its blog' })
  return changeSubscription(ownApiClient(event), blogId, body.subscribed)
})
