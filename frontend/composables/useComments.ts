// The comments under a post (E09-02, ADR-0054), read in the browser once the page is shown: the post comes first and
// the comments never hold up its server render. One read brings some levels of the tree (fewer on a phone); a branch
// with replies left out is read on «Показать ещё». The authors' names and avatars follow, a few profiles at a time.
// A comment written (E09-03, ADR-0056) is in the tree at once, dimmed until the server keeps it; a failed send takes it
// out again and gives its text back to its form with the reason.
import { onMounted, ref, shallowRef } from 'vue'
import {
  commentLevels,
  loadAuthors,
  loadBranch,
  loadComments,
  replaceComment,
  unknownAuthors,
  withBranch,
  withComment,
  withoutComment,
  type Comment,
  type CommentSend,
  type CommentsState,
} from '../api/comments'
import type { Profile } from '../api/profiles'
import {
  formTarget,
  type BranchStatus,
  type CommentFailure,
  type FormTarget,
} from '../components/comments/context'
import { commentDraftKey, draftStorage, forgetCommentDraft } from '../utils/comment-draft'
import { phoneQuery } from './useMediaQuery'

const sendFailure = 'Не удалось отправить комментарий. Текст сохранён — попробуйте ещё раз.'
const signedOut = 'Войдите снова, чтобы отправить комментарий.'

/** The send goes through the site's own server, which holds the access token (server/routes/bff/comments.post.ts). */
async function post(postId: string, text: string, parentId: string | null): Promise<CommentSend> {
  try {
    return await $fetch<CommentSend>('/bff/comments', { method: 'POST', body: { postId, text, parentId } })
  } catch {
    return { status: 'error' }
  }
}

export function useComments(postId: () => string) {
  const { $api } = useNuxtApp()
  const state = shallowRef<CommentsState>({ status: 'loading' })
  const branches = ref<Record<string, BranchStatus>>({})
  const authors = shallowRef(new Map<string, Profile | null>())
  const pending = ref<Record<string, true>>({})
  const failures = ref<Record<FormTarget, CommentFailure>>({})
  let levels: number = commentLevels.wide
  let sent = 0

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
      found.status === 'found'
        ? { status: 'found', comments: found.tree.comments, maxDepth: found.tree.maxDepth }
        : { status: 'error' }
    if (found.status === 'found') await addAuthors(found.tree.comments)
  }

  async function more(commentId: string) {
    if (branches.value[commentId] === 'loading') return
    branches.value = { ...branches.value, [commentId]: 'loading' }
    const found = await loadBranch($api, commentId, levels)
    const rest = Object.fromEntries(Object.entries(branches.value).filter(([id]) => id !== commentId))
    branches.value = found.status === 'found' ? rest : { ...rest, [commentId]: 'error' }
    if (found.status !== 'found' || state.value.status !== 'found') return
    state.value = { ...state.value, comments: withBranch(state.value.comments, found.tree.comments) }
    await addAuthors(found.tree.comments)
  }

  /** The tree changed by `change` when it is shown; a send outlives a reload that failed meanwhile. */
  function update(change: (comments: Comment[]) => Comment[]) {
    if (state.value.status === 'found')
      state.value = { ...state.value, comments: change(state.value.comments) }
  }

  async function send(text: string, parentId: string | null) {
    if (state.value.status !== 'found') return
    const target = formTarget(parentId)
    const parent = parentId === null ? undefined : state.value.comments.find((c) => c.id === parentId)
    const local: Comment = {
      id: `local-${++sent}`,
      postId: postId(),
      parentId,
      depth: (parent?.depth ?? 0) + 1,
      kind: 'native',
      authorAccountId: null,
      guest: null,
      text: text.trim(),
      createdAt: new Date().toISOString(),
      replies: 0,
    }
    failures.value = Object.fromEntries(Object.entries(failures.value).filter(([key]) => key !== target))
    pending.value = { ...pending.value, [local.id]: true }
    update((comments) => withComment(comments, local))

    const result = await post(postId(), local.text, parentId)
    pending.value = Object.fromEntries(Object.entries(pending.value).filter(([id]) => id !== local.id))
    if (result.status === 'sent') {
      update((comments) => replaceComment(comments, local.id, result.comment))
      forgetCommentDraft(draftStorage(), commentDraftKey(postId(), parentId), text)
      await addAuthors([result.comment])
      return
    }
    update((comments) => withoutComment(comments, local.id))
    const message =
      result.status === 'refused'
        ? result.message
        : result.status === 'unauthorized'
          ? signedOut
          : sendFailure
    failures.value = { ...failures.value, [target]: { text, message } }
  }

  function dismiss(target: FormTarget) {
    failures.value = Object.fromEntries(Object.entries(failures.value).filter(([key]) => key !== target))
  }

  onMounted(() => {
    if (typeof window.matchMedia === 'function' && window.matchMedia(phoneQuery).matches) {
      levels = commentLevels.phone
    }
    void load()
  })

  return { state, branches, authors, pending, failures, retry: load, more, send, dismiss }
}
