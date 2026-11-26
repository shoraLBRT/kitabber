// The «…» menu of a comment (E09-05, FR-COMMENT, docs/ui/comments.md, ADR-0059). For now it holds the author's items
// only: «Редактировать» while the time to change the text lasts and «Удалить» at any time. A reader's items (reply is
// under the text already; copy the link, report, block the author) and the post author's (pin, delete the branch) come
// with their own tasks, into this same list.
import { Pencil, Trash2 } from 'lucide-vue-next'
import { canEdit, type Comment } from '../api/comments'
import type { MenuItem } from '../types/ui'
import { t } from '../i18n'

export type CommentMenuAction = 'edit' | 'delete'

export interface CommentMenuItem extends MenuItem {
  id: CommentMenuAction
}

/** The items of a comment's menu: none for someone else's comment, a deleted one or one still being sent. */
export function commentMenuItems(comment: Comment, own: boolean, now: number): CommentMenuItem[] {
  if (!own || comment.deleted) return []
  const items: CommentMenuItem[] = []
  if (canEdit(comment, now)) items.push({ id: 'edit', label: t('actions.edit'), icon: Pencil })
  items.push({ id: 'delete', label: t('actions.delete'), icon: Trash2, danger: true })
  return items
}
