// The realtime channel in the browser (E01-11, ADR-0019) as `useNuxtApp().$realtime` (composables/useRealtime.ts).
// It opens while the person is signed in. The hub is at `public.apiBase` + /hubs/realtime (the same origin behind the
// stand's proxy); the ticket comes from the site's own server route, which holds the access token (ADR-0018).
// SignalR is loaded only when a signed-in page needs it.
import { RealtimeChannel, type HubLike } from '~/realtime/channel'

export const hubPath = '/hubs/realtime'

async function ticket(): Promise<string | null> {
  try {
    const issued = await $fetch<{ ticket: string }>('/auth/realtime-ticket', { method: 'POST' })
    return issued.ticket
  } catch (error) {
    if ((error as { statusCode?: number }).statusCode === 401) return null
    throw error
  }
}

export default defineNuxtPlugin(async () => {
  const config = useRuntimeConfig()
  const account = useAccount()
  const signalr = await import('@microsoft/signalr')
  const channel = new RealtimeChannel({
    ticket,
    connect: ({ accessTokenFactory, retry }) =>
      new signalr.HubConnectionBuilder()
        .withUrl(`${config.public.apiBase}${hubPath}`, {
          accessTokenFactory,
          // The ticket authenticates; no cookie goes to the API.
          withCredentials: false,
        })
        .withAutomaticReconnect(retry)
        // A break is silent for the reader (docs/ui/comments.md); the console stays clean too.
        .configureLogging(import.meta.dev ? signalr.LogLevel.Information : signalr.LogLevel.None)
        .build() as unknown as HubLike,
  })
  watch(
    () => account.value.signedIn,
    (signedIn) => (signedIn ? channel.start() : void channel.stop()),
    { immediate: true },
  )
  return { provide: { realtime: channel } }
})
