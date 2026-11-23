// «Нет интернета» in the browser (E02-07, FR-UI, ADR-0057): `useOnline()` follows the online and offline events, and when
// the network comes back the data that failed to load while it was gone is read again (utils/network.ts).
import { failedKeys, watchNetwork } from '~/utils/network'

export default defineNuxtPlugin((nuxtApp) => {
  const online = useOnline()
  online.value = navigator.onLine

  watchNetwork(window, (now) => {
    const back = now && !online.value
    online.value = now
    if (!back) return
    const keys = failedKeys(nuxtApp.payload.data)
    if (keys.length > 0) void refreshNuxtData(keys)
  })
})
