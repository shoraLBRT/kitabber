// Realtime events for pages and components (E01-11, ADR-0019). On the server and for a guest there is no channel:
// the page renders from the API and simply gets no live updates.
import type { RealtimeChannel, RealtimeHandler } from '~/realtime/channel'

export function useRealtime(): RealtimeChannel | null {
  return (useNuxtApp().$realtime as RealtimeChannel | undefined) ?? null
}

/**
 * Listens to the events of `topic` (`post:<id>`, or the own `account:<id>`) while the component is mounted, and
 * subscribes to the topic for that time. After a reconnect the subscription comes back on its own; events missed
 * during the break are not replayed, so a page reloads what it shows (docs/ui/comments.md).
 */
export function useRealtimeTopic(topic: string, handler: RealtimeHandler): void {
  const channel = useRealtime()
  const shared = topic.startsWith('account:')
  let off: (() => void) | null = null
  onMounted(() => {
    if (!channel) return
    off = channel.on(handler, topic)
    if (!shared) void channel.subscribe(topic)
  })
  onBeforeUnmount(() => {
    off?.()
    if (channel && !shared) void channel.unsubscribe(topic)
  })
}
