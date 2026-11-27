// A feed read page by page (FR-FEED, ADR-0043) that comes back where it was (E08-03, ADR-0051). The first page is read
// on the server; later pages come from the browser with the cursor of the page before. What the feed shows is kept per
// history entry: «назад» from a post draws the same posts at once, without asking the API, so the router's saved
// scroll position lands on the same post. A new visit to the feed (a link, the menu) reads it again.
import {
  appendPage,
  feedEntryKey,
  rememberFeed,
  type FeedLookup,
  type FeedPost,
  type FeedSnapshot,
  type FeedState,
} from '~/api/feeds'

export async function useFeed(name: string, load: (cursor?: string | null) => Promise<FeedLookup>) {
  const snapshots = useState<Record<string, FeedSnapshot>>('feed:snapshots', () => ({}))
  const entry = import.meta.client ? `${name}:${feedEntryKey(window.history)}` : null
  const saved = entry ? snapshots.value[entry] : undefined

  const { data, status, refresh } = await useAsyncData(`feed:${name}`, () => load(), {
    lazy: true,
    immediate: !saved,
  })

  const posts = ref<FeedPost[] | null>(saved?.posts ?? null)
  const cursor = ref<string | null>(saved?.cursor ?? null)
  const loadingMore = ref(false)
  const moreFailed = ref(false)

  // The posts start again whenever the first page is read again.
  watch(
    data,
    (first) => {
      posts.value = first?.status === 'found' ? first.page.items : null
      cursor.value = first?.status === 'found' ? first.page.nextCursor : null
      moreFailed.value = false
    },
    { immediate: !saved },
  )

  watch(
    [posts, cursor],
    ([shown, next]) => {
      if (entry && shown)
        snapshots.value = rememberFeed(snapshots.value, entry, { posts: shown, cursor: next })
    },
    { immediate: true },
  )

  const state = computed<FeedState>(() => {
    if (status.value === 'pending') return { status: 'loading' }
    if (posts.value) return { status: 'found', posts: posts.value, hasMore: cursor.value !== null }
    if (data.value?.status === 'error') return { status: 'error' }
    return { status: 'loading' }
  })

  async function loadMore() {
    if (!cursor.value || !posts.value || loadingMore.value) return
    loadingMore.value = true
    const next = await load(cursor.value)
    loadingMore.value = false
    if (next.status === 'error') {
      moreFailed.value = true
      return
    }
    moreFailed.value = false
    posts.value = appendPage(posts.value, next.page)
    cursor.value = next.page.nextCursor
  }

  // `posts` and `cursor` are what the feed shows; «Показать новые» puts new posts on top of them (useNewPosts).
  return { state, posts, cursor, loadingMore, moreFailed, loadMore, retry: () => refresh() }
}
