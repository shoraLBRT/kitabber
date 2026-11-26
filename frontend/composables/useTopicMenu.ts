// The menu's topics (E11-02, ADR-0088) for the desktop left menu and the phone «Меню» screen: asked in the browser after
// the page shows (server/routes/bff/topic-menu.get.ts), so a page render never waits on them; asked again when the
// person signs in or out and when a subscription changes (useSubscription). While they load or when they fail, the
// menu shows only «Показать все».
import { computed, watch } from 'vue'
import { topicMenuItems, type TopicMenu } from '../api/topics'

export const topicMenuKey = 'topic-menu'

export function useTopicMenu() {
  const account = useAccount()
  const { data, status, refresh } = useFetch<TopicMenu | null>('/bff/topic-menu', {
    key: topicMenuKey,
    server: false,
    lazy: true,
    default: () => null,
  })
  watch(
    () => account.value.signedIn,
    () => refresh(),
  )
  const topics = computed(() => topicMenuItems(data.value))
  return { topics, status }
}
