<script setup lang="ts">
// A subscriber or subscription list (E04-06, FR-PROFILE, docs/ui/blog.md, ADR-0099): rows of blogs and topics, the
// newest subscriptions first, each leading to its page and with «Подписаться» / «Вы подписаны» (none on the reader's
// own blog). «Показать ещё» reads the next page. The states of docs/ui/README.md: skeletons while loading, a quiet text
// when the list is empty, an error with «Повторить». The dialog around it (SubscriptionListDialog.vue) holds the state.
import { computed } from 'vue'
import UiAvatar from '../ui/UiAvatar.vue'
import UiButton from '../ui/UiButton.vue'
import UiSkeleton from '../ui/UiSkeleton.vue'
import UiState from '../ui/UiState.vue'
import { blogTitle } from '../../api/profiles'
import { subsitePath } from '../../api/posts'
import type { SubscriptionCard, SubscriptionListKind } from '../../api/subscriptions'
import { t } from '../../i18n'

export type SubscriptionListState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'found'; cards: SubscriptionCard[]; hasMore: boolean }

const props = withDefaults(
  defineProps<{
    kind: SubscriptionListKind
    state: SubscriptionListState
    loadingMore?: boolean
    moreFailed?: boolean
    /** The rows whose subscription is being changed: their buttons wait. */
    busy?: readonly string[]
  }>(),
  { loadingMore: false, moreFailed: false, busy: () => [] },
)
const emit = defineEmits<{ retry: []; more: []; toggle: [card: SubscriptionCard]; leave: [] }>()

const emptyText = computed(() =>
  props.kind === 'subscribers'
    ? t('subscriptionList.emptySubscribers')
    : t('subscriptionList.emptySubscriptions'),
)
</script>

<template>
  <div class="subscription-list" :data-testid="`subscription-list-${kind}`">
    <div v-if="state.status === 'loading'" aria-busy="true" data-testid="subscription-list-loading">
      <span class="visually-hidden" role="status">{{ t('subscriptionList.loading') }}</span>
      <div v-for="row in 4" :key="row" class="subscription-list__row">
        <UiSkeleton shape="avatar" size="md" />
        <span class="subscription-list__text"><UiSkeleton width="50%" /></span>
      </div>
    </div>

    <UiState v-else-if="state.status === 'error'" tone="error">
      {{ t('subscriptionList.failed') }}
      <template #actions>
        <UiButton
          variant="secondary"
          size="sm"
          data-testid="subscription-list-retry"
          @click="emit('retry')"
          >{{ t('common.retry') }}</UiButton
        >
      </template>
    </UiState>

    <UiState v-else-if="state.cards.length === 0">{{ emptyText }}</UiState>

    <template v-else>
      <ul class="subscription-list__items" :aria-label="t(`subscriptionList.${kind}`)">
        <li
          v-for="card in state.cards"
          :key="card.id"
          class="subscription-list__row"
          data-testid="subscription-card"
        >
          <NuxtLink :to="subsitePath(card)" class="subscription-list__link" @click="emit('leave')">
            <UiAvatar :name="blogTitle(card)" :sources="card.avatarSources" size="md" decorative />
            <span class="subscription-list__text">
              <span class="subscription-list__name" data-testid="subscription-card-name">{{
                blogTitle(card)
              }}</span>
              <span v-if="card.type === 'topic'" class="subscription-list__kind">{{
                t('subscriptionList.topic')
              }}</span>
            </span>
          </NuxtLink>
          <UiButton
            v-if="!card.own"
            :variant="card.subscribed ? 'secondary' : 'primary'"
            size="sm"
            :aria-pressed="card.subscribed ? 'true' : 'false'"
            :disabled="busy.includes(card.id)"
            data-testid="subscription-card-button"
            @click="emit('toggle', card)"
          >
            {{ card.subscribed ? t('subscription.subscribed') : t('subscription.subscribe') }}
          </UiButton>
        </li>
      </ul>
      <p v-if="moreFailed" class="subscription-list__more-failed" role="alert">
        {{ t('subscriptionList.moreFailed') }}
      </p>
      <UiButton
        v-if="state.hasMore"
        variant="secondary"
        :loading="loadingMore"
        data-testid="subscription-list-more"
        @click="emit('more')"
        >{{ moreFailed ? t('common.retry') : t('subscriptionList.more') }}</UiButton
      >
    </template>
  </div>
</template>

<style scoped>
.subscription-list {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  min-width: 0;
}

.subscription-list__items {
  display: flex;
  flex-direction: column;
  margin: 0;
  padding: 0;
  list-style: none;
}

.subscription-list__row {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  min-width: 0;
  padding: var(--space-2) 0;
}

.subscription-list__link {
  display: flex;
  flex: 1;
  align-items: center;
  gap: var(--space-3);
  min-width: 0;
  color: inherit;
  text-decoration: none;
}

.subscription-list__text {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-width: 0;
}

.subscription-list__name {
  overflow: hidden;
  font-weight: var(--font-weight-medium);
  text-overflow: ellipsis;
  white-space: nowrap;
}

.subscription-list__kind,
.subscription-list__more-failed {
  margin: 0;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}
</style>
