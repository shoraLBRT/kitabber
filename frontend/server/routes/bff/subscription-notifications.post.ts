// The «колокольчик» of a subscription from the browser (E11-04, ADR-0118): the browser sends the blog and the wanted
// state here, the server sends PUT or DELETE …/subscription/notifications with the person's access token (it never
// leaves the server, ADR-0018). The answer is the outcome (the new state, the server's reason, signed out).
import { changeNewPostNotifications, type SubscriptionChange } from '~/api/subscriptions'

export default defineEventHandler(async (event): Promise<SubscriptionChange> => {
  setResponseHeader(event, 'cache-control', 'no-store')
  if (!event.context.auth) return { status: 'unauthorized' }
  const body = (await readBody<Record<string, unknown>>(event)) ?? {}
  const blogId = typeof body.blogId === 'string' ? body.blogId : ''
  if (blogId === '' || typeof body.on !== 'boolean')
    throw createError({ statusCode: 400, message: 'The bell is missing its blog' })
  return changeNewPostNotifications(ownApiClient(event), blogId, body.on)
})
