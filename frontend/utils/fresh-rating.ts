// Where a guest's rating filter of «Свежее» is kept (E08-07, ADR-0106): this browser's storage, like a guest's theme
// (ADR-0070). A signed-in person's filter is kept in the account instead. Storage may be blocked or hold a value of an
// older version; either reads as «все».
import { isFreshRating, type FreshRating } from '../api/feeds'

/** The key of the guest's filter in `localStorage`; no entry is «все». */
export const freshRatingStorageKey = 'fresh-min-rating'

/** The guest's remembered threshold, or `null` («все») when there is none or it is not a threshold. */
export function readFreshRating(storage: Pick<Storage, 'getItem'>): FreshRating {
  try {
    const saved = storage.getItem(freshRatingStorageKey)
    if (saved === null) return null
    const value = Number(saved)
    return isFreshRating(value) ? value : null
  } catch {
    return null
  }
}

/** Remembers the guest's threshold; «все» leaves no entry. */
export function rememberFreshRating(
  storage: Pick<Storage, 'setItem' | 'removeItem'>,
  rating: FreshRating,
): void {
  try {
    if (rating === null) storage.removeItem(freshRatingStorageKey)
    else storage.setItem(freshRatingStorageKey, String(rating))
  } catch {
    // Storage may be blocked; the filter still holds for this visit.
  }
}

/** The threshold of the account's saved settings; anything that is not a threshold reads as «все». */
export function accountFreshRating(
  preferences: { feeds?: { freshMinRating?: number | null } } | null,
): FreshRating {
  const saved = preferences?.feeds?.freshMinRating ?? null
  return isFreshRating(saved) ? saved : null
}
