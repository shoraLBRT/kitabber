// A tab of the section «Закладки» (E04-12, ADR-0116): read in the browser through
// server/routes/bff/bookmark-section.get.ts with the person's token, after the header of the page is on the screen.
// «Показать ещё» reads the next page with the cursor of the one before. The comments' authors' names and avatars
// follow, as in the tab «Комментарии» of a blog.
import { computed, ref, shallowRef } from 'vue'
import {
  appendBookmarks,
  type BookmarkedComment,
  type BookmarkedPost,
  type BookmarkSectionTab,
} from '../api/bookmark-section'
import { loadAuthors } from '../api/comments'
import type { Profile } from '../api/profiles'

type ItemOf<T extends BookmarkSectionTab> = T extends 'posts' ? BookmarkedPost : BookmarkedComment

/** What a tab shows: a skeleton, the error with «Повторить», the empty state, or the bookmarks. */
export type BookmarkSectionState<T> =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'empty' }
  | { status: 'found'; items: T[]; hasMore: boolean }

export function useBookmarkSection<T extends BookmarkSectionTab>(tab: T) {
  const { $api } = useNuxtApp()
  const authors = shallowRef(new Map<string, Profile | null>())
  const shown = shallowRef<ItemOf<T>[] | null>(null)
  const cursor = ref<string | null>(null)
  const loadingMore = ref(false)
  const moreFailed = ref(false)

  type Page = { items: ItemOf<T>[]; nextCursor: string | null }
  const read = (after?: string | null) =>
    $fetch<Page>('/bff/bookmark-section', { query: { tab, ...(after ? { cursor: after } : {}) } })

  async function withAuthors(page: Page): Promise<Page> {
    const ids = [
      ...new Set(
        page.items.flatMap((item) => {
          const author = 'comment' in item ? item.comment?.authorAccountId : null
          return author ? [author] : []
        }),
      ),
    ].filter((id) => !authors.value.has(id))
    // A new map, not the old one changed: the list sees a new prop and draws the names.
    if (ids.length > 0) authors.value = new Map([...authors.value, ...(await loadAuthors($api, ids))])
    return page
  }

  const { status, refresh } = useAsyncData(
    `bookmark-section:${tab}`,
    async () => {
      const page = await withAuthors(await read())
      shown.value = page.items
      cursor.value = page.nextCursor ?? null
      moreFailed.value = false
      return page
    },
    { server: false, lazy: true, default: () => null },
  )

  const state = computed<BookmarkSectionState<ItemOf<T>>>(() => {
    if (status.value === 'error') return { status: 'error' }
    if (status.value !== 'success' || !shown.value) return { status: 'loading' }
    if (shown.value.length === 0) return { status: 'empty' }
    return { status: 'found', items: shown.value, hasMore: cursor.value !== null }
  })

  async function loadMore() {
    if (!cursor.value || !shown.value || loadingMore.value) return
    loadingMore.value = true
    moreFailed.value = false
    try {
      const page = await withAuthors(await read(cursor.value))
      shown.value = appendBookmarks(shown.value, page.items)
      cursor.value = page.nextCursor ?? null
    } catch {
      moreFailed.value = true
    } finally {
      loadingMore.value = false
    }
  }

  return { state, authors, loadingMore, moreFailed, loadMore, retry: () => refresh() }
}
