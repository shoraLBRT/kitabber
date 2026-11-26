// The query history of the header's search (E14-02, ADR-0097): it stays in this browser's localStorage, never on the
// server, the latest first, without repeats. Each account has its own list under a key the site's server derives from
// the account (useAccount().historyKey), so one account never sees another's queries in a shared browser; a guest has
// the list `search.history.guest`. Without storage (the server, a blocked storage) the search works without history.
import { draftStorage } from './comment-draft'

export const searchHistoryPrefix = 'search.history'

/** How many queries the history keeps. */
export const searchHistoryLimit = 10

/** Where the history of the account `historyKey` lives; a guest's when there is none. */
export function searchHistoryKey(historyKey: string | null | undefined): string {
  return `${searchHistoryPrefix}.${historyKey || 'guest'}`
}

/** The browser's localStorage, or nothing where it is unavailable. */
export const historyStorage = draftStorage

export function readSearchHistory(storage: Storage | undefined, key: string): string[] {
  try {
    const parsed: unknown = JSON.parse(storage?.getItem(key) ?? '[]')
    return Array.isArray(parsed)
      ? parsed
          .filter((q): q is string => typeof q === 'string' && q.trim() !== '')
          .slice(0, searchHistoryLimit)
      : []
  } catch {
    return []
  }
}

function write(storage: Storage | undefined, key: string, history: string[]): string[] {
  try {
    if (history.length === 0) storage?.removeItem(key)
    else storage?.setItem(key, JSON.stringify(history))
  } catch {
    // Without storage the search still works; only the history is not kept.
  }
  return history
}

/** Puts `query` first; the same query typed in another case or with other spaces is not repeated. */
export function rememberSearch(storage: Storage | undefined, key: string, query: string): string[] {
  const normalized = query.trim().split(/\s+/u).filter(Boolean).join(' ')
  if (!normalized) return readSearchHistory(storage, key)
  const same = (q: string) => q.toLocaleLowerCase('ru') === normalized.toLocaleLowerCase('ru')
  return write(
    storage,
    key,
    [normalized, ...readSearchHistory(storage, key).filter((q) => !same(q))].slice(0, searchHistoryLimit),
  )
}

/** Removes one query from the history. */
export function forgetSearch(storage: Storage | undefined, key: string, query: string): string[] {
  return write(
    storage,
    key,
    readSearchHistory(storage, key).filter((q) => q !== query),
  )
}

/** Empties the history. */
export function clearSearchHistory(storage: Storage | undefined, key: string): string[] {
  return write(storage, key, [])
}
