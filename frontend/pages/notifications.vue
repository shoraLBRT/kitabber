<script setup lang="ts">
// «Уведомления» (E12-02, FR-NOTIFY, ADR-0093, docs/ui/notifications.md) at /notifications: the whole feed in the
// central column, the screen of the phone's «Уведомления» tab. The list is read in the browser through the site's
// server (server/routes/bff/notifications*.ts, ADR-0018) and marks the shown rows read, which lowers the counter of the
// bell and of the tab. A guest signs in first and comes back here.
import NotificationList from '~/components/notifications/NotificationList.vue'
import { notificationsApi } from '~/api/notifications'
import { unreadNotificationsKey } from '~/composables/useUnreadNotifications'
import { t } from '../i18n'

useHead({ title: t('common.notifications') })

const account = useAccount()
if (!account.value.signedIn) await navigateTo('/auth/sign-in?returnTo=/notifications', { external: true })

const api = notificationsApi()
const { data: unread } = useNuxtData<number>(unreadNotificationsKey)
</script>

<template>
  <section class="notifications-page" aria-labelledby="notifications-title" data-testid="notifications-page">
    <h1 id="notifications-title" class="notifications-page__title">{{ t('common.notifications') }}</h1>
    <NotificationList :api="api" @unread="unread = $event" />
  </section>
</template>

<style scoped>
.notifications-page {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.notifications-page__title {
  margin: 0;
  padding: 0 var(--space-5);
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-bold);
}
</style>
