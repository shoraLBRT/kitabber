// The bookmarks of a page (E10-07, FR-REACT, ADR-0113): of a post and its comments on the post page (`loadPost`), of
// the posts of the cards on a feed (`loadPosts`). Only a signed-in person has them, so a guest's page reads nothing and
// a guest's press opens the sign-in window (ADR-0032); back on the page signed in, the bookmark is put by itself. A
// press shows the new state at once; the server's answer then replaces it, and a failure puts the old one back with a
// toast. The count of people who keep a post or a comment comes only to its author.
import { ref, watch, type InjectionKey } from 'vue'
import {
  bookmarkKey,
  feedBookmarks,
  isBookmarkTarget,
  maxFeedPosts,
  noBookmark,
  postBookmarks,
  toggled,
  type BookmarkChange,
  type BookmarkState,
  type BookmarkTarget,
  type FeedBookmarks,
  type PostBookmarks,
} from '../api/bookmarks'
import { pendingActionWith, tabStorage, takePendingAction } from '../utils/pending-action'
import { t } from '../i18n'

/** The action a guest's press remembers for the sign-in window: `bookmark:<post|comment>:<id>`. */
export function bookmarkAction(target: BookmarkTarget, id: string): string {
  return `bookmark:${target}:${id}`
}

async function send(target: BookmarkTarget, id: string, on: boolean): Promise<BookmarkChange> {
  try {
    return await $fetch<BookmarkChange>('/bff/bookmark', { method: 'POST', body: { target, id, on } })
  } catch {
    return { status: 'error' }
  }
}

/** The bookmarks of the page's posts and comments. */
export function useBookmarks() {
  const account = useAccount()
  const requireAccount = useRequireAccount()
  const route = useRoute()
  const toast = useToast()
  // Changed key by key, not replaced while the person stays: a reader of one target (a card, a comment's open menu)
  // is not run again when the state of another one comes.
  const states = ref<Record<string, BookmarkState>>({})
  /** The targets whose change the server has not answered yet, by key. */
  const busy = ref(new Set<string>())

  // Another person, or none, has other bookmarks: what was read is forgotten.
  watch(
    () => account.value.signedIn,
    () => {
      states.value = {}
    },
  )

  function of(target: BookmarkTarget, id: string): BookmarkState {
    return states.value[bookmarkKey(target, id)] ?? noBookmark
  }

  function isBusy(target: BookmarkTarget, id: string): boolean {
    return busy.value.has(bookmarkKey(target, id))
  }

  function merge(read: Record<string, BookmarkState>) {
    // A target being changed keeps the state its change shows; the answer brings the rest.
    for (const [key, state] of Object.entries(read)) if (!busy.value.has(key)) states.value[key] = state
  }

  /** Makes the person keep the target (`on`) or not; nothing while its last change is not answered. */
  async function change(target: BookmarkTarget, id: string, on: boolean) {
    const key = bookmarkKey(target, id)
    const before = of(target, id)
    if (busy.value.has(key) || before.bookmarked === on) return
    busy.value.add(key)
    states.value[key] = toggled(before)
    const result = await send(target, id, on)
    busy.value.delete(key)
    if (result.status === 'done') {
      states.value[key] = result.state
      toast.show(on ? t('bookmarks.added') : t('bookmarks.removed'), { tone: 'success' })
      return
    }
    states.value[key] = before
    toast.show(
      result.status === 'refused'
        ? result.message
        : result.status === 'unauthorized'
          ? t('bookmarks.signedOut')
          : t('bookmarks.failed'),
      { tone: 'error' },
    )
  }

  /** A press: the bookmark is put or taken out; a guest gets the sign-in window. */
  function toggle(target: BookmarkTarget, id: string) {
    requireAccount(bookmarkAction(target, id), () => void change(target, id, !of(target, id).bookmarked))
  }

  // The bookmark a guest asked for before the sign-in on this page: put once the states are read.
  async function resume() {
    if (!account.value.signedIn) return
    const action = pendingActionWith(tabStorage(), 'bookmark:')
    if (!action || !takePendingAction(tabStorage(), action, route.fullPath)) return
    const [, target, id] = action.split(':')
    if (isBookmarkTarget(target) && id) await change(target, id, true)
  }

  /** Reads the bookmarks of the post `postId` and of its comments, for its page. */
  async function loadPost(postId: string) {
    if (!account.value.signedIn) return
    const found = await $fetch<PostBookmarks>('/bff/bookmarks', { query: { postId } }).catch(() => null)
    // A failed read leaves the marks as they were: the buttons still work, and the server's answer brings the state.
    if (found) merge(postBookmarks(found))
    await resume()
  }

  /** Reads the bookmarks of the feed's posts `ids` that are not read yet. */
  async function loadPosts(ids: readonly string[]) {
    if (!account.value.signedIn) return
    const fresh = [...new Set(ids)].filter((id) => !(bookmarkKey('post', id) in states.value))
    for (let at = 0; at < fresh.length; at += maxFeedPosts) {
      const part = fresh.slice(at, at + maxFeedPosts)
      const found = await $fetch<FeedBookmarks>('/bff/bookmarks', { query: { ids: part } }).catch(() => null)
      if (found) merge(feedBookmarks(part, found))
    }
    await resume()
  }

  return { states, of, isBusy, toggle, loadPost, loadPosts }
}

export type Bookmarks = ReturnType<typeof useBookmarks>

/**
 * The person's bookmarks, provided once by app.vue for every page: the post page's action panel and comment menus and
 * the feed cards take them from there. Without it (a component on its own) no bookmark is offered.
 */
export const bookmarksKey: InjectionKey<Bookmarks> = Symbol('bookmarks')
