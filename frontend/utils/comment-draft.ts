// The draft of a comment form (E09-03, ADR-0056): the text being written waits in the browser's localStorage, one draft
// per post and per comment replied to, so a reload, a closed tab or a failed send does not lose it. The draft is gone
// once the server keeps the comment, or when the text is wiped out.
export const commentDraftPrefix = 'comments.draft'

/** Where the draft of the form under the post (`parentId` null) or of the reply to a comment lives. */
export function commentDraftKey(postId: string, parentId: string | null): string {
  return `${commentDraftPrefix}.${postId}.${parentId ?? 'post'}`
}

/** The browser's localStorage, or nothing where it is unavailable (the server, a blocked storage). */
export function draftStorage(): Storage | undefined {
  try {
    return typeof localStorage === 'undefined' ? undefined : localStorage
  } catch {
    return undefined
  }
}

export function readCommentDraft(storage: Storage | undefined, key: string): string {
  try {
    return storage?.getItem(key) ?? ''
  } catch {
    return ''
  }
}

/** Keeps the text; a blank one removes the draft. */
export function saveCommentDraft(storage: Storage | undefined, key: string, text: string): void {
  try {
    if (text.trim() === '') storage?.removeItem(key)
    else storage?.setItem(key, text)
  } catch {
    // Without storage the form still works; only the draft is not kept.
  }
}

/** Removes the draft once its comment is sent, unless the person has written another text since. */
export function forgetCommentDraft(storage: Storage | undefined, key: string, sent: string): void {
  try {
    if ((storage?.getItem(key) ?? '').trim() === sent.trim()) storage?.removeItem(key)
  } catch {
    // Nothing to forget without storage.
  }
}
