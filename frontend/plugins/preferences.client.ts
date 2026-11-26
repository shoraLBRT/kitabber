// Signed in, the account's settings follow the person to this device (E04-08, ADR-0070): read once when the site
// starts, and new choices saved to the account (composables/useAccountPreferences.ts). A guest keeps the browser's.
import { preferencesApi } from '~/api/preferences'
import { connectAccountPreferences } from '~/composables/useAccountPreferences'

export default defineNuxtPlugin(() => {
  if (!useAccount().value.signedIn) return
  void connectAccountPreferences(preferencesApi())
})
