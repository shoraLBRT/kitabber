// Bookmarks on posts and comments (E10-07, FR-REACT, ADR-0113). A signed-in person keeps a post or a comment in their
// bookmarks and takes it out; a repeat changes nothing. Bookmarks are private: every request goes with the person's
// access token through the site's own server, the answer tells only about the person's own bookmarks, and the count of
// people who keep a post or a comment comes only to its author. The post page reads the post and its comments at once,
// a feed reads the posts of its cards at once.
import type { ApiClient, components } from './client'
import type { ReactionTarget } from './reactions'
import { t } from '../i18n'

export type BookmarkTarget = ReactionTarget

/** Whether the person keeps the target, and how many people do: a number only for the target's author. */
export interface BookmarkState {
  bookmarked: boolean
  count: number | null
}

export const noBookmark: BookmarkState = { bookmarked: false, count: null }

export type PostBookmarks = components['schemas']['PostBookmarksView']
export type FeedBookmarks = components['schemas']['FeedBookmarksView']

/** The most posts one feed request asks about (`BookmarkEndpoints.MaxFeedPosts`). */
export const maxFeedPosts = 100

/** The outcome of putting or taking out a bookmark. */
export type BookmarkChange =
  | { status: 'done'; state: BookmarkState }
  | { status: 'refused'; message: string }
  | { status: 'unauthorized' }
  | { status: 'error' }

export function isBookmarkTarget(value: unknown): value is BookmarkTarget {
  return value === 'post' || value === 'comment'
}

/** The key of a target in the page's state: `post:<id>` or `comment:<id>`. */
export function bookmarkKey(target: BookmarkTarget, id: string): string {
  return `${target}:${id}`
}

function stateOf(view: { bookmarked: boolean; count: number | null }): BookmarkState {
  return { bookmarked: view.bookmarked, count: view.count }
}

/** The state shown before the server answers: the person's mark flipped, the author's count moved with it. */
export function toggled(state: BookmarkState): BookmarkState {
  const bookmarked = !state.bookmarked
  const count = state.count === null ? null : Math.max(0, state.count + (bookmarked ? 1 : -1))
  return { bookmarked, count }
}

/** The states of the post page by target key; a target not listed is neither kept nor the author's with a count. */
export function postBookmarks(view: PostBookmarks): Record<string, BookmarkState> {
  return {
    [bookmarkKey('post', view.postId)]: stateOf(view.post),
    ...Object.fromEntries(view.comments.map((c) => [bookmarkKey('comment', c.id), stateOf(c)])),
  }
}

/** The states of the asked posts of a feed by target key: every asked post, the unlisted ones as not kept. */
export function feedBookmarks(ids: readonly string[], view: FeedBookmarks): Record<string, BookmarkState> {
  const listed = new Map(view.posts.map((p) => [p.id, stateOf(p)]))
  return Object.fromEntries(ids.map((id) => [bookmarkKey('post', id), listed.get(id) ?? noBookmark]))
}

/** The bookmarks of the post `postId` and of its comments; `null` when they cannot be read. */
export async function loadPostBookmarks(client: ApiClient, postId: string): Promise<PostBookmarks | null> {
  try {
    const { data } = await client.GET('/api/v1/posts/{postId}/bookmarks', { params: { path: { postId } } })
    return data ?? null
  } catch {
    return null
  }
}

/** The bookmarks of the posts `ids` of a feed (at most `maxFeedPosts`); `null` when they cannot be read. */
export async function loadFeedBookmarks(
  client: ApiClient,
  ids: readonly string[],
): Promise<FeedBookmarks | null> {
  try {
    const { data } = await client.GET('/api/v1/bookmarks/posts', {
      params: { query: { ids: ids.slice(0, maxFeedPosts) } },
    })
    return data ?? null
  } catch {
    return null
  }
}

/** Puts (`on`) or takes out the bookmark of the target. */
export async function changeBookmark(
  client: ApiClient,
  target: BookmarkTarget,
  id: string,
  on: boolean,
): Promise<BookmarkChange> {
  try {
    const { data, response } =
      target === 'post'
        ? on
          ? await client.PUT('/api/v1/posts/{postId}/bookmark', { params: { path: { postId: id } } })
          : await client.DELETE('/api/v1/posts/{postId}/bookmark', { params: { path: { postId: id } } })
        : on
          ? await client.PUT('/api/v1/comments/{id}/bookmark', { params: { path: { id } } })
          : await client.DELETE('/api/v1/comments/{id}/bookmark', { params: { path: { id } } })
    if (data) return { status: 'done', state: stateOf(data) }
    if (response.status === 401) return { status: 'unauthorized' }
    if (response.status === 404) return { status: 'refused', message: t('bookmarks.notFound') }
    return { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}
