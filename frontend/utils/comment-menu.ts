// The «…» menu of a comment (E09-05, FR-COMMENT, docs/ui/comments.md, ADR-0059). Any reader's item: «Скопировать
// ссылку», the comment's permanent link (E09-07, ADR-0107), when the page knows its post's address. The author's items:
// «Редактировать» while the time to change the text lasts and «Удалить» at any time. A signed-in reader's item on
// someone else's comment: «Заблокировать автора» (E11-06, ADR-0091). The reader's report and the post author's items
// (pin, delete the branch) come with their own tasks, into this same list.
import { Ban, Link, Pencil, Trash2 } from 'lucide-vue-next'
import { canEdit, type Comment } from '../api/comments'
import type { MenuItem } from '../types/ui'
import type { useToast } from '../composables/useToast'
import { t } from '../i18n'

export type CommentMenuAction = 'copy-link' | 'edit' | 'delete' | 'block'

export interface CommentMenuItem extends MenuItem {
  id: CommentMenuAction
}

/**
 * The items of a comment's menu: «Скопировать ссылку» first when it has a link (`linked`); then the author's for their
 * own comment, or «Заблокировать автора» for someone else's native comment when the reader may block (`canBlock`:
 * signed in) and has not blocked its author yet; none for a deleted comment, a guest's one aside from the link, or
 * one still being sent.
 */
export function commentMenuItems(
  comment: Comment,
  own: boolean,
  now: number,
  canBlock = false,
  linked = false,
): CommentMenuItem[] {
  if (comment.deleted) return []
  const items: CommentMenuItem[] = linked
    ? [{ id: 'copy-link', label: t('actions.copyLink'), icon: Link }]
    : []
  if (!own) {
    if (canBlock && comment.kind === 'native' && comment.authorAccountId && !comment.authorBlocked)
      items.push({ id: 'block', label: t('blocks.blockAuthor'), icon: Ban, danger: true })
    return items
  }
  if (canEdit(comment, now)) items.push({ id: 'edit', label: t('actions.edit'), icon: Pencil })
  items.push({ id: 'delete', label: t('actions.delete'), icon: Trash2, danger: true })
  return items
}

/**
 * The full link of a comment (E09-07): its permanent link (`commentPath`) on this site, without a query, so what is
 * copied names the comment and not how the reader came to it.
 */
export function commentUrl(link: string, origin: string): string {
  const url = new URL(link, origin)
  return `${url.origin}${url.pathname}${url.hash}`
}

/** Copies the comment's link and says how it went. */
export async function copyCommentLink(link: string, toast: ReturnType<typeof useToast>): Promise<void> {
  try {
    await navigator.clipboard.writeText(commentUrl(link, window.location.origin))
    toast.show(t('actions.linkCopied'), { tone: 'success' })
  } catch {
    toast.show(t('actions.copyLinkFailed'), { tone: 'error' })
  }
}
