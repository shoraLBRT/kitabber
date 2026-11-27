// The tab «Комментарии» of a blog and a topic (E09-15, FR-COMMENT, ADR-0114): GET /api/v1/comments/blogs/{blogId},
// newest first, page by page. A personal blog lists the comments its owner wrote, a topic every comment under its
// posts. Deleted comments are never in it, and the server leaves out the authors the reader blocked, so the list is
// read with the reader's token (server/routes/bff/blog-comments.get.ts), never from the browser without it.
import type { ApiClient, components } from './client'
import { commentHash } from './comments'
import { postSegment } from './posts'

export type BlogComment = components['schemas']['BlogCommentView']
export type BlogCommentPage = components['schemas']['CursorPageOfBlogCommentView']

/** A page of the tab, or a failure the tab shows with «Повторить». */
export type BlogCommentsLookup = { status: 'found'; page: BlogCommentPage } | { status: 'error' }

/** How many comments one page gives. */
export const blogCommentsPageSize = 20

/** One page of the blog's comments after `cursor`; the first one without it. */
export async function loadBlogComments(
  client: ApiClient,
  blogId: string,
  cursor?: string | null,
): Promise<BlogCommentsLookup> {
  try {
    const { data } = await client.GET('/api/v1/comments/blogs/{blogId}', {
      params: {
        path: { blogId },
        query: { limit: blogCommentsPageSize, ...(cursor ? { cursor } : {}) },
      },
    })
    return data ? { status: 'found', page: data } : { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/**
 * The comment on its post's page (ADR-0107): the post's address by its blog id, which the post page leads to the
 * canonical one (ADR-0041), with `#comment-<id>`.
 */
export function blogCommentPath(
  comment: Pick<BlogComment, 'id' | 'postId' | 'postBlogId' | 'postSlug'>,
): string {
  return `/u/${comment.postBlogId}/${postSegment({ id: comment.postId, slug: comment.postSlug ?? null })}${commentHash(comment.id)}`
}

/** The comments of `next` after those already shown, each once. */
export function appendBlogComments(
  shown: readonly BlogComment[],
  next: readonly BlogComment[],
): BlogComment[] {
  const known = new Set(shown.map((comment) => comment.id))
  return [...shown, ...next.filter((comment) => !known.has(comment.id))]
}
