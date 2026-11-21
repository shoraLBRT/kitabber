// The sign-in window (E03-04, ADR-0032, docs/spec.md: any action of a guest with a record opens it). One window per
// page, in app.vue (AuthWindowHost); a button that needs an account asks for it with `requireAccount`, and after the
// sign-in the same action runs again through `onSignedInAgain` on the page it was started on.
import { onMounted } from 'vue'
import { rememberPendingAction, tabStorage, takePendingAction } from '../utils/pending-action'

export type AuthWindowMode = 'sign-in' | 'registration'

export interface AuthWindowState {
  open: boolean
  mode: AuthWindowMode
  /** The action that opened the window, repeated after the sign-in; none for a plain "Войти". */
  action?: string
}

export function useAuthWindow() {
  return useState<AuthWindowState>('auth-window', () => ({ open: false, mode: 'sign-in' }))
}

/**
 * Runs `run` for a signed-in person; a guest gets the sign-in window instead, and `action` (a stable name such as
 * `like:post:42`) is remembered so that the same button repeats it after the sign-in (`onSignedInAgain`).
 */
export function useRequireAccount() {
  const account = useAccount()
  const authWindow = useAuthWindow()
  return (action: string, run: () => void): void => {
    if (account.value.signedIn) run()
    else authWindow.value = { open: true, mode: 'sign-in', action }
  }
}

/** Remembers the window's action for the current page right before the sign-in leaves the site. */
export function rememberAuthAction(action: string | undefined, path: string): void {
  if (action) rememberPendingAction(tabStorage(), action, path)
}

/** Repeats `action` once on mount when the person came back signed in from the window this action opened. */
export function onSignedInAgain(action: string, run: () => void): void {
  const account = useAccount()
  const route = useRoute()
  onMounted(() => {
    if (account.value.signedIn && takePendingAction(tabStorage(), action, route.fullPath)) run()
  })
}
