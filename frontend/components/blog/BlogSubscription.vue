<script setup lang="ts">
// The subscription line of a blog or topic header (E11-03, FR-SUB, docs/ui/blog.md, ADR-0085): the number of
// subscribers (and, for a personal blog, how many the owner follows) and the button «Подписаться» / «Вы подписаны».
// The owner sees the numbers without the button. The page holds the state (composables/useSubscription.ts); this only
// shows it and reports a press. On a phone the button takes the free width.
import { computed } from 'vue'
import UiButton from '../ui/UiButton.vue'
import { compactCount, type SubscriptionView } from '../../api/subscriptions'
import { t } from '../../i18n'

const props = defineProps<{
  state: SubscriptionView
  /** How many blogs the owner of a personal blog follows; none for a topic. */
  subscriptionCount?: number | null
  /** The reader runs this blog: the numbers only, no button. */
  own?: boolean
  busy?: boolean
}>()

defineEmits<{ toggle: [] }>()

const subscribers = computed(() =>
  t('subscription.subscribers', {
    count: props.state.subscriberCount,
    shown: compactCount(props.state.subscriberCount),
  }),
)

const subscriptions = computed(() =>
  typeof props.subscriptionCount === 'number'
    ? t('subscription.subscriptions', {
        count: props.subscriptionCount,
        shown: compactCount(props.subscriptionCount),
      })
    : null,
)
</script>

<template>
  <div class="subscription" data-testid="subscription">
    <p class="subscription__counts" data-testid="subscription-counts">
      <span data-testid="subscriber-count">{{ subscribers }}</span>
      <template v-if="subscriptions">
        <span aria-hidden="true"> · </span>
        <span data-testid="subscription-count">{{ subscriptions }}</span>
      </template>
    </p>
    <div v-if="!own" class="subscription__actions">
      <UiButton
        :variant="state.subscribed ? 'secondary' : 'primary'"
        size="sm"
        :aria-pressed="state.subscribed ? 'true' : 'false'"
        :disabled="busy"
        data-testid="subscribe-button"
        @click="$emit('toggle')"
      >
        {{ state.subscribed ? t('subscription.subscribed') : t('subscription.subscribe') }}
      </UiButton>
    </div>
  </div>
</template>

<style scoped>
.subscription {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: var(--space-2);
  width: 100%;
}

.subscription__counts {
  margin: 0;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.subscription__actions {
  display: flex;
}

@media (max-width: 639px) {
  .subscription__actions {
    width: 100%;
  }

  .subscription__actions > :deep(.ui-button) {
    flex: 1;
  }
}
</style>
