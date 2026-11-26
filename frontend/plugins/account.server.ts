// Passes the server-side session to the page as signed in or not (composables/useAccount.ts), with the key of the
// account's search history in the browser (ADR-0097): a keyed hash of the account, never the account_id itself.
import { searchHistoryKeyOf } from '../server/utils/search-history-key'

export default defineNuxtPlugin(() => {
  const event = useRequestEvent()
  const auth = event?.context.auth
  useAccount().value = auth
    ? { signedIn: true, historyKey: searchHistoryKeyOf(auth.accountId, useRuntimeConfig().sessionPassword) }
    : { signedIn: false }
})
