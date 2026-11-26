<script setup lang="ts">
// The text of an own comment opened for a change (E09-05, ADR-0059), in place of the text: «Сохранить» (or Ctrl+Enter)
// sends it, «Отмена» (or Escape) leaves the text as it was. The server keeps the change only while the time to change it
// lasts; its refusal (the time is over, an empty text) stays under the field with the text written.
import { computed, inject, onMounted, ref } from 'vue'
import UiButton from '../ui/UiButton.vue'
import { commentTreeKey } from './context'
import { t } from '../../i18n'

const props = defineProps<{ commentId: string; text: string }>()

const emit = defineEmits<{ close: [] }>()

/** The longest text the server takes (CommentRules.MaxTextLength). */
const maxLength = 5000

const tree = inject(commentTreeKey)!
const text = ref(props.text)
const saving = ref(false)
const failure = ref<string | null>(null)
const field = ref<HTMLTextAreaElement>()
const fieldId = computed(() => `comment-edit-${props.commentId}`)
const blank = computed(() => text.value.trim() === '')

async function submit() {
  if (blank.value || saving.value) return
  if (text.value.trim() === props.text) {
    emit('close')
    return
  }
  saving.value = true
  try {
    failure.value = await tree.edit(props.commentId, text.value)
    if (failure.value === null) emit('close')
  } finally {
    saving.value = false
  }
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
    event.preventDefault()
    void submit()
  } else if (event.key === 'Escape') {
    emit('close')
  }
}

onMounted(() => field.value?.focus())
</script>

<template>
  <form class="comment-edit" data-testid="comment-edit" @submit.prevent="submit">
    <label :for="fieldId" class="visually-hidden">{{ t('comments.text') }}</label>
    <textarea
      :id="fieldId"
      ref="field"
      v-model="text"
      dir="auto"
      class="comment-edit__field"
      :maxlength="maxLength"
      rows="3"
      :aria-invalid="failure ? 'true' : undefined"
      :aria-describedby="failure ? `${fieldId}-error` : undefined"
      data-testid="comment-edit-text"
      @input="failure = null"
      @keydown="onKeydown"
    />
    <p
      v-if="failure"
      :id="`${fieldId}-error`"
      class="comment-edit__error"
      role="alert"
      data-testid="comment-edit-error"
    >
      {{ failure }}
    </p>
    <div class="comment-edit__actions">
      <UiButton variant="ghost" size="sm" data-testid="comment-edit-cancel" @click="emit('close')">
        {{ t('actions.cancel') }}
      </UiButton>
      <UiButton type="submit" size="sm" :disabled="blank" :loading="saving" data-testid="comment-edit-save">{{
        t('actions.save')
      }}</UiButton>
    </div>
  </form>
</template>

<style scoped>
.comment-edit {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  min-width: 0;
}

.comment-edit__field {
  box-sizing: border-box;
  width: 100%;
  min-height: calc(var(--size-touch) * 2);
  padding: var(--space-2) var(--space-3);
  border: var(--size-border) solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: var(--color-text);
  font: inherit;
  font-size: var(--font-size-md);
  resize: vertical;
}

.comment-edit__field:focus-visible {
  outline: var(--size-focus-ring) solid var(--color-focus);
  outline-offset: 0;
}

.comment-edit__field[aria-invalid='true'] {
  border-color: var(--color-danger);
}

.comment-edit__error {
  margin: 0;
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}

.comment-edit__actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--space-2);
}
</style>
