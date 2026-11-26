// The menu's topics (E11-02, ADR-0088): the left menu and the phone «Меню» ask here, and the server asks the API with
// the person's access token when there is one (it never leaves the server, ADR-0018): a person sees their subscribed
// topics, a guest the popular ones. An unavailable API is a 502, so the menu shows only «Показать все».
import { createApiClient } from '~/api/client'
import { forwardedHeaders } from '~/api/forwarded'
import { loadTopicMenu } from '~/api/topics'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'private, no-store')
  const accessToken = event.context.auth?.accessToken
  const client = createApiClient({
    baseUrl: useRuntimeConfig().apiBase,
    headers: {
      ...forwardedHeaders(getRequestHeader(event, 'x-forwarded-for')),
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
    },
  })
  const menu = await loadTopicMenu(client)
  if (!menu) throw createError({ statusCode: 502, message: 'The topics of the menu are unavailable' })
  return menu
})
