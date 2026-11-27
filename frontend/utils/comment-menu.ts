// The «…» menu of a comment (E09-05, FR-COMMENT, docs/ui/comments.md, ADR-0059). The author's items: «Редактировать»
// while the time to change the text lasts and «Удалить» at any time. A signed-in reader's item on someone else's
// comment: «Заблокировать автора» (E11-06, ADR-0091). The other reader's items (copy the link, report) and the post
// author's (pin, delete the branch) come with their own tasks, into this same list.
import { Ban, Pencil, Trash2 } from 'lucide-vue-next'
import { canEdit, type Comment } from '../api/comments'
import type { MenuItem } from '../types/ui'
import { t } from '../i18n'

export type CommentMenuAction = 'edit' | 'delete' | 'block'

export interface CommentMenuItem extends MenuItem {
  id: CommentMenuAction
}

/**
 * The items of a comment's menu: the author's for their own comment; «Заблокировать автора» for someone else's native
 * comment when the reader may block (`canBlock`: signed in) and has not blocked its author yet; none for a deleted
 * comment, a guest's one or one still being sent.
 */
export function commentMenuItems(
  comment: Comment,
  own: boolean,
  now: number,
  canBlock = false,
): CommentMenuItem[] {
  if (comment.deleted) return []
  if (!own)
    return canBlock && comment.kind === 'native' && comment.authorAccountId && !comment.authorBlocked
      ? [{ id: 'block', label: t('blocks.blockAuthor'), icon: Ban, danger: true }]
      : []
  const items: CommentMenuItem[] = []
  if (canEdit(comment, now)) items.push({ id: 'edit', label: t('actions.edit'), icon: Pencil })
  items.push({ id: 'delete', label: t('actions.delete'), icon: Trash2, danger: true })
  return items
}
