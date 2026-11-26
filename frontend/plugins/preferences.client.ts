// Signed in, the account's settings follow the person to this device (E04-08, ADR-0070): read once when the site
// starts, and new choices saved to the account (composables/useAccountPreferences.ts). A guest keeps the browser's.
// The read starts once hydration is over: the server rendered the page before the settings were known, and an answer
// arriving during hydration would change the theme switch and the «Основные» section under Vue's feet.
import { preferencesApi } from '~/api/preferences'
import { connectAccountPreferences } from '~/composables/useAccountPreferences'

export default defineNuxtPlugin((nuxtApp) => {
  if (!useAccount().value.signedIn) return
  let started = false
  const start = () => {
    if (started) return
    started = true
    void connectAccountPreferences(preferencesApi())
  }
  if (!nuxtApp.isHydrating) start()
  else nuxtApp.hook('app:suspense:resolve', start)
})
