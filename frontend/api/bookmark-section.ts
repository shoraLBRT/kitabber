// The section «Закладки» (E04-12, FR-PROFILE, ADR-0116): GET /api/v1/feeds/bookmarks (the tab «Посты») and
// GET /api/v1/comments/bookmarks (the tab «Комментарии»), newest bookmark first, page by page. Only the signed-in
// person's own bookmarks, so both are read with their token (server/routes/bff/bookmark-section.get.ts). A post or a
// comment that is no longer shown comes without its content, and the section shows a stub in its place.
import type { ApiClient, components } from './client'

export type BookmarkedPost = components['schemas']['BookmarkedPostView']
export type BookmarkedComment = components['schemas']['BookmarkedCommentView']
export type BookmarkedPostPage = components['schemas']['CursorPageOfBookmarkedPostView']
export type BookmarkedCommentPage = components['schemas']['CursorPageOfBookmarkedCommentView']

/** The tabs of the section. */
export type BookmarkSectionTab = 'posts' | 'comments'

/** A page of a tab, or a failure the tab shows with «Повторить». */
export type BookmarkSectionLookup =
  { status: 'found'; page: BookmarkedPostPage | BookmarkedCommentPage } | { status: 'error' }

/** How many bookmarks one page gives. */
export const bookmarkSectionPageSize = 20

/** The address of the section and of its tab «Комментарии». */
export const bookmarksPath = '/bookmarks'
export const bookmarkedCommentsPath = '/bookmarks/comments'

/** One page of the tab after `cursor`; the first one without it. */
export async function loadBookmarkSection(
  client: ApiClient,
  tab: BookmarkSectionTab,
  cursor?: string | null,
): Promise<BookmarkSectionLookup> {
  const query = { limit: bookmarkSectionPageSize, ...(cursor ? { cursor } : {}) }
  try {
    const { data } =
      tab === 'posts'
        ? await client.GET('/api/v1/feeds/bookmarks', { params: { query } })
        : await client.GET('/api/v1/comments/bookmarks', { params: { query } })
    return data ? { status: 'found', page: data } : { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/** The id of a bookmark in its tab: the post or the comment. */
export function bookmarkedId(item: BookmarkedPost | BookmarkedComment): string {
  return 'commentId' in item ? item.commentId : item.postId
}

/** The bookmarks of `next` after those already shown, each once. */
export function appendBookmarks<T extends BookmarkedPost | BookmarkedComment>(
  shown: readonly T[],
  next: readonly T[],
): T[] {
  const known = new Set(shown.map(bookmarkedId))
  return [...shown, ...next.filter((item) => !known.has(bookmarkedId(item)))]
}
