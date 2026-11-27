// What every comment of a tree shares (E09-02, E09-03): the authors read so far, where each branch's reading stands and
// how to read the rest of a branch, and what the forms need: who writes, which comments are still being sent, why a send
// failed and how to send, the person's own comments with how to change and delete them (E09-05), where a deep
// branch opens on a phone (E09-06), and the comments' likes (E10-01). CommentTree
// provides it once, so the nested CommentItems do not pass it down level by level.
import type { InjectionKey, Ref } from 'vue'
import type { Profile } from '../../api/profiles'
import type { Likes } from '../../composables/useLikes'

export type BranchStatus = 'loading' | 'error'

/**
 * Which form a comment comes from: `post` for the form under the post, otherwise the id of the comment replied to. It
 * keys the form's draft, its failure and the action repeated after the sign-in.
 */
export type FormTarget = string

export function formTarget(parentId: string | null): FormTarget {
  return parentId ?? 'post'
}

/**
 * A send that did not go through: its text, given back to the form, and the reason shown under it. `blocked`: the
 * author of the post or of the comment replied to blocked the person (E11-06, `403 comments.blocked`), so sending
 * again will not help.
 */
export type CommentFailure = { text: string; message: string; blocked?: boolean }

export type CommentTreeContext = {
  postId: Readonly<Ref<string>>
  signedIn: Readonly<Ref<boolean>>
  /** The deepest level a reply may take; no «Ответить» below it. */
  maxDepth: Readonly<Ref<number>>
  authors: Readonly<Ref<ReadonlyMap<string, Profile | null>>>
  branches: Readonly<Ref<Readonly<Record<string, BranchStatus>>>>
  /** The comments shown before the server kept them, by their local id. */
  pending: Readonly<Ref<Readonly<Record<string, true>>>>
  failures: Readonly<Ref<Readonly<Record<FormTarget, CommentFailure>>>>
  /** The person's own comments: they take «Редактировать» (while the time lasts) and «Удалить». */
  own: Readonly<Ref<Readonly<Record<string, true>>>>
  /** The likes of the comments (E10-01): their counts, the person's marks and the press; `null` shows none. */
  likes: Readonly<Ref<Likes | null>>
  /** The time the menus compare `editableUntil` with, moved on by the tree every half minute. */
  now: Readonly<Ref<number>>
  /** The form to open on arrival: the person came back signed in from the window its button opened. */
  resume: Readonly<Ref<FormTarget | null>>
  /**
   * The address of a branch's own screen when the replies a read left out open there (a phone, E09-06), `null` when
   * «Показать ещё» reads them in place.
   */
  branchLink: Readonly<Ref<((commentId: string) => string) | null>>
  more: (commentId: string) => void
  send: (text: string, parentId: string | null) => void
  /** Changes an own comment's text: `null` once kept, otherwise the reason to show under the form. */
  edit: (id: string, text: string) => Promise<string | null>
  /** Deletes an own comment: `false` when it failed. */
  remove: (id: string) => Promise<boolean>
  /** Forgets a failure once its form has shown it. */
  dismiss: (target: FormTarget) => void
  /** The reader may block the authors of others' comments (E11-06): signed in, with the own comments read. */
  canBlock: Readonly<Ref<boolean>>
  /** The reader blocked `accountId` from a comment's menu: that author's comments fold (ADR-0091). */
  blocked: (accountId: string) => void
  /** The comment a link named (E09-07, ADR-0107), highlighted; `null` when none. */
  highlighted: Readonly<Ref<string | null>>
  /** The permanent link of a comment (E09-07): its post's address with `#comment-<id>`; `null` when unknown. */
  commentLink: Readonly<Ref<((commentId: string) => string) | null>>
  /** A guest pressed a form's button: the sign-in window opens instead. */
  signIn: (target: FormTarget) => void
}

export const commentTreeKey: InjectionKey<CommentTreeContext> = Symbol('comment-tree')
