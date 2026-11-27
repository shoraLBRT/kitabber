// «Показать новые» (E08-08, FR-FEED, docs/ui/feed.md, ADR-0108): new posts of a chronological feed are announced, never
// put into it on their own. While the feed is on the screen and the tab is visible, its first page is read again every
// `newPostsInterval` and when the tab comes back into view; the posts above the newest one shown are counted. The page
// is read the way the feed is (the same threshold of «Свежее», ADR-0106), so the count is of the posts the reader would
// see. Pressing the banner puts them on top and scrolls up. A failed check is silent: the next one tries again.
import { computed, onBeforeUnmount, onMounted, ref, type Ref } from 'vue'
import {
  newerOverflow,
  newerPosts,
  showNewer,
  type FeedLookup,
  type FeedPage,
  type FeedPost,
} from '../api/feeds'

/** How often the first page is read again, in milliseconds. */
export const newPostsInterval = 60_000

export function useNewPosts(
  posts: Ref<FeedPost[] | null>,
  cursor: Ref<string | null>,
  loadFirst: () => Promise<FeedLookup>,
) {
  const waiting = ref<FeedPage | null>(null)
  // A check asked before a reset (another threshold, the feed read again) is dropped when it answers.
  let generation = 0
  let checking = false

  const count = computed(() =>
    waiting.value && posts.value ? newerPosts(posts.value, waiting.value).length : 0,
  )
  // The whole first page is new: there may be more than it counts.
  const more = computed(() =>
    waiting.value && posts.value ? newerOverflow(posts.value, waiting.value) : false,
  )

  async function check() {
    if (checking || !posts.value || document.visibilityState === 'hidden') return
    checking = true
    const asked = generation
    try {
      const first = await loadFirst()
      if (asked === generation && first.status === 'found') waiting.value = first.page
    } finally {
      checking = false
    }
  }

  function reset() {
    generation++
    waiting.value = null
  }

  function show() {
    if (!waiting.value || !posts.value) return
    const next = showNewer({ posts: posts.value, cursor: cursor.value }, waiting.value)
    reset()
    posts.value = next.posts
    cursor.value = next.cursor
    window.scrollTo({ top: 0 })
  }

  let timer: ReturnType<typeof setInterval> | null = null
  const onVisibility = () => {
    if (document.visibilityState === 'visible') void check()
  }
  onMounted(() => {
    timer = setInterval(() => void check(), newPostsInterval)
    document.addEventListener('visibilitychange', onVisibility)
  })
  onBeforeUnmount(() => {
    if (timer) clearInterval(timer)
    document.removeEventListener('visibilitychange', onVisibility)
    generation++
  })

  return { count, more, check, show, reset }
}
