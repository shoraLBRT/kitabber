<script setup lang="ts">
// «Заблокировать» on someone else's personal blog (E11-06, FR-PROFILE «действия посетителя», FR-SUB, ADR-0091): a
// signed-in reader blocks the blog's owner after BlockDialog asks first. A blocked owner gets the line «Вы заблокировали
// этого человека» and «Разблокировать», which unblocks at once with a toast. `change` says what the list holds now, so
// the page keeps its profile current.
import { computed, ref, watch } from 'vue'
import BlockDialog from '../blocks/BlockDialog.vue'
import UiButton from '../ui/UiButton.vue'
import { blocksApi, type BlocksApi } from '../../api/blocks'
import { useToast } from '../../composables/useToast'
import { t } from '../../i18n'

const props = defineProps<{
  /** The owner of the blog. */
  accountId: string
  /** The blog's title. */
  name: string
  /** The reader blocked the owner already. */
  blocked: boolean
  /** The calls; the browser's own (through the site's server) by default. */
  api?: BlocksApi
}>()

const emit = defineEmits<{ change: [blocked: boolean] }>()

const toast = useToast()
const state = ref(props.blocked)
watch(
  () => props.blocked,
  (next) => (state.value = next),
)
const dialogOpen = ref(false)
const busy = ref(false)
const label = computed(() => (state.value ? t('blocks.unblock') : t('blocks.block')))

function onBlocked() {
  state.value = true
  emit('change', true)
}

async function press() {
  if (!state.value) {
    dialogOpen.value = true
    return
  }
  if (busy.value) return
  busy.value = true
  try {
    const result = await (props.api ?? blocksApi()).unblock(props.accountId)
    if (result.status === 204) {
      state.value = false
      emit('change', false)
      toast.show(t('blocks.unblocked', { name: props.name }), { tone: 'success' })
    } else
      toast.show(result.status === 401 ? t('blocks.signedOut') : t('blocks.unblockFailed'), { tone: 'error' })
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="blog-block" data-testid="blog-block">
    <p v-if="state" class="blog-block__note" data-testid="blog-blocked">{{ t('blocks.blockedHere') }}</p>
    <UiButton variant="secondary" size="sm" :loading="busy" data-testid="blog-block-button" @click="press">
      {{ label }}
    </UiButton>
    <BlockDialog
      v-model:open="dialogOpen"
      :account-id="accountId"
      :name="name"
      :api="api"
      @blocked="onBlocked"
    />
  </div>
</template>

<style scoped>
.blog-block {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
}

.blog-block__note {
  margin: 0;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}
</style>
