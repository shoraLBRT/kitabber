// What every comment of a tree shares (E09-02, E09-03): the authors read so far, where each branch's reading stands and
// how to read the rest of a branch, and what the forms need: who writes, which comments are still being sent, why a send
// failed and how to send. CommentTree provides it once, so the nested CommentItems do not pass it down level by level.
import type { InjectionKey, Ref } from 'vue'
import type { Profile } from '../../api/profiles'

export type BranchStatus = 'loading' | 'error'

/**
 * Which form a comment comes from: `post` for the form under the post, otherwise the id of the comment replied to. It
 * keys the form's draft, its failure and the action repeated after the sign-in.
 */
export type FormTarget = string

export function formTarget(parentId: string | null): FormTarget {
  return parentId ?? 'post'
}

/** A send that did not go through: its text, given back to the form, and the reason shown under it. */
export type CommentFailure = { text: string; message: string }

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
  /** The form to open on arrival: the person came back signed in from the window its button opened. */
  resume: Readonly<Ref<FormTarget | null>>
  more: (commentId: string) => void
  send: (text: string, parentId: string | null) => void
  /** Forgets a failure once its form has shown it. */
  dismiss: (target: FormTarget) => void
  /** A guest pressed a form's button: the sign-in window opens instead. */
  signIn: (target: FormTarget) => void
}

export const commentTreeKey: InjectionKey<CommentTreeContext> = Symbol('comment-tree')
