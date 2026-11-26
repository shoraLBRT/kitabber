<script setup lang="ts">
// The dialog behind the header's numbers (E04-06, FR-PROFILE, docs/ui/blog.md, ADR-0099): «Подписчики» of a blog or a
// topic, or «Подписки» of a personal blog, in a modal (a bottom sheet on a phone). The first page is read when the dialog
// opens and «Показать ещё» reads the next, through server/routes/bff/subscription-list.get.ts. «Подписаться» in a row
// changes at once and goes back with a toast when the server refuses; a guest's press opens the sign-in window
// (ADR-0032).
import { computed, ref, watch } from 'vue'
import UiModal from '../ui/UiModal.vue'
import SubscriptionList, { type SubscriptionListState } from './SubscriptionList.vue'
import {
  appendSubscriptionCards,
  type SubscriptionCard,
  type SubscriptionChange,
  type SubscriptionListKind,
  type SubscriptionListLookup,
} from '../../api/subscriptions'
import { t } from '../../i18n'

const props = defineProps<{ blogId: string; kind: SubscriptionListKind }>()
const open = defineModel<boolean>('open', { default: false })

const requireAccount = useRequireAccount()
const toast = useToast()

const status = ref<'loading' | 'error' | 'found'>('loading')
const cards = ref<SubscriptionCard[]>([])
const nextCursor = ref<string | null>(null)
const loadingMore = ref(false)
const moreFailed = ref(false)
const busy = ref<string[]>([])

const state = computed<SubscriptionListState>(() =>
  status.value === 'found'
    ? { status: 'found', cards: cards.value, hasMore: nextCursor.value !== null }
    : { status: status.value },
)

function read(cursor: string | null): Promise<SubscriptionListLookup> {
  return $fetch<SubscriptionListLookup>('/bff/subscription-list', {
    query: { blogId: props.blogId, kind: props.kind, ...(cursor ? { cursor } : {}) },
  }).catch((): SubscriptionListLookup => ({ status: 'error' }))
}

async function loadFirst() {
  status.value = 'loading'
  moreFailed.value = false
  const lookup = await read(null)
  if (lookup.status !== 'found') {
    status.value = 'error'
    return
  }
  cards.value = lookup.page.items
  nextCursor.value = lookup.page.nextCursor ?? null
  status.value = 'found'
}

async function loadMore() {
  if (loadingMore.value || !nextCursor.value) return
  loadingMore.value = true
  const lookup = await read(nextCursor.value)
  loadingMore.value = false
  if (lookup.status !== 'found') {
    moreFailed.value = true
    return
  }
  moreFailed.value = false
  cards.value = appendSubscriptionCards(cards.value, lookup.page)
  nextCursor.value = lookup.page.nextCursor ?? null
}

watch(
  open,
  (shown) => {
    if (shown) void loadFirst()
  },
  { immediate: true },
)

function mark(id: string, subscribed: boolean) {
  cards.value = cards.value.map((card) => (card.id === id ? { ...card, subscribed } : card))
}

async function change(card: SubscriptionCard) {
  if (busy.value.includes(card.id)) return
  const wanted = !card.subscribed
  busy.value = [...busy.value, card.id]
  mark(card.id, wanted)
  const result = await $fetch<SubscriptionChange>('/bff/subscription', {
    method: 'POST',
    body: { blogId: card.id, subscribed: wanted },
  }).catch((): SubscriptionChange => ({ status: 'error' }))
  busy.value = busy.value.filter((id) => id !== card.id)
  if (result.status === 'done') {
    mark(card.id, result.state.subscribed)
    return
  }
  mark(card.id, card.subscribed)
  toast.show(
    result.status === 'refused'
      ? result.message
      : result.status === 'unauthorized'
        ? t('subscription.signedOut')
        : t('subscription.failed'),
    { tone: 'error' },
  )
}

function toggle(card: SubscriptionCard) {
  requireAccount(`subscribe-list:${card.id}`, () => void change(card))
}
</script>

<template>
  <UiModal v-model:open="open" :title="t(`subscriptionList.${kind}`)">
    <SubscriptionList
      :kind="kind"
      :state="state"
      :loading-more="loadingMore"
      :more-failed="moreFailed"
      :busy="busy"
      @retry="loadFirst()"
      @more="loadMore()"
      @toggle="toggle"
      @leave="open = false"
    />
  </UiModal>
</template>
