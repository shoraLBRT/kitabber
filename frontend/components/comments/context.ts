// What every comment of a tree shares (E09-02): the authors read so far, where each branch's reading stands and how to
// read the rest of a branch. CommentTree provides it once, so the nested CommentItems do not pass it down level by
// level.
import type { InjectionKey, Ref } from 'vue'
import type { Profile } from '../../api/profiles'

export type BranchStatus = 'loading' | 'error'

export type CommentTreeContext = {
  authors: Readonly<Ref<ReadonlyMap<string, Profile | null>>>
  branches: Readonly<Ref<Readonly<Record<string, BranchStatus>>>>
  more: (commentId: string) => void
}

export const commentTreeKey: InjectionKey<CommentTreeContext> = Symbol('comment-tree')
