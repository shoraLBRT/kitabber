// Likes on posts and comments (E10-01, FR-REACT, ADR-0061). The post page reads the likes of the post and of its
// comments at once; a like is put and taken back one target at a time, and the server answers with the target's new
// count. The server refuses one's own post or comment and a guest comment; the page does not offer them either.
import type { ApiClient, components } from './client'

export type LikeState = components['schemas']['LikeView']
export type PostLikes = components['schemas']['PostLikesView']

/** What a like is on. */
export type LikeTarget = 'post' | 'comment'

/** How putting or taking back a like went: the new state, signed out, the server's refusal, or a failure. */
export type LikeChange =
  | { status: 'done'; state: LikeState }
  | { status: 'unauthorized' }
  | { status: 'refused'; message: string }
  | { status: 'error' }

export const noLikes: LikeState = { count: 0, liked: false }

const refusedMessages: Record<number, string> = {
  403: 'Этот пост или комментарий оценить нельзя.',
  404: 'Пост или комментарий не найден: возможно, его удалили.',
}

export function isLikeTarget(value: unknown): value is LikeTarget {
  return value === 'post' || value === 'comment'
}

/** The likes of the post `postId` and of its comments; `null` when they cannot be read. */
export async function loadLikes(client: ApiClient, postId: string): Promise<PostLikes | null> {
  try {
    const { data } = await client.GET('/api/v1/posts/{postId}/likes', { params: { path: { postId } } })
    return data ?? null
  } catch {
    return null
  }
}

/** Puts (`liked`) or takes back the like of the post or the comment `id`. */
export async function changeLike(
  client: ApiClient,
  target: LikeTarget,
  id: string,
  liked: boolean,
): Promise<LikeChange> {
  try {
    const { data, error, response } =
      target === 'post'
        ? liked
          ? await client.PUT('/api/v1/posts/{postId}/like', { params: { path: { postId: id } } })
          : await client.DELETE('/api/v1/posts/{postId}/like', { params: { path: { postId: id } } })
        : liked
          ? await client.PUT('/api/v1/comments/{id}/like', { params: { path: { id } } })
          : await client.DELETE('/api/v1/comments/{id}/like', { params: { path: { id } } })
    if (data) return { status: 'done', state: data }
    if (response.status === 401) return { status: 'unauthorized' }
    const detail = (error as { detail?: string } | undefined)?.detail
    const refused = refusedMessages[response.status]
    return refused ? { status: 'refused', message: detail ?? refused } : { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/** The state shown at once, before the server answers: the like put or taken back, the count moved by one. */
export function toggled(state: LikeState): LikeState {
  return state.liked
    ? { count: Math.max(0, state.count - 1), liked: false }
    : { count: state.count + 1, liked: true }
}

/** The comments' likes by comment id. */
export function commentLikes(likes: PostLikes): Record<string, LikeState> {
  return Object.fromEntries(likes.comments.map((c) => [c.commentId, { count: c.count, liked: c.liked }]))
}
