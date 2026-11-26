// The comments under a post (E09-02, ADR-0054), read in the browser once the page is shown: the post comes first and
// the comments never hold up its server render. One read brings some levels of the tree (fewer on a phone); a branch
// with replies left out is read on «Показать ещё». The authors' names and avatars follow, a few profiles at a time.
// A comment written (E09-03, ADR-0056) is in the tree at once, dimmed until the server keeps it; a failed send takes it
// out again and gives its text back to its form with the reason. The server puts the threads in the order the reader
// chose (E09-04, ADR-0058), remembered in the browser; another choice reads the tree again. The person's own comments
// (read through the site's server, ADR-0059) take «Редактировать» while the time to change them lasts and «Удалить»
// (E09-05); a deleted comment with replies stays as their stub.
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
  withDeleted,
  withEdited,
  type Comment,
  type CommentEdit,
  type CommentSend,
  type CommentSort,
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
import { readCommentSort, saveCommentSort } from '../utils/comment-sort'
import { phoneQuery } from './useMediaQuery'

const sendFailure = 'Не удалось отправить комментарий. Текст сохранён — попробуйте ещё раз.'
const signedOut = 'Войдите снова, чтобы отправить комментарий.'
const editFailure = 'Не удалось сохранить правку. Попробуйте ещё раз.'
const editSignedOut = 'Войдите снова, чтобы изменить комментарий.'

/** The send goes through the site's own server, which holds the access token (server/routes/bff/comments.post.ts). */
async function post(postId: string, text: string, parentId: string | null): Promise<CommentSend> {
  try {
    return await $fetch<CommentSend>('/bff/comments', { method: 'POST', body: { postId, text, parentId } })
  } catch {
    return { status: 'error' }
  }
}

async function patch(id: string, text: string): Promise<CommentEdit> {
  try {
    return await $fetch<CommentEdit>('/bff/comments', { method: 'PATCH', body: { id, text } })
  } catch {
    return { status: 'error' }
  }
}

async function ownComments(postId: string): Promise<string[]> {
  try {
    return (await $fetch<{ ids: string[] }>('/bff/own-comments', { query: { postId } })).ids
  } catch {
    return []
  }
}

export function useComments(postId: () => string) {
  const { $api } = useNuxtApp()
  const account = useAccount()
  const state = shallowRef<CommentsState>({ status: 'loading' })
  const branches = ref<Record<string, BranchStatus>>({})
  const authors = shallowRef(new Map<string, Profile | null>())
  const pending = ref<Record<string, true>>({})
  const failures = ref<Record<FormTarget, CommentFailure>>({})
  const own = ref<Record<string, true>>({})
  const sort = ref<CommentSort>('popular')
  let levels: number = commentLevels.wide
  let sent = 0
  let reads = 0

  async function addAuthors(comments: Comment[]) {
    const ids = unknownAuthors(comments, authors.value)
    if (ids.length === 0) return
    // A new map, not the old one changed: the tree sees a new prop and draws the names.
    authors.value = new Map([...authors.value, ...(await loadAuthors($api, ids))])
  }

  async function load() {
    state.value = { status: 'loading' }
    branches.value = {}
    // Only the last read counts: a quick change of order must not let an older answer draw over a newer one.
    const read = ++reads
    const found = await loadComments($api, postId(), levels, sort.value)
    if (read !== reads) return
    state.value =
      found.status === 'found'
        ? { status: 'found', comments: found.tree.comments, maxDepth: found.tree.maxDepth }
        : { status: 'error' }
    if (found.status !== 'found') return
    await Promise.all([addAuthors(found.tree.comments), addOwn()])
  }

  async function addOwn() {
    if (!account.value.signedIn) return
    const ids = await ownComments(postId())
    own.value = { ...own.value, ...Object.fromEntries(ids.map((id) => [id, true as const])) }
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
      editedAt: null,
      editableUntil: null,
      deleted: false,
      replies: 0,
    }
    failures.value = Object.fromEntries(Object.entries(failures.value).filter(([key]) => key !== target))
    pending.value = { ...pending.value, [local.id]: true }
    update((comments) => withComment(comments, local, sort.value))

    const result = await post(postId(), local.text, parentId)
    pending.value = Object.fromEntries(Object.entries(pending.value).filter(([id]) => id !== local.id))
    if (result.status === 'sent') {
      update((comments) => replaceComment(comments, local.id, result.comment))
      own.value = { ...own.value, [result.comment.id]: true }
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

  /** Changes the text of an own comment; `null` once the server keeps it, otherwise the reason shown under the form. */
  async function edit(id: string, text: string): Promise<string | null> {
    const result = await patch(id, text)
    if (result.status === 'edited') {
      update((comments) => withEdited(comments, result.comment))
      return null
    }
    return result.status === 'refused'
      ? result.message
      : result.status === 'unauthorized'
        ? editSignedOut
        : editFailure
  }

  /** Deletes an own comment; `false` when the server did not. */
  async function remove(id: string): Promise<boolean> {
    try {
      await $fetch('/bff/comments', { method: 'DELETE', query: { id } })
    } catch {
      return false
    }
    update((comments) => withDeleted(comments, id))
    own.value = Object.fromEntries(Object.entries(own.value).filter(([key]) => key !== id))
    return true
  }

  function dismiss(target: FormTarget) {
    failures.value = Object.fromEntries(Object.entries(failures.value).filter(([key]) => key !== target))
  }

  /** Another order: remembered for the next posts and read from the server. */
  function order(next: CommentSort) {
    if (next === sort.value) return
    sort.value = next
    saveCommentSort(draftStorage(), next)
    void load()
  }

  onMounted(() => {
    if (typeof window.matchMedia === 'function' && window.matchMedia(phoneQuery).matches) {
      levels = commentLevels.phone
    }
    sort.value = readCommentSort(draftStorage())
    void load()
  })

  return {
    state,
    branches,
    authors,
    pending,
    failures,
    own,
    sort,
    retry: load,
    more,
    send,
    edit,
    remove,
    dismiss,
    order,
  }
}
