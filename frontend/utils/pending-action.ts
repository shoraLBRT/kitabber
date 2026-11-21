// The action a guest started before the sign-in window (E03-04, ADR-0032): the sign-in leaves the site for the
// provider's authorization request and comes back with a full page load, so the action and the page it was started on
// wait in the tab's sessionStorage. Back on that page, signed in, the action runs once; on another page, or after ten
// minutes (the life of the sign-in flow, server/utils/oidc.ts), it is forgotten.
export const pendingActionKey = 'auth.pending-action'
export const pendingActionLifetimeMs = 10 * 60 * 1000

interface PendingAction {
  action: string
  path: string
  at: number
}

/** The tab's sessionStorage, or nothing where it is unavailable (the server, a blocked storage). */
export function tabStorage(): Storage | undefined {
  try {
    return typeof sessionStorage === 'undefined' ? undefined : sessionStorage
  } catch {
    return undefined
  }
}

export function rememberPendingAction(
  storage: Storage | undefined,
  action: string,
  path: string,
  now = Date.now(),
): void {
  try {
    storage?.setItem(pendingActionKey, JSON.stringify({ action, path, at: now } satisfies PendingAction))
  } catch {
    // Without storage the person is still signed in; only the action is not repeated.
  }
}

/** True once for the action remembered on this page; a record of this action is gone after its first look either way. */
export function takePendingAction(
  storage: Storage | undefined,
  action: string,
  path: string,
  now = Date.now(),
): boolean {
  let pending: Partial<PendingAction> | undefined
  try {
    const raw = storage?.getItem(pendingActionKey)
    if (!raw) return false
    pending = JSON.parse(raw) as Partial<PendingAction>
  } catch {
    pending = undefined
  }
  if (pending?.action !== action) return false
  try {
    storage?.removeItem(pendingActionKey)
  } catch {
    return false
  }
  return (
    pending.path === path && typeof pending.at === 'number' && now - pending.at <= pendingActionLifetimeMs
  )
}
