// The view reports of the browser (E07-06, ADR-0071): an opened post, the cards a feed showed, the reading time. The
// server passes each on to the API — with the access token of a signed-in person (it never leaves the server,
// ADR-0018), and for everyone with the reader's address, browser string and prefetch mark, by which the API counts a
// guest once a day and leaves bots out. The answer is always 204: a report is fire-and-forget.
import { createApiClient } from '~/api/client'
import { forwardedHeaders } from '~/api/forwarded'
import { forwardViewReport, parseViewReport } from '~/api/views'

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const report = parseViewReport(await readBody<unknown>(event).catch(() => null))
  if (!report) throw createError({ statusCode: 400, message: 'Not a view report' })

  const session = event.context.auth
  const purpose = getRequestHeader(event, 'sec-purpose') ?? getRequestHeader(event, 'purpose')
  const client = createApiClient({
    baseUrl: useRuntimeConfig().apiBase,
    headers: {
      ...forwardedHeaders(getRequestHeader(event, 'x-forwarded-for') ?? getRequestIP(event)),
      'user-agent': getRequestHeader(event, 'user-agent') ?? '',
      ...(purpose ? { 'sec-purpose': purpose } : {}),
      ...(session ? { authorization: `Bearer ${session.accessToken}` } : {}),
    },
  })
  try {
    await forwardViewReport(client, report)
  } catch {
    // The API being away costs these views, not the page.
  }
  setResponseStatus(event, 204)
  return null
})
