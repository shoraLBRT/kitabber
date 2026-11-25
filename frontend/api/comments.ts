// The comment tree under a post (E09-02, FR-COMMENT, ADR-0048, ADR-0054). The API gives comments in the order of the
// tree, some levels down, each with the number of replies under it at any depth; the replies a read left out come
// with the branch of their comment («Показать ещё N ответов»). The page keeps the comments as that one ordered list and
// builds the nested tree from it, so a loaded branch only takes the place of its comment's part of the list.
import type { ApiClient, components } from './client'
import type { Profile } from './profiles'

export type Comment = components['schemas']['CommentView']
export type CommentTree = components['schemas']['CommentTreeView']

export type CommentsLookup = { status: 'found'; tree: CommentTree } | { status: 'error' }

/**
 * What the comments block shows: the skeleton, a failure with «Повторить», or the comments in the order of the tree
 * with the deepest level a reply may take (`maxDepth`, CommentLimits on the server).
 */
export type CommentsState =
  { status: 'loading' } | { status: 'error' } | { status: 'found'; comments: Comment[]; maxDepth: number }

/**
 * How many levels of replies one read brings (docs/ui/comments.md): six on a wide screen, three on a phone, where the
 * branch line takes the room of the text. Deeper replies come by the button, the same number of levels at a time.
 */
export const commentLevels = { wide: 6, phone: 3 } as const

/**
 * The order of the comments under a post (E09-04, ADR-0058), kept by the server: «Популярные» — the threads with the
 * most replies first, the older among equals; «По дате» — the newest first. Replies keep the order of the tree.
 */
export type CommentSort = 'popular' | 'date'

export const commentSorts: readonly { id: CommentSort; label: string }[] = [
  { id: 'popular', label: 'Популярные' },
  { id: 'date', label: 'По дате' },
]

export function isCommentSort(value: unknown): value is CommentSort {
  return value === 'popular' || value === 'date'
}

/** The comments under a post, `levels` levels down, in the order `sort`. */
export async function loadComments(
  client: ApiClient,
  postId: string,
  levels: number,
  sort: CommentSort = 'popular',
): Promise<CommentsLookup> {
  try {
    const { data } = await client.GET('/api/v1/posts/{postId}/comments', {
      params: { path: { postId }, query: { levels, sort } },
    })
    return data ? { status: 'found', tree: data } : { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/** A comment and the replies under it, `levels` levels below it. */
export async function loadBranch(
  client: ApiClient,
  commentId: string,
  levels: number,
): Promise<CommentsLookup> {
  try {
    const { data } = await client.GET('/api/v1/comments/{id}/branch', {
      params: { path: { id: commentId }, query: { levels } },
    })
    return data ? { status: 'found', tree: data } : { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/**
 * A comment in the nested tree: its replies that were read, and how many replies under it a read left out right there
 * — the ones not under any reply that was read, so «Показать ещё» stands where the tree stops, not above it.
 */
export type CommentNode = { comment: Comment; replies: CommentNode[]; hidden: number }

/** The nested tree of comments in the order of the tree, one pass: a comment's replies are the deeper ones after it. */
export function nestComments(comments: readonly Comment[]): CommentNode[] {
  const roots: CommentNode[] = []
  const open: CommentNode[] = []
  for (const comment of comments) {
    while (open.length > 0 && open[open.length - 1]!.comment.depth >= comment.depth) open.pop()
    const node: CommentNode = { comment, replies: [], hidden: comment.replies }
    const parent = open[open.length - 1]
    if (parent) {
      parent.replies.push(node)
      // The reply and everything under it are the parent's, accounted for by the reply.
      parent.hidden = Math.max(0, parent.hidden - 1 - comment.replies)
    } else {
      roots.push(node)
    }
    open.push(node)
  }
  return roots
}

/**
 * The comments with a loaded branch in place of its comment's part: the comment and everything under it give way to the
 * branch, which begins with that comment. A branch of a comment that is not shown changes nothing.
 */
export function withBranch(comments: readonly Comment[], branch: readonly Comment[]): Comment[] {
  const root = branch[0]
  const at = root ? comments.findIndex((comment) => comment.id === root.id) : -1
  if (at < 0) return [...comments]
  let end = at + 1
  while (end < comments.length && comments[end]!.depth > comments[at]!.depth) end++
  return [...comments.slice(0, at), ...branch, ...comments.slice(end)]
}

/** Every comment under the post: each reply to it and everything under that. */
export function commentCount(comments: readonly Comment[]): number {
  return comments
    .filter((comment) => comment.parentId === null)
    .reduce((sum, root) => sum + 1 + root.replies, 0)
}

/** «3 ответа», «5 ответов», «21 ответ». */
export function repliesText(count: number): string {
  const word = { one: 'ответ', few: 'ответа', many: 'ответов' }[
    pluralRules.select(count) as 'one' | 'few' | 'many'
  ]
  return `${count} ${word ?? 'ответа'}`
}

const pluralRules = new Intl.PluralRules('ru')

/** The authors of native comments no profile is known for yet, each once. */
export function unknownAuthors(comments: readonly Comment[], known: ReadonlyMap<string, unknown>): string[] {
  const ids = comments.flatMap((comment) => (comment.authorAccountId ? [comment.authorAccountId] : []))
  return [...new Set(ids)].filter((id) => !known.has(id))
}

/** How many author profiles are asked for at the same time. */
export const authorLookups = 6

/**
 * The personal blogs of these accounts — the name and the avatar of a comment. A profile that cannot be read is
 * `null`: the comment goes without its author's name rather than failing.
 */
export async function loadAuthors(
  client: ApiClient,
  accountIds: readonly string[],
): Promise<Map<string, Profile | null>> {
  const found = new Map<string, Profile | null>()
  const queue = [...accountIds]
  const next = async (): Promise<void> => {
    const accountId = queue.shift()
    if (!accountId) return
    try {
      const { data } = await client.GET('/api/v1/profiles/by-account/{accountId}', {
        params: { path: { accountId } },
      })
      found.set(accountId, data ?? null)
    } catch {
      found.set(accountId, null)
    }
    return next()
  }
  await Promise.all(Array.from({ length: Math.min(authorLookups, queue.length) }, next))
  return found
}

/**
 * What became of a comment sent (E09-03): the comment the server kept; a refusal with the server's reason (an empty
 * text, a branch too deep); the person is not signed in any more; or the request failed and may be sent again.
 */
export type CommentSend =
  | { status: 'sent'; comment: Comment }
  | { status: 'refused'; message: string }
  | { status: 'unauthorized' }
  | { status: 'error' }

/** Writes a comment under the post, or a reply to `parentId`: POST /posts/{postId}/comments. */
export async function sendComment(
  client: ApiClient,
  postId: string,
  text: string,
  parentId: string | null,
): Promise<CommentSend> {
  try {
    const { data, error, response } = await client.POST('/api/v1/posts/{postId}/comments', {
      params: { path: { postId } },
      body: { text, parentId },
    })
    if (data) return { status: 'sent', comment: data }
    if (response.status === 400) {
      const errors = (error as { errors?: Record<string, string[]> } | undefined)?.errors ?? {}
      return { status: 'refused', message: Object.values(errors).flat()[0] ?? 'Комментарий не принят.' }
    }
    if (response.status === 401) return { status: 'unauthorized' }
    return { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/**
 * The comments with a new one where the tree puts it (E09-03): a reply after the replies of its parent that were read,
 * a comment under the post where the order `sort` puts a new thread with no replies — at the end of «Популярные», at
 * the top of «По дате»; every comment above it counts one reply more. A reply to a comment that is not shown changes
 * nothing.
 */
export function withComment(
  comments: readonly Comment[],
  added: Comment,
  sort: CommentSort = 'popular',
): Comment[] {
  if (added.parentId === null) return sort === 'date' ? [added, ...comments] : [...comments, added]
  const at = comments.findIndex((comment) => comment.id === added.parentId)
  if (at < 0) return [...comments]
  let end = at + 1
  while (end < comments.length && comments[end]!.depth > comments[at]!.depth) end++
  const above = ancestors(comments, added.parentId)
  const counted = comments.map((comment) =>
    above.has(comment.id) ? { ...comment, replies: comment.replies + 1 } : comment,
  )
  return [...counted.slice(0, end), added, ...counted.slice(end)]
}

/** The comments without one that has no replies, every comment above it counting one reply less: a send undone. */
export function withoutComment(comments: readonly Comment[], id: string): Comment[] {
  const removed = comments.find((comment) => comment.id === id)
  if (!removed) return [...comments]
  const above = removed.parentId === null ? new Set<string>() : ancestors(comments, removed.parentId)
  return comments
    .filter((comment) => comment.id !== id)
    .map((comment) =>
      above.has(comment.id) ? { ...comment, replies: Math.max(0, comment.replies - 1) } : comment,
    )
}

/** The comments with `id` replaced by `next` in the same place: the server's comment takes the sent one's place. */
export function replaceComment(comments: readonly Comment[], id: string, next: Comment): Comment[] {
  return comments.map((comment) => (comment.id === id ? { ...next, replies: comment.replies } : comment))
}

/** The comment `id` and every comment above it. */
function ancestors(comments: readonly Comment[], id: string): Set<string> {
  const byId = new Map(comments.map((comment) => [comment.id, comment]))
  const found = new Set<string>()
  for (let current = byId.get(id); current && !found.has(current.id);) {
    found.add(current.id)
    current = current.parentId === null ? undefined : byId.get(current.parentId)
  }
  return found
}

/** Whether a native comment may reply to this one: not to a guest comment (FR-TG-6), not past `maxDepth` levels. */
export function canReply(comment: Comment, maxDepth: number): boolean {
  return comment.kind !== 'guest' && comment.depth < maxDepth
}
