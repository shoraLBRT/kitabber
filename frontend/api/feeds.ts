// The fresh feed (E08-01, FR-FEED, ADR-0043): every published post, newest first, page by page with the cursor the API
// gives (`nextCursor`, ADR-0006). The client never reads the cursor; it only passes it back for the next page.
import { commentHash } from './comments'
import { postPath, postSegment, type Post } from './posts'
import type { ApiClient, components } from './client'

export type FeedPage = components['schemas']['CursorPageOfFeedPostView']

/** A post of a feed with the blogs its card shows: the author's personal blog and the topic (ADR-0052). */
export type FeedPost = components['schemas']['FeedPostView']

/** The author or the topic on a card. */
export type FeedBlog = components['schemas']['FeedBlogView']

/**
 * The best comment under a card (E08-15, ADR-0062): the most liked shown one, the earlier on a tie, chosen by the
 * server for the whole page; `null` on the card of a post without comments.
 */
export type FeedComment = components['schemas']['FeedCommentView']

export type FeedLookup = { status: 'found'; page: FeedPage } | { status: 'error' }

/** What a feed shows: the posts read so far and whether there is a next page. */
export type FeedState =
  { status: 'loading' } | { status: 'error' } | { status: 'found'; posts: FeedPost[]; hasMore: boolean }

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
export function appendPage(shown: FeedPost[], page: FeedPage): FeedPost[] {
  const seen = new Set(shown.map((item) => item.post.id))
  return [...shown, ...page.items.filter((item) => !seen.has(item.post.id))]
}

/**
 * The address of a post by its blog id. The post page leads an address with a blog id to the canonical one
 * (ADR-0041), so a card whose blog is unknown still leads to the post.
 */
export function feedPostPath(post: Pick<Post, 'id' | 'slug' | 'blogId'>): string {
  return `/u/${post.blogId}/${postSegment(post)}`
}

/** The canonical address of a feed post: in its topic, or in its author's blog; by the blog id when neither is known. */
export function feedItemPath(item: Pick<FeedPost, 'post' | 'author' | 'topic'>): string {
  const blog = item.topic ?? (item.author?.id === item.post.blogId ? item.author : null)
  return blog ? postPath(blog, item.post) : feedPostPath(item.post)
}

/**
 * The comment `commentId` on its post's page (ADR-0062): the post's address with `#comment-<id>`. The page reads the
 * comments in the browser and then brings that comment into view (or opens its branch when it is deeper than a read).
 */
export function feedCommentPath(
  item: Pick<FeedPost, 'post' | 'author' | 'topic'>,
  commentId: string,
): string {
  return `${feedItemPath(item)}${commentHash(commentId)}`
}

// Coming back to a feed (E08-03, ADR-0051): what a feed showed is kept per history entry, so «назад» from a post
// draws the same posts at once and the router puts the page back where it was. A new visit reads the feed again.

/** The posts a feed showed in one history entry and the cursor of its next page. */
export type FeedSnapshot = { posts: FeedPost[]; cursor: string | null }

/** How many history entries keep their feed; the oldest is forgotten first. */
export const feedSnapshotLimit = 10

/** The store with this entry's snapshot as the newest one, keeping at most `feedSnapshotLimit` entries. */
export function rememberFeed(
  store: Record<string, FeedSnapshot>,
  entry: string,
  snapshot: FeedSnapshot,
): Record<string, FeedSnapshot> {
  const rest = Object.entries(store).filter(([key]) => key !== entry)
  return Object.fromEntries([...rest.slice(-(feedSnapshotLimit - 1)), [entry, snapshot]])
}

/** The field of `history.state` that names the entry for the feed; the router keeps the fields it does not own. */
export const feedEntryField = 'feedEntry'

/**
 * The key of the current history entry. An entry seen before keeps its key (it lives in `history.state`, so «назад»
 * and «вперёд» bring it back); a new entry gets one.
 */
export function feedEntryKey(
  history: Pick<History, 'state' | 'replaceState'>,
  newKey: () => string = () => `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`,
): string {
  const state = (history.state ?? {}) as Record<string, unknown>
  const known = state[feedEntryField]
  if (typeof known === 'string' && known) return known
  const key = newKey()
  history.replaceState({ ...state, [feedEntryField]: key }, '')
  return key
}

// The card of a post in a feed (E08-02, FR-FEED, ADR-0052): a short post is shown whole, a longread only its first
// blocks with «Показать полностью».

/** The most blocks a card shows before «Показать полностью». */
export const feedPreviewBlocks = 3

/** The text a card shows before «Показать полностью»: the block that reaches it is the last one shown. */
export const feedPreviewCharacters = 600

/** What a card shows of a post's document: its first blocks, and whether some are left out. */
export type FeedPreview = { document: unknown; cut: boolean }

/**
 * The preview of a document by the rule of ADR-0052: at most `feedPreviewBlocks` blocks, and no block after the one
 * whose text brings the total to `feedPreviewCharacters`; the first block is always shown. A document that fits is
 * given back as it is, so a short post is drawn whole.
 */
export function feedPreview(document: unknown): FeedPreview {
  const blocks = (document as { blocks?: unknown } | null)?.blocks
  if (!Array.isArray(blocks)) return { document, cut: false }
  let shown = 0
  let characters = 0
  while (shown < blocks.length && shown < feedPreviewBlocks && characters < feedPreviewCharacters) {
    characters += textLength((blocks[shown] as { data?: unknown } | null)?.data)
    shown += 1
  }
  if (shown >= blocks.length) return { document, cut: false }
  return { document: { ...(document as object), blocks: blocks.slice(0, shown) }, cut: true }
}

// The fields of block data that people read; ids, sizes and addresses of files are not text.
const textFields = new Set(['text', 'caption', 'items'])

/** The length of the text in a block's data, its markup left out. */
function textLength(value: unknown, field?: string): number {
  if (typeof value === 'string')
    return field && textFields.has(field) ? value.replace(/<[^>]*>/g, '').length : 0
  if (Array.isArray(value)) return value.reduce((sum: number, item) => sum + textLength(item, field), 0)
  if (value && typeof value === 'object')
    return Object.entries(value).reduce((sum, [key, item]) => sum + textLength(item, key), 0)
  return 0
}
