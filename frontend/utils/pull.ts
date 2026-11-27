// Pull to refresh (E08-14, ADR-0111): how far the mark above the feed opens for a finger moved `moved` pixels down.
// It follows the finger at half its speed, so the pull feels heavy, and stops at `pullLimit`; moving up opens nothing.

/** Letting go with the mark open this far reads the feed again, in pixels. */
export const pullThreshold = 64
/** The mark opens no further than this, in pixels. */
export const pullLimit = 96

export function pullDistance(moved: number): number {
  if (moved <= 0) return 0
  return Math.min(pullLimit, Math.round(moved / 2))
}
