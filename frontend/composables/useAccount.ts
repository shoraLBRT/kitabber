// Whether the person is signed in (E18-02). The server fills it from its session (plugins/account.server.ts); the
// page gets a yes or no only, never a token and never the account_id (FR-ID: identifiers are not shown). The search
// history's key (ADR-0097) is a keyed hash of the account: it tells accounts apart without naming one.
import { t } from '../i18n'

export interface AccountState {
  signedIn: boolean
  /** Where this account's search history lives in the browser (utils/search-history.ts); absent for a guest. */
  historyKey?: string
}

export function useAccount() {
  return useState<AccountState>('account', () => ({ signedIn: false }))
}

/** The person as the shell shows them: a placeholder name until the profile exists (Blogs, #33). */
export function useShellUser() {
  const account = useAccount()
  return computed(() => (account.value.signedIn ? { name: t('shell.account') } : null))
}

/**
 * Signs the person out (E03-03): a form POST to the site's server, which revokes the session and sends the browser
 * through the provider's sign-out back to the home page. A full page load, so the realtime connection of the page
 * closes with it.
 */
export function signOut(): void {
  const form = document.createElement('form')
  form.method = 'post'
  form.action = '/auth/sign-out'
  document.body.append(form)
  form.submit()
}
