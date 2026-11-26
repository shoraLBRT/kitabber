// The row of reactions with their counts (E10-03, FR-REACT, ADR-0072): the kinds put on a post or a comment, most
// popular first, the person's own one marked. Only «Нравится» exists for now; the set of reactions (E02-12, E10-02)
// adds kinds to `reactionKinds`, and the row, the sorting and the realtime event take them as they are.
import type { LikeState, LikeTarget } from './likes'

/** The kinds of reaction, in the order the row keeps for equal counts. */
export const reactionKinds = ['like'] as const

export type ReactionKind = (typeof reactionKinds)[number]

/** One kind of reaction on a target: how many people put it, and whether the person is one of them. */
export interface ReactionCount {
  kind: ReactionKind
  count: number
  mine: boolean
}

/** The realtime event type of new counts in the topic `post:<id>` (ReactionEvents in the Reactions module). */
export const reactionsChangedType = 'reactions.changed'

/** New counts of a post or of one of its comments, as the event brings them. It names no one. */
export interface ReactionsChanged {
  target: LikeTarget
  targetId: string
  counts: Partial<Record<ReactionKind, number>>
}

export function isReactionKind(value: unknown): value is ReactionKind {
  return (reactionKinds as readonly unknown[]).includes(value)
}

/**
 * The row's chips: the kinds someone put, most first, equal counts in the order of `reactionKinds`. The kinds in
 * `always` stay in the row with no count, so there is always something to press.
 */
export function sortReactions(
  reactions: readonly ReactionCount[],
  always: readonly ReactionKind[] = [],
): ReactionCount[] {
  return reactions
    .filter((r) => r.count > 0 || r.mine || always.includes(r.kind))
    .sort((a, b) => b.count - a.count || rank(a.kind) - rank(b.kind))
}

// A kind the page does not know yet goes after the known ones.
function rank(kind: ReactionKind): number {
  const at = reactionKinds.indexOf(kind)
  return at < 0 ? reactionKinds.length : at
}

/** The reactions of a target that has only likes so far. */
export function reactionsOfLikes(state: LikeState): ReactionCount[] {
  return [{ kind: 'like', count: state.count, mine: state.liked }]
}

/** Reads the data of a `reactions.changed` event; `null` when it is not one. Kinds the page does not know are skipped. */
export function parseReactionsChanged(data: unknown): ReactionsChanged | null {
  if (typeof data !== 'object' || data === null) return null
  const { target, targetId, reactions } = data as Record<string, unknown>
  if (
    (target !== 'post' && target !== 'comment') ||
    typeof targetId !== 'string' ||
    !Array.isArray(reactions)
  )
    return null
  const counts: Partial<Record<ReactionKind, number>> = {}
  for (const item of reactions as unknown[]) {
    const { kind, count } = (item ?? {}) as Record<string, unknown>
    if (isReactionKind(kind) && typeof count === 'number' && Number.isInteger(count) && count >= 0)
      counts[kind] = count
  }
  return { target, targetId, counts }
}

/** The likes of a target after new counts came: the count from the event, the own mark as it was. */
export function likesAfter(before: LikeState, change: ReactionsChanged): LikeState {
  return { count: change.counts.like ?? 0, liked: before.liked }
}
