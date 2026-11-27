// The tab «Комментарии» of a blog and a topic (E09-15, ADR-0114): read in the browser through
// server/routes/bff/blog-comments.get.ts, so every read — after a reload too — carries the reader's token and leaves
// out the authors they blocked; the header of the page is already on the screen meanwhile. «Показать ещё» reads the
// next page with the cursor of the one before. The authors' names and avatars follow, as in the comments under a post;
// a guest comment from Telegram is named by its guest.
import { computed, ref, shallowRef } from 'vue'
import { appendBlogComments, type BlogComment, type BlogCommentPage } from '../api/blog-comments'
import { loadAuthors } from '../api/comments'
import type { Profile } from '../api/profiles'

/** What the tab shows: a skeleton, the error with «Повторить», «Пока нет комментариев», or the comments. */
export type BlogCommentsState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'empty' }
  | { status: 'found'; comments: BlogComment[]; hasMore: boolean }

export function useBlogComments(blogId: () => string) {
  const { $api } = useNuxtApp()
  const authors = shallowRef(new Map<string, Profile | null>())
  const shown = ref<BlogComment[] | null>(null)
  const cursor = ref<string | null>(null)
  const loadingMore = ref(false)
  const moreFailed = ref(false)

  const read = (after?: string | null) =>
    $fetch<BlogCommentPage>('/bff/blog-comments', {
      query: { blog: blogId(), ...(after ? { cursor: after } : {}) },
    })

  async function withAuthors(page: BlogCommentPage): Promise<BlogCommentPage> {
    const ids = [
      ...new Set(page.items.flatMap((comment) => (comment.authorAccountId ? [comment.authorAccountId] : []))),
    ].filter((id) => !authors.value.has(id))
    // A new map, not the old one changed: the list sees a new prop and draws the names.
    if (ids.length > 0) authors.value = new Map([...authors.value, ...(await loadAuthors($api, ids))])
    return page
  }

  const { status, refresh } = useAsyncData(
    () => `blog-comments:${blogId()}`,
    async () => {
      const page = await withAuthors(await read())
      shown.value = page.items
      cursor.value = page.nextCursor ?? null
      moreFailed.value = false
      return page
    },
    { server: false, lazy: true, default: () => null },
  )

  const state = computed<BlogCommentsState>(() => {
    if (status.value === 'error') return { status: 'error' }
    if (status.value !== 'success' || !shown.value) return { status: 'loading' }
    if (shown.value.length === 0) return { status: 'empty' }
    return { status: 'found', comments: shown.value, hasMore: cursor.value !== null }
  })

  async function loadMore() {
    if (!cursor.value || !shown.value || loadingMore.value) return
    loadingMore.value = true
    moreFailed.value = false
    try {
      const page = await withAuthors(await read(cursor.value))
      shown.value = appendBlogComments(shown.value, page.items)
      cursor.value = page.nextCursor ?? null
    } catch {
      moreFailed.value = true
    } finally {
      loadingMore.value = false
    }
  }

  return { state, authors, loadingMore, moreFailed, loadMore, retry: () => refresh() }
}
