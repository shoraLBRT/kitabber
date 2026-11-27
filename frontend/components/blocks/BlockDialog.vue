<script setup lang="ts">
// «Заблокировать» asks first (E11-06, FR-SUB, ADR-0091): the dialog names the person and says what blocking does, and
// «Заблокировать» adds them to the black list through the site's server (/bff/blocks). Once kept, the dialog closes
// with a toast and says so (`blocked`); a failure keeps it open with a toast. One dialog for the blog's page and the
// «…» menus of someone else's post and comment.
import { ref } from 'vue'
import UiButton from '../ui/UiButton.vue'
import UiModal from '../ui/UiModal.vue'
import { blocksApi, type BlocksApi } from '../../api/blocks'
import { useToast } from '../../composables/useToast'
import { t } from '../../i18n'

const props = defineProps<{
  /** The account to block. */
  accountId: string
  /** How the page names it: its blog's title. */
  name: string
  /** The calls; the browser's own (through the site's server) by default. */
  api?: BlocksApi
}>()

const emit = defineEmits<{ blocked: [] }>()

const open = defineModel<boolean>('open', { default: false })
const toast = useToast()
const busy = ref(false)

async function confirm() {
  if (busy.value) return
  busy.value = true
  try {
    const result = await (props.api ?? blocksApi()).block(props.accountId)
    if (result.status === 204) {
      open.value = false
      toast.show(t('blocks.blocked', { name: props.name }), { tone: 'success' })
      emit('blocked')
    } else
      toast.show(result.status === 401 ? t('blocks.signedOut') : t('blocks.blockFailed'), { tone: 'error' })
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <UiModal v-model:open="open" :title="t('blocks.confirm', { name })">
    <p class="block-dialog__text" data-testid="block-dialog-text">{{ t('blocks.confirmText') }}</p>
    <template #footer>
      <UiButton variant="secondary" data-autofocus @click="open = false">{{ t('actions.cancel') }}</UiButton>
      <UiButton variant="danger" :loading="busy" data-testid="block-confirm" @click="confirm">
        {{ t('blocks.block') }}
      </UiButton>
    </template>
  </UiModal>
</template>

<style scoped>
.block-dialog__text {
  margin: 0;
}
</style>
