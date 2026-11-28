<script setup lang="ts">
// «Скрыть из лент» on the topic page (E11-07, FR-TOPIC, docs/ui/topic.md, ADR-0119), for a signed-in reader: the posts
// of a hidden topic leave the reader's feeds, while the topic's own page still shows them. The state is read once the
// page is in the browser; until then, and when it cannot be read, the button is not shown. A press changes the button
// when the server agrees, and a failure says so in a toast. The calls come in as `api` (api/hidden.ts).
import { EyeOff, Eye } from 'lucide-vue-next'
import { onMounted, ref } from 'vue'
import UiButton from '../ui/UiButton.vue'
import type { HiddenApi } from '../../api/hidden'
import { useToast } from '../../composables/useToast'
import { t } from '../../i18n'

const props = defineProps<{ blogId: string; api: HiddenApi }>()

const toast = useToast()
const hidden = ref<boolean | null>(null)
const busy = ref(false)

onMounted(async () => {
  const result = await props.api.topicState(props.blogId)
  const body = result.body as { hidden?: unknown } | undefined
  if (result.status === 200 && typeof body?.hidden === 'boolean') hidden.value = body.hidden
})

async function toggle() {
  if (hidden.value === null || busy.value) return
  busy.value = true
  const hide = !hidden.value
  const result = hide
    ? await props.api.hide('topics', props.blogId)
    : await props.api.show('topics', props.blogId)
  busy.value = false
  if (result.status === 204) {
    hidden.value = hide
    toast.show(hide ? t('hidden.topicHidden') : t('hidden.topicShown'), { tone: 'success' })
    return
  }
  toast.show(result.status === 401 ? t('settings.sessionEnded') : t('hidden.hideFailed'), { tone: 'error' })
}
</script>

<template>
  <div v-if="hidden !== null" class="topic-hide" data-testid="topic-hide">
    <UiButton variant="secondary" :loading="busy" :aria-pressed="hidden" @click="toggle">
      <component :is="hidden ? Eye : EyeOff" class="topic-hide__icon" aria-hidden="true" />
      {{ hidden ? t('hidden.showTopic') : t('hidden.hideTopic') }}
    </UiButton>
    <p v-if="hidden" class="topic-hide__note" data-testid="topic-hidden-note">
      {{ t('hidden.topicHiddenNote') }}
    </p>
  </div>
</template>

<style scoped>
.topic-hide {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2) var(--space-3);
}

.topic-hide__icon {
  width: var(--size-icon-md);
  height: var(--size-icon-md);
}

.topic-hide__note {
  margin: 0;
  color: var(--color-muted);
}
</style>
