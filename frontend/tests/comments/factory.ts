import type { Comment } from '../../api/comments'

/** A native comment of the post `p1` by the account `author`, with `replies` replies under it at any depth. */
export function comment(
  id: string,
  parentId: string | null,
  depth: number,
  replies = 0,
  author = 'a1',
): Comment {
  return {
    id,
    postId: 'p1',
    parentId,
    depth,
    kind: 'native',
    authorAccountId: author,
    guest: null,
    text: `Комментарий ${id}`,
    createdAt: '2026-11-20T12:00:00Z',
    editedAt: null,
    editableUntil: '2026-11-20T12:15:00Z',
    deleted: false,
    replies,
    authorBlocked: false,
  }
}
