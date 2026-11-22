// The fresh feed (E08-01, FR-FEED, ADR-0043): every published post, newest first, page by page with the cursor the API
// gives (`nextCursor`, ADR-0006). The client never reads the cursor; it only passes it back for the next page.
import { postSegment, type Post } from './posts'
import type { ApiClient, components } from './client'

export type FeedPage = components['schemas']['CursorPageOfPostView']

export type FeedLookup = { status: 'found'; page: FeedPage } | { status: 'error' }

/** What a feed shows: the posts read so far and whether there is a next page. */
export type FeedState =
  { status: 'loading' } | { status: 'error' } | { status: 'found'; posts: Post[]; hasMore: boolean }

/** One page of the fresh feed: the first without a cursor, the next with the previous page's `nextCursor`. */
export async function loadFreshFeed(client: ApiClient, cursor?: string | null): Promise<FeedLookup> {
  try {
    const { data } = await client.GET('/api/v1/feeds/fresh', {
      params: { query: cursor ? { cursor } : {} },
    })
    return data ? { status: 'found', page: data } : { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/** Adds a later page to the posts already shown, leaving out any post that is already there. */
export function appendPage(shown: Post[], page: FeedPage): Post[] {
  const seen = new Set(shown.map((post) => post.id))
  return [...shown, ...page.items.filter((post) => !seen.has(post.id))]
}

/**
 * The address of a post of the feed. The feed item knows its blog by id only, and the post page leads an address with
 * a blog id to the canonical one (ADR-0041), so the card does not need the blog itself. The card with the author and
 * the blog comes with E08-02 (#55).
 */
export function feedPostPath(post: Pick<Post, 'id' | 'slug' | 'blogId'>): string {
  return `/u/${post.blogId}/${postSegment(post)}`
}
