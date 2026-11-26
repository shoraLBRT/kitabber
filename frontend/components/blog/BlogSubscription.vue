<script setup lang="ts">
// The subscription line of a blog or topic header (E11-03, FR-SUB, docs/ui/blog.md, ADR-0085): the number of
// subscribers (and, for a personal blog, how many the owner follows) and the button «Подписаться» / «Вы подписаны».
// The owner sees the numbers without the button. The page holds the state (composables/useSubscription.ts); this only
// shows it and reports a press. On a phone the button takes the free width. With the blog's id the numbers open its lists
// (E04-06, ADR-0099): «Подписчики» and, for a personal blog, «Подписки».
import { computed, nextTick, ref } from 'vue'
import UiButton from '../ui/UiButton.vue'
import SubscriptionListDialog from './SubscriptionListDialog.vue'
import { compactCount, type SubscriptionListKind, type SubscriptionView } from '../../api/subscriptions'
import { t } from '../../i18n'

const props = defineProps<{
  state: SubscriptionView
  /** How many blogs the owner of a personal blog follows; none for a topic. */
  subscriptionCount?: number | null
  /** The reader runs this blog: the numbers only, no button. */
  own?: boolean
  busy?: boolean
  /** The blog the numbers belong to: they then open its lists. */
  blogId?: string
}>()

// The dialog is made at the first press and then opened, so it opens as a closed dialog does (focus, scroll lock).
const list = ref<SubscriptionListKind>('subscribers')
const listMade = ref(false)
const listOpen = ref(false)

async function openList(kind: SubscriptionListKind) {
  list.value = kind
  if (!listMade.value) {
    listMade.value = true
    await nextTick()
  }
  listOpen.value = true
}

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
      <button
        v-if="blogId"
        type="button"
        class="subscription__count"
        aria-haspopup="dialog"
        data-testid="subscriber-count"
        @click="openList('subscribers')"
      >
        {{ subscribers }}
      </button>
      <span v-else data-testid="subscriber-count">{{ subscribers }}</span>
      <template v-if="subscriptions">
        <span aria-hidden="true"> · </span>
        <button
          v-if="blogId"
          type="button"
          class="subscription__count"
          aria-haspopup="dialog"
          data-testid="subscription-count"
          @click="openList('subscriptions')"
        >
          {{ subscriptions }}
        </button>
        <span v-else data-testid="subscription-count">{{ subscriptions }}</span>
      </template>
    </p>
    <SubscriptionListDialog
      v-if="blogId && listMade"
      v-model:open="listOpen"
      :blog-id="blogId"
      :kind="list"
    />
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

.subscription__count {
  padding: 0;
  border: 0;
  background: none;
  color: inherit;
  font: inherit;
  cursor: pointer;
}

.subscription__count:hover {
  color: var(--color-text);
  text-decoration: underline;
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
