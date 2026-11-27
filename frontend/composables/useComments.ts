// The comments under a post (E09-02, ADR-0054), read in the browser once the page is shown: the post comes first and
// the comments never hold up its server render. One read brings some levels of the tree (fewer on a phone); a branch
// with replies left out is read on «Показать ещё». The authors' names and avatars follow, a few profiles at a time.
// A comment written (E09-03, ADR-0056) is in the tree at once, dimmed until the server keeps it; a failed send takes it
// out again and gives its text back to its form with the reason. The server puts the threads in the order the reader
// chose (E09-04, ADR-0058), remembered in the browser; another choice reads the tree again. The person's own comments
// (read through the site's server, ADR-0059) take «Редактировать» while the time to change them lasts and «Удалить»
// (E09-05); a deleted comment with replies stays as their stub. A branch's own screen (E09-06) reads only that branch.
// Under a post, the comments others write while it is open come in real time (E09-09, ADR-0112): each is read and
// waits behind «N новых», counted in the heading, so what the person reads does not move until they ask for them.
import { onMounted, ref, shallowRef } from 'vue'
import {
  commentLevels,
  commentPostedType,
  loadAuthors,
  loadBranch,
  loadCommentPlace,
  loadComments,
  parseCommentPosted,
  replaceComment,
  withArrived,
  unknownAuthors,
  withAuthorBlocked,
  withBranch,
  withComment,
  withoutComment,
  withDeleted,
  withEdited,
  type Comment,
  type CommentArrival,
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
import { useRealtimeTopic } from './useRealtime'
import type { RealtimeEvent } from '../realtime/channel'
import { t } from '../i18n'

const sendFailure = t('comments.sendFailed')
const signedOut = t('comments.signedOut')
const blockedByAuthor = t('comments.blockedByAuthor')
const editFailure = t('comments.editFailed')
const editSignedOut = t('comments.editSignedOut')

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

async function arrival(postId: string, commentId: string): Promise<CommentArrival> {
  try {
    return await $fetch<CommentArrival>('/bff/comment-arrival', { query: { postId, commentId } })
  } catch {
    return { comment: null, own: [] }
  }
}

async function ownComments(postId: string): Promise<string[]> {
  try {
    return (await $fetch<{ ids: string[] }>('/bff/own-comments', { query: { postId } })).ids
  } catch {
    return []
  }
}

/**
 * The comments under the post `postId`, or only the branch of the comment `branchId` on its own screen (E09-06): the
 * same reads, sends and changes, the tree starting at that comment. A branch of another post is not found there.
 */
export function useComments(postId: () => string, branchId: () => string | null = () => null) {
  const { $api } = useNuxtApp()
  const account = useAccount()
  const state = shallowRef<CommentsState>({ status: 'loading' })
  const branches = ref<Record<string, BranchStatus>>({})
  const authors = shallowRef(new Map<string, Profile | null>())
  const pending = ref<Record<string, true>>({})
  const failures = ref<Record<FormTarget, CommentFailure>>({})
  const own = ref<Record<string, true>>({})
  // The own comments are known (E11-06): only then may someone else's comment offer «Заблокировать автора», so the
  // person's own comment never does while they are read.
  const ownRead = ref(false)
  const sort = ref<CommentSort>('popular')
  // A phone reads fewer levels and opens a deeper branch on its own screen (E09-06); known once the page is shown.
  const phone = ref(false)
  /** The comments others wrote meanwhile (E09-09), read and waiting behind «N новых», in the order they came. */
  const arrived = shallowRef<Comment[]>([])
  /** The ones «N новых» put in the tree: marked «новый» like the new since the previous opening. */
  const live = ref<string[]>([])
  let levels: number = commentLevels.wide
  let sent = 0
  let reads = 0
  // The sends not answered yet: the event of one's own comment may come before the answer that tells it is one's own.
  let sending: Promise<unknown> = Promise.resolve()

  async function read(): Promise<CommentsState> {
    const root = branchId()
    if (root === null) {
      const found = await loadComments($api, postId(), levels, sort.value)
      return found.status === 'found'
        ? { status: 'found', comments: found.tree.comments, maxDepth: found.tree.maxDepth }
        : { status: 'error' }
    }
    const found = await loadBranch($api, root, levels)
    if (found.status === 'found' && found.tree.postId !== postId()) return { status: 'not-found' }
    return found.status === 'found'
      ? { status: 'found', comments: found.tree.comments, maxDepth: found.tree.maxDepth }
      : found
  }

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
    const current = ++reads
    const found = await read()
    if (current !== reads) return
    state.value = found
    if (found.status !== 'found') return
    // A new read already has the comments that came meanwhile: they no longer wait.
    const inTree = new Set(found.comments.map((comment) => comment.id))
    arrived.value = arrived.value.filter((comment) => !inTree.has(comment.id))
    await Promise.all([addAuthors(found.comments), addOwn()])
  }

  const known = (id: string) =>
    own.value[id] === true ||
    arrived.value.some((comment) => comment.id === id) ||
    (state.value.status === 'found' && state.value.comments.some((comment) => comment.id === id))

  // A new comment under the post (E09-09, ADR-0112), read through the site's server with the person's token: an author
  // the reader blocked is left out, as among the new since the previous opening (ADR-0109), and one's own, written here
  // or in another tab, is not new either.
  async function receive(event: RealtimeEvent) {
    if (event.type !== commentPostedType) return
    const posted = parseCommentPosted(event.data)
    if (!posted || posted.postId.toLowerCase() !== postId().toLowerCase()) return
    await sending
    if (known(posted.commentId)) return
    const found = await arrival(postId(), posted.commentId)
    own.value = { ...own.value, ...Object.fromEntries(found.own.map((id) => [id, true as const])) }
    const comment = found.comment
    if (comment === null || known(comment.id)) return
    arrived.value = [...arrived.value, comment]
    await addAuthors([comment])
  }

  /** «N новых»: the comments that came are put in the tree, each in its place, and marked «новый». */
  async function showArrived() {
    const batch = arrived.value
    if (batch.length === 0 || state.value.status !== 'found') return
    arrived.value = []
    const ids = new Set([...state.value.comments, ...batch].map((comment) => comment.id))
    // A reply whose parent is not shown counts under the deepest of its ancestors that is (ADR-0112).
    const places = await Promise.all(
      batch.map((comment) =>
        comment.parentId !== null && !ids.has(comment.parentId)
          ? loadCommentPlace($api, comment.id)
          : Promise.resolve(null),
      ),
    )
    update((comments) =>
      batch.reduce(
        (tree, comment, at) => withArrived(tree, comment, places[at]?.ancestors ?? [], sort.value),
        comments,
      ),
    )
    live.value = [...live.value, ...batch.map((comment) => comment.id)]
  }

  if (branchId() === null) useRealtimeTopic(`post:${postId()}`, (event) => void receive(event))

  async function addOwn() {
    if (!account.value.signedIn) return
    const ids = await ownComments(postId())
    own.value = { ...own.value, ...Object.fromEntries(ids.map((id) => [id, true as const])) }
    ownRead.value = true
  }

  /** The reader blocked `accountId` (E11-06): that author's comments fold at once. */
  function blocked(accountId: string) {
    update((comments) => withAuthorBlocked(comments, accountId))
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
      authorBlocked: false,
    }
    failures.value = Object.fromEntries(Object.entries(failures.value).filter(([key]) => key !== target))
    pending.value = { ...pending.value, [local.id]: true }
    update((comments) => withComment(comments, local, sort.value))

    const request = post(postId(), local.text, parentId)
    sending = Promise.all([sending, request])
    const result = await request
    pending.value = Object.fromEntries(Object.entries(pending.value).filter(([id]) => id !== local.id))
    if (result.status === 'sent') {
      update((comments) => replaceComment(comments, local.id, result.comment))
      own.value = { ...own.value, [result.comment.id]: true }
      forgetCommentDraft(draftStorage(), commentDraftKey(postId(), parentId), text)
      await addAuthors([result.comment])
      return
    }
    update((comments) => withoutComment(comments, local.id))
    if (result.status === 'blocked') {
      failures.value = { ...failures.value, [target]: { text, message: blockedByAuthor, blocked: true } }
      return
    }
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
      phone.value = true
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
    ownRead,
    sort,
    phone,
    blocked,
    arrived,
    live,
    showArrived,
    retry: load,
    more,
    send,
    edit,
    remove,
    dismiss,
    order,
  }
}
