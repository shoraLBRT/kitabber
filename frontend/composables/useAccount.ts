// Whether the person is signed in (E18-02). The server fills it from its session (plugins/account.server.ts); the
// page gets a yes or no only, never a token and never the account_id (FR-ID: identifiers are not shown).
export interface AccountState {
  signedIn: boolean
}

export function useAccount() {
  return useState<AccountState>('account', () => ({ signedIn: false }))
}

/** The person as the shell shows them: a placeholder name until the profile exists (Blogs, #33). */
export function useShellUser() {
  const account = useAccount()
  return computed(() => (account.value.signedIn ? { name: 'Аккаунт' } : null))
}
