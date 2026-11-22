// Publishing a post from the editor (E05-09, FR-EDITOR, ADR-0046): the draft is saved first (POST /posts, or PUT
// /posts/{id} when an earlier attempt already made it), then POST /posts/{id}/publish runs the server's checks, and
// the outcome carries the post's canonical address for the page to open. A draft kept from a failed attempt comes
// back in `draftId`, so trying again does not make a second draft.
import type { ApiClient } from './client'
import { postPath, type Post } from './posts'
import type { PostDocument } from '../editor/document'

export interface PublishInput {
  /** The draft an earlier attempt saved; `null` for the first one. */
  draftId: string | null
  title: string | null
  /** A topic, or `null` for «Без темы»: the author's own blog. */
  blogId: string | null
  body: PostDocument
}

export type PublishOutcome =
  | { status: 'published'; post: Post; path: string }
  | { status: 'invalid'; errors: Record<string, string>; draftId: string | null }
  | { status: 'email-not-confirmed'; draftId: string }
  | { status: 'unauthorized' }
  | { status: 'error'; draftId: string | null }

/** The first message of each field of a validation problem (ADR-0006: `errors` keyed by field). */
function fieldErrors(problem: unknown): Record<string, string> {
  const errors = (problem as { errors?: Record<string, string[]> } | undefined)?.errors ?? {}
  return Object.fromEntries(
    Object.entries(errors)
      .filter(([, messages]) => messages.length > 0)
      .map(([field, messages]) => [field, messages[0]!]),
  )
}

const codeOf = (problem: unknown) => (problem as { code?: string } | undefined)?.code

export async function publishPost(client: ApiClient, input: PublishInput): Promise<PublishOutcome> {
  let draftId = input.draftId
  try {
    const body = { title: input.title, blogId: input.blogId, body: input.body }
    const saved = draftId
      ? await client.PUT('/api/v1/posts/{id}', { params: { path: { id: draftId } }, body })
      : await client.POST('/api/v1/posts', { body })
    if (!saved.data) {
      if (saved.response.status === 401) return { status: 'unauthorized' }
      if (saved.response.status === 400)
        return { status: 'invalid', errors: fieldErrors(saved.error), draftId }
      return { status: 'error', draftId }
    }
    draftId = saved.data.id

    const published = await client.POST('/api/v1/posts/{id}/publish', { params: { path: { id: draftId } } })
    if (!published.data) {
      const status = published.response.status
      if (status === 401) return { status: 'unauthorized' }
      if (status === 403 && codeOf(published.error) === 'email_not_confirmed')
        return { status: 'email-not-confirmed', draftId }
      if (status === 400) return { status: 'invalid', errors: fieldErrors(published.error), draftId }
      return { status: 'error', draftId }
    }

    const post = published.data
    const { data: blog } = await client.GET('/api/v1/profiles/{id}', {
      params: { path: { id: post.blogId } },
    })
    // Without the blog the id alone still finds the post: the page leads on to its canonical address.
    return { status: 'published', post, path: blog ? postPath(blog, post) : `/u/${post.blogId}/${post.id}` }
  } catch {
    return { status: 'error', draftId }
  }
}
