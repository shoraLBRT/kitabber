// Publishing from the editor (E05-09, ADR-0046): the browser sends the title, the topic and the document here, the
// server saves the draft and publishes it with the person's access token (it never leaves the server, ADR-0018). The
// answer is the editor's outcome (published with the post's address, field errors, a confirmed email needed), not the
// API's.
import type { PostDocument } from '~/editor/document'
import { publishPost } from '~/api/publish'

const text = (value: unknown) => (typeof value === 'string' && value !== '' ? value : null)

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const client = ownApiClient(event)
  const body = (await readBody<Record<string, unknown>>(event)) ?? {}
  return publishPost(client, {
    draftId: text(body.draftId),
    title: text(body.title),
    blogId: text(body.blogId),
    body: body.body as PostDocument,
  })
})
