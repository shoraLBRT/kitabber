// Reactions on posts and comments (E10-01, E10-02, E10-03, FR-REACT, ADR-0061, ADR-0072, ADR-0101). A person has one
// reaction on a target: a kind of the set (`GET /api/v1/reactions/set`, ADR-0095) is put, changed to another one in one
// request, and taken back. The post page reads the reactions of the post and of its comments at once; the server
// answers every change with the target's new counts, and the realtime event `reactions.changed` brings the counts of
// other readers' changes. The server refuses one's own post or comment and a guest comment; the page does not offer
// them either.
import type { ApiClient, components } from './client'
import { t } from '../i18n'

/** The kinds of reaction, in the order the row keeps for equal counts: the codes of the bundled pictures. */
export const reactionKinds = ['like', 'heart', 'smile', 'clap', 'idea', 'wow', 'sad'] as const

export type ReactionKind = (typeof reactionKinds)[number]

/** What a reaction is on. */
export type ReactionTarget = 'post' | 'comment'

/** The reactions of one target as the page keeps them: the count of every kind put, and the person's own one. */
export interface ReactionState {
  counts: Partial<Record<ReactionKind, number>>
  mine: ReactionKind | null
}

export const noReactions: ReactionState = { counts: {}, mine: null }

/** One kind of reaction on a target: how many people put it, and whether the person is one of them. */
export interface ReactionCount {
  kind: ReactionKind
  count: number
  mine: boolean
}

export type ReactionsView = components['schemas']['ReactionsView']
export type PostReactions = components['schemas']['PostReactionsView']

/** How a change went: the new state, signed out, the server's refusal, or a failure. */
export type ReactionChange =
  | { status: 'done'; state: ReactionState }
  | { status: 'unauthorized' }
  | { status: 'refused'; message: string }
  | { status: 'error' }

/** The realtime event type of new counts in the topic `post:<id>` (ReactionEvents in the Reactions module). */
export const reactionsChangedType = 'reactions.changed'

/** New counts of a post or of one of its comments, as the event brings them. It names no one. */
export interface ReactionsChanged {
  target: ReactionTarget
  targetId: string
  counts: Partial<Record<ReactionKind, number>>
}

export function isReactionKind(value: unknown): value is ReactionKind {
  return (reactionKinds as readonly unknown[]).includes(value)
}

export function isReactionTarget(value: unknown): value is ReactionTarget {
  return value === 'post' || value === 'comment'
}

// The counts of a list of kinds, skipping the kinds this page does not know and impossible counts.
function countsOf(items: readonly unknown[]): Partial<Record<ReactionKind, number>> {
  const counts: Partial<Record<ReactionKind, number>> = {}
  for (const item of items) {
    const { kind, count } = (item ?? {}) as Record<string, unknown>
    if (isReactionKind(kind) && typeof count === 'number' && Number.isInteger(count) && count > 0)
      counts[kind] = count
  }
  return counts
}

/** The state of an answer of the API; a kind the page does not know is left out. */
export function stateOf(view: Pick<ReactionsView, 'reactions' | 'mine'>): ReactionState {
  return { counts: countsOf(view.reactions), mine: isReactionKind(view.mine) ? view.mine : null }
}

/** The reactions of the post and of its comments by comment id. */
export function postReactions(view: PostReactions): {
  post: ReactionState
  comments: Record<string, ReactionState>
} {
  return {
    post: stateOf(view.post),
    comments: Object.fromEntries(view.comments.map((c) => [c.commentId, stateOf(c)])),
  }
}

/**
 * The state shown at once, before the server answers: the person's reaction becomes `kind` (`null` takes it back),
 * the old kind loses one, the new one gains one.
 */
export function chosen(state: ReactionState, kind: ReactionKind | null): ReactionState {
  const counts: Partial<Record<ReactionKind, number>> = {}
  for (const each of reactionKinds) {
    const count = (state.counts[each] ?? 0) - (state.mine === each ? 1 : 0) + (kind === each ? 1 : 0)
    if (count > 0) counts[each] = count
  }
  return { counts, mine: kind }
}

/** The reactions of a target for its row (ReactionRow): every kind with a count, and the own one. */
export function rowOf(state: ReactionState): ReactionCount[] {
  const kinds = reactionKinds.filter((kind) => (state.counts[kind] ?? 0) > 0 || state.mine === kind)
  return kinds.map((kind) => ({ kind, count: state.counts[kind] ?? 0, mine: state.mine === kind }))
}

/**
 * The row's chips: the kinds someone put, most first, equal counts in the order of `reactionKinds`. The kinds in
 * `always` stay in the row with no count, so there is always something to press.
 */
export function sortReactions(
  reactions: readonly ReactionCount[],
  always: readonly ReactionKind[] = [],
): ReactionCount[] {
  const missing = always
    .filter((kind) => !reactions.some((r) => r.kind === kind))
    .map((kind) => ({ kind, count: 0, mine: false }))
  return [...reactions, ...missing]
    .filter((r) => r.count > 0 || r.mine || always.includes(r.kind))
    .sort((a, b) => b.count - a.count || rank(a.kind) - rank(b.kind))
}

// A kind the page does not know yet goes after the known ones.
function rank(kind: ReactionKind): number {
  const at = reactionKinds.indexOf(kind)
  return at < 0 ? reactionKinds.length : at
}

/** Reads the data of a `reactions.changed` event; `null` when it is not one. Kinds the page does not know are skipped. */
export function parseReactionsChanged(data: unknown): ReactionsChanged | null {
  if (typeof data !== 'object' || data === null) return null
  const { target, targetId, reactions } = data as Record<string, unknown>
  if (!isReactionTarget(target) || typeof targetId !== 'string' || !Array.isArray(reactions)) return null
  return { target, targetId, counts: countsOf(reactions as unknown[]) }
}

/** The reactions of a target after new counts came: the counts from the event, the own reaction as it was. */
export function reactionsAfter(before: ReactionState, change: ReactionsChanged): ReactionState {
  return { counts: change.counts, mine: before.mine }
}

const refusedMessages: Record<number, string> = {
  400: t('reactions.unavailable'),
  403: t('reactions.forbidden'),
  404: t('reactions.notFound'),
}

/** The reactions of the post `postId` and of its comments; `null` when they cannot be read. */
export async function loadReactions(client: ApiClient, postId: string): Promise<PostReactions | null> {
  try {
    const { data } = await client.GET('/api/v1/posts/{postId}/reactions', { params: { path: { postId } } })
    return data ?? null
  } catch {
    return null
  }
}

/** Puts the reaction `kind` on the post or the comment `id`, changing the person's one; `null` takes it back. */
export async function changeReaction(
  client: ApiClient,
  target: ReactionTarget,
  id: string,
  kind: ReactionKind | null,
): Promise<ReactionChange> {
  try {
    const { data, error, response } =
      target === 'post'
        ? kind
          ? await client.PUT('/api/v1/posts/{postId}/reaction', {
              params: { path: { postId: id } },
              body: { kind },
            })
          : await client.DELETE('/api/v1/posts/{postId}/reaction', { params: { path: { postId: id } } })
        : kind
          ? await client.PUT('/api/v1/comments/{id}/reaction', { params: { path: { id } }, body: { kind } })
          : await client.DELETE('/api/v1/comments/{id}/reaction', { params: { path: { id } } })
    if (data) return { status: 'done', state: stateOf(data) }
    if (response.status === 401) return { status: 'unauthorized' }
    const refused = refusedMessages[response.status]
    // A validation problem's detail is technical; the page says it in its own words.
    const detail = response.status === 400 ? undefined : (error as { detail?: string } | undefined)?.detail
    return refused ? { status: 'refused', message: detail ?? refused } : { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/** One reaction the platform offers (E02-12, ADR-0095): its code, which is the code of its picture, and its name. */
export type ReactionSetItem = components['schemas']['ReactionSetItem']

export type ReactionSetLookup = { status: 'found'; items: ReactionSetItem[] } | { status: 'error' }

/** The set of reactions from the directory (GET /api/v1/reactions/set), in its order; no account needed. */
export async function loadReactionSet(client: ApiClient): Promise<ReactionSetLookup> {
  try {
    const { data } = await client.GET('/api/v1/reactions/set')
    return data ? { status: 'found', items: data } : { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}
