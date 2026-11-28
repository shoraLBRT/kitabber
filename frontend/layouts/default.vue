<script setup lang="ts">
// Default layout: the shell (E02-05 desktop, E02-06 phone: bottom bar and floating "Написать" below 925 px).
// Signed in (E18-02), the shell shows the account menu; the name and avatar come with the profile (#33).
import NotificationList from '~/components/notifications/NotificationList.vue'
import SettingsNav from '~/components/settings/SettingsNav.vue'
import { notificationsApi } from '~/api/notifications'
import ShellSearch from '~/components/shell/ShellSearch.vue'
import { searchApi, searchResultsPath } from '~/api/search'
import ShellAside from '~/components/shell/ShellAside.vue'
import PopularCommentList from '~/components/comments/PopularCommentList.vue'
import { asideQuery } from '~/composables/useMediaQuery'
import { useNotificationChanges } from '~/composables/useUnreadNotifications'
import { t } from '~/i18n'

const route = useRoute()
const user = useShellUser()
// The floating «Написать» of a phone opens the quick post (E05-29, ADR-0055); its «Развернуть» leads on to /write.
const { sheetOpen } = useQuickPost()
// «Нет подключения» above the content while the browser is offline (E02-07, ADR-0057).
const online = useOnline()
// The menu's topics: the person's subscribed ones or the popular ones (E11-02, ADR-0088).
const { topics } = useTopicMenu()

// The unread counter of the bell and of the phone's «Уведомления» tab; the bell's panel lists the notifications and
// marks the shown ones read (E12-02, ADR-0093).
const { unread, set: setUnread, refresh: refreshUnread } = useUnreadNotifications()
// Live (E12-04, ADR-0123): a change of the notifications in the account's topic asks for the number again; a rise is
// said politely to a screen reader (docs/ui/notifications.md), a fall (read in another tab) is not.
const arrived = ref('')
useNotificationChanges(async () => {
  const before = unread.value
  await refreshUnread()
  arrived.value = unread.value > before ? t('notifications.arrived', { count: unread.value }) : ''
})
const counts = computed(() => ({ notifications: unread.value }))
const notifications = notificationsApi()
// The header's search (E14-02, ADR-0097): suggestions through /bff/search-suggestions, the history of this account.
const search = searchApi()
const account = useAccount()
// Enter with no suggestion picked opens the results in the section «Рейтинг» (E14-03, ADR-0124).
async function openResults(query: string, close: () => void) {
  close()
  await navigateTo(searchResultsPath(query))
}
// The right column's «Популярные комментарии» (E08-12, ADR-0105): read only while the column is on the screen.
const asideShown = useMediaQuery(asideQuery)
const popular = usePopularComments(asideShown)
// The account menu's "Выйти" (E03-03); its other items lead to pages of their own.
function onAccount(id: string) {
  if (id === 'logout') signOut()
}
</script>

<template>
  <ShellLayout
    :current-path="route.path"
    :user="user"
    :topics="topics"
    :write-button="route.meta.writeButton !== false"
    quick-post
    :offline="!online"
    :counts="counts"
    @account="onAccount"
    @write="sheetOpen = true"
  >
    <slot />
    <span class="visually-hidden" aria-live="polite" data-testid="notifications-arrived">{{ arrived }}</span>
    <template #search="{ close }">
      <ShellSearch
        :api="search"
        :history-key="account.historyKey ?? null"
        @close="close()"
        @search="(q: string) => openResults(q, close)"
      />
    </template>
    <template #notifications="{ close }">
      <NotificationList :api="notifications" compact @unread="setUnread" @open="close()" />
    </template>
    <!-- The settings put their sections in the right column instead of the widgets (E04-08). -->
    <template v-if="route.meta.settingsNav" #aside>
      <SettingsNav :current-path="route.path" />
    </template>
    <template v-else #aside>
      <ShellAside
        :comments-state="popular.state.value"
        @retry="(widget) => widget === 'comments' && popular.refresh()"
      >
        <template #comments>
          <PopularCommentList :comments="popular.comments.value" :authors="popular.authors.value" />
        </template>
      </ShellAside>
    </template>
  </ShellLayout>
</template>
