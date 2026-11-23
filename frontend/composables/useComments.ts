// The comments under a post (E09-02, ADR-0054), read in the browser once the page is shown: the post comes first and
// the comments never hold up its server render. One read brings some levels of the tree (fewer on a phone); a branch
// with replies left out is read on «Показать ещё». The authors' names and avatars follow, a few profiles at a time.
import { onMounted, ref, shallowRef } from 'vue'
import {
  commentLevels,
  loadAuthors,
  loadBranch,
  loadComments,
  unknownAuthors,
  withBranch,
  type Comment,
  type CommentsState,
} from '../api/comments'
import type { Profile } from '../api/profiles'
import type { BranchStatus } from '../components/comments/context'
import { phoneQuery } from './useMediaQuery'

export function useComments(postId: () => string) {
  const { $api } = useNuxtApp()
  const state = shallowRef<CommentsState>({ status: 'loading' })
  const branches = ref<Record<string, BranchStatus>>({})
  const authors = shallowRef(new Map<string, Profile | null>())
  let levels: number = commentLevels.wide

  async function addAuthors(comments: Comment[]) {
    const ids = unknownAuthors(comments, authors.value)
    if (ids.length === 0) return
    // A new map, not the old one changed: the tree sees a new prop and draws the names.
    authors.value = new Map([...authors.value, ...(await loadAuthors($api, ids))])
  }

  async function load() {
    state.value = { status: 'loading' }
    const found = await loadComments($api, postId(), levels)
    state.value =
      found.status === 'found' ? { status: 'found', comments: found.tree.comments } : { status: 'error' }
    if (found.status === 'found') await addAuthors(found.tree.comments)
  }

  async function more(commentId: string) {
    if (branches.value[commentId] === 'loading') return
    branches.value = { ...branches.value, [commentId]: 'loading' }
    const found = await loadBranch($api, commentId, levels)
    const rest = Object.fromEntries(Object.entries(branches.value).filter(([id]) => id !== commentId))
    branches.value = found.status === 'found' ? rest : { ...rest, [commentId]: 'error' }
    if (found.status !== 'found' || state.value.status !== 'found') return
    state.value = { status: 'found', comments: withBranch(state.value.comments, found.tree.comments) }
    await addAuthors(found.tree.comments)
  }

  onMounted(() => {
    if (typeof window.matchMedia === 'function' && window.matchMedia(phoneQuery).matches) {
      levels = commentLevels.phone
    }
    void load()
  })

  return { state, branches, authors, retry: load, more }
}
