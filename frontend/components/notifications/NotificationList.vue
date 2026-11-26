<script setup lang="ts">
// The notification feed (E12-02, FR-NOTIFY, ADR-0093, docs/ui/notifications.md), one list for the bell's panel and for
// the page /notifications: the rows newest first, each with the avatar and name of the person who acted, what they did
// and when; an unread row has a dot and the subtle background. The next page comes with «Показать ещё», by the cursor
// of the page before. The unread rows of every page shown are marked read at once, and the new number goes up with
// `unread` for the counter; the dots stay on those rows until the list is opened again, so the person still sees what
// is new. States: a skeleton of five rows, «Уведомлений пока нет», a failure with «Повторить». The calls come in as
// `api` (api/notifications.ts), so the list is tested without a server. Grouping and «Прочитать все» are E12-03.
import { computed, onMounted, ref, resolveComponent } from 'vue'
import UiAvatar from '../ui/UiAvatar.vue'
import UiButton from '../ui/UiButton.vue'
import UiSkeleton from '../ui/UiSkeleton.vue'
import UiState from '../ui/UiState.vue'
import {
  actionText,
  actorName,
  appendNotifications,
  notificationLink,
  unreadOf,
  type Notification,
  type NotificationPage,
  type NotificationsApi,
} from '../../api/notifications'
import { locale, t } from '../../i18n'

const props = withDefaults(defineProps<{ api: NotificationsApi; compact?: boolean }>(), { compact: false })
const emit = defineEmits<{ unread: [count: number]; open: [] }>()

const state = ref<'loading' | 'error' | 'ready'>('loading')
const items = ref<Notification[]>([])
const cursor = ref<string | null>(null)
const loadingMore = ref(false)
const moreFailed = ref(false)

const timeFormat = new Intl.DateTimeFormat(locale, {
  day: 'numeric',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
})

function pageOf(body: unknown): NotificationPage | null {
  const page = body as NotificationPage | undefined
  return Array.isArray(page?.items) ? page : null
}

// The unread rows just shown are marked read; a failure leaves them for the next time the list is opened.
async function markShown(rows: readonly Notification[]) {
  const ids = rows.filter((n) => !n.read).map((n) => n.id)
  if (ids.length === 0) return
  const count = unreadOf(await props.api.markRead(ids))
  if (count !== null) emit('unread', count)
}

async function load() {
  state.value = 'loading'
  const result = await props.api.load()
  const page = result.status === 200 ? pageOf(result.body) : null
  if (!page) {
    state.value = 'error'
    return
  }
  items.value = page.items
  cursor.value = page.nextCursor ?? null
  moreFailed.value = false
  state.value = 'ready'
  await markShown(page.items)
}

async function loadMore() {
  if (!cursor.value || loadingMore.value) return
  loadingMore.value = true
  const result = await props.api.load(cursor.value)
  loadingMore.value = false
  const page = result.status === 200 ? pageOf(result.body) : null
  if (!page) {
    moreFailed.value = true
    return
  }
  moreFailed.value = false
  items.value = appendNotifications(items.value, page.items)
  cursor.value = page.nextCursor ?? null
  await markShown(page.items)
}

onMounted(load)

const hasMore = computed(() => cursor.value !== null)
const link = resolveComponent('NuxtLink')
</script>

<template>
  <div
    :class="['notifications', { 'notifications--compact': compact }]"
    data-testid="notification-list"
    :aria-busy="state === 'loading'"
  >
    <div v-if="state === 'loading'" class="notifications__list" data-testid="notifications-loading">
      <span class="visually-hidden" role="status">{{ t('notifications.loading') }}</span>
      <div v-for="n in 5" :key="n" class="notifications__skeleton">
        <UiSkeleton shape="avatar" size="sm" />
        <div class="notifications__skeleton-text">
          <UiSkeleton size="sm" width="80%" />
          <UiSkeleton size="sm" width="40%" />
        </div>
      </div>
    </div>

    <UiState v-else-if="state === 'error'" tone="error" data-testid="notifications-error">
      {{ t('notifications.failed') }}
      <template #actions>
        <UiButton variant="secondary" @click="load()">{{ t('common.retry') }}</UiButton>
      </template>
    </UiState>

    <UiState v-else-if="items.length === 0" data-testid="notifications-empty">
      {{ compact ? t('notifications.emptyShort') : t('notifications.empty') }}
    </UiState>

    <template v-else>
      <ul class="notifications__list">
        <li
          v-for="item in items"
          :key="item.id"
          :class="['notifications__item', { 'notifications__item--unread': !item.read }]"
          data-testid="notification"
          :data-read="item.read ? 'true' : 'false'"
        >
          <component
            :is="notificationLink(item) ? link : 'div'"
            :to="notificationLink(item) ?? undefined"
            class="notifications__row"
            @click="emit('open')"
          >
            <span v-if="!item.read" class="notifications__dot" aria-hidden="true" />
            <UiAvatar :name="actorName(item)" :sources="item.actor.avatarSources" size="sm" decorative />
            <span class="notifications__body">
              <span class="notifications__text">
                <strong class="notifications__name">{{ actorName(item) }}</strong>
                {{ actionText(item) }}
                <span v-if="!item.read" class="visually-hidden">{{ t('notifications.unreadMark') }}</span>
              </span>
              <time class="notifications__time" :datetime="item.createdAt">{{
                timeFormat.format(new Date(item.createdAt))
              }}</time>
            </span>
          </component>
        </li>
      </ul>
      <div v-if="hasMore || moreFailed" class="notifications__more">
        <p v-if="moreFailed" class="notifications__error" role="alert">{{ t('notifications.moreFailed') }}</p>
        <UiButton
          variant="secondary"
          :loading="loadingMore"
          data-testid="notifications-more"
          @click="loadMore()"
        >
          {{ moreFailed ? t('common.retry') : t('notifications.more') }}
        </UiButton>
      </div>
    </template>
  </div>
</template>

<style scoped>
.notifications {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.notifications__list {
  display: flex;
  flex-direction: column;
  margin: 0;
  padding: 0;
  list-style: none;
}

.notifications__item {
  border-bottom: var(--size-border) solid var(--color-border);
}

.notifications__item--unread {
  background: var(--color-surface-subtle);
}

.notifications__row {
  position: relative;
  display: flex;
  align-items: flex-start;
  gap: var(--space-3);
  box-sizing: border-box;
  min-height: var(--size-touch);
  padding: var(--space-3) var(--space-5);
  color: var(--color-text);
  text-decoration: none;
}

.notifications__row:hover {
  color: var(--color-text);
}

.notifications__dot {
  position: absolute;
  top: 50%;
  left: var(--space-2);
  width: var(--space-2);
  height: var(--space-2);
  border-radius: var(--radius-full);
  background: var(--color-accent);
  transform: translateY(-50%);
}

.notifications__body {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  min-width: 0;
}

.notifications__text {
  font-size: var(--font-size-sm);
  overflow-wrap: anywhere;
}

.notifications__time {
  color: var(--color-muted);
  font-size: var(--font-size-xs);
}

.notifications__skeleton {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-3) var(--space-5);
}

.notifications__skeleton-text {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: var(--space-1);
}

.notifications__more {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: var(--space-2);
  padding: var(--space-4) var(--space-5);
}

.notifications__error {
  margin: 0;
  color: var(--color-danger);
}
</style>
