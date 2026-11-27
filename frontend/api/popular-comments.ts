// The widget «Популярные комментарии» of the desktop's right column (E08-12, FR-FEED, ADR-0105): the highest rated
// native comments of the last day. The server chooses them for everyone, caches the choice for a minute and leaves out
// the authors the reader blocked; a guest comment from Telegram never enters it.
import type { ApiClient, components } from './client'
import { commentHash } from './comments'
import { postSegment } from './posts'

export type PopularComment = components['schemas']['PopularCommentView']
export type PopularComments = components['schemas']['PopularCommentsView']

/** How many comments the widget shows. */
export const popularCommentsShown = 5

/** The widget's comments, or `null` when the API cannot give them. */
export async function loadPopularComments(client: ApiClient): Promise<PopularComments | null> {
  try {
    const { data } = await client.GET('/api/v1/comments/popular', {
      params: { query: { limit: popularCommentsShown } },
    })
    return data ?? null
  } catch {
    return null
  }
}

/**
 * The comment on its post's page (ADR-0062): the post's address by its blog id, which the post page leads to the
 * canonical one (ADR-0041), with `#comment-<id>`.
 */
export function popularCommentPath(
  comment: Pick<PopularComment, 'id' | 'postId' | 'postBlogId' | 'postSlug'>,
): string {
  return `/u/${comment.postBlogId}/${postSegment({ id: comment.postId, slug: comment.postSlug ?? null })}${commentHash(comment.id)}`
}
