// The order of comments a reader chose (E09-04, ADR-0058): one choice for every post, kept in the browser's
// localStorage, so the next post and the next visit open in the same order. Without a choice, or without storage,
// the comments come «Популярные».
import { isCommentSort, type CommentSort } from '../api/comments'

export const commentSortKey = 'comments.sort'

export function readCommentSort(storage: Storage | undefined): CommentSort {
  try {
    const kept = storage?.getItem(commentSortKey)
    return isCommentSort(kept) ? kept : 'popular'
  } catch {
    return 'popular'
  }
}

export function saveCommentSort(storage: Storage | undefined, sort: CommentSort): void {
  try {
    storage?.setItem(commentSortKey, sort)
  } catch {
    // Without storage the order still changes; only the next page does not remember it.
  }
}
