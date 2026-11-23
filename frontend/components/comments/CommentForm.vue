<script setup lang="ts">
// The form of a comment (E09-03, FR-COMMENT, docs/ui/comments.md, ADR-0056). Under the post it is the folded field
// «Комментарий…» that opens on a press; under a comment it is the reply form, open at once. A guest's press opens the
// sign-in window instead. The text waits in a local draft while it is written; on «Отправить» (or Ctrl+Enter) the
// comment goes into the tree at once and the form empties. A failed send gives the text back with the reason.
import { computed, inject, nextTick, onMounted, ref, watch } from 'vue'
import UiButton from '../ui/UiButton.vue'
import { commentTreeKey, formTarget } from './context'
import { commentDraftKey, draftStorage, readCommentDraft, saveCommentDraft } from '../../utils/comment-draft'

const props = withDefaults(defineProps<{ parentId?: string | null; inviting?: boolean }>(), {
  parentId: null,
  inviting: false,
})

const emit = defineEmits<{ close: [] }>()

/** The longest text the server takes (CommentRules.MaxTextLength). */
const maxLength = 5000

const tree = inject(commentTreeKey)!
const reply = computed(() => props.parentId !== null)
const target = computed(() => formTarget(props.parentId))
const draftKey = computed(() => commentDraftKey(tree.postId.value, props.parentId))
const failure = computed(() => tree.failures.value[target.value])
const fieldId = computed(() => `comment-form-${target.value}`)

const text = ref(failure.value?.text || readCommentDraft(draftStorage(), draftKey.value))
const open = ref(
  reply.value ||
    tree.resume.value === target.value ||
    text.value !== '' ||
    (props.inviting && tree.signedIn.value),
)
const field = ref<HTMLTextAreaElement>()
const blank = computed(() => text.value.trim() === '')

// A send that failed after the form emptied: its text comes back unless a new one is being written.
watch(failure, (next) => {
  if (!next) return
  if (blank.value) {
    text.value = next.text
    saveCommentDraft(draftStorage(), draftKey.value, next.text)
  }
  open.value = true
})

async function unfold() {
  if (!tree.signedIn.value) {
    tree.signIn(target.value)
    return
  }
  open.value = true
  await nextTick()
  field.value?.focus()
}

function onInput() {
  saveCommentDraft(draftStorage(), draftKey.value, text.value)
  if (failure.value) tree.dismiss(target.value)
}

function submit() {
  if (blank.value) return
  if (!tree.signedIn.value) {
    tree.signIn(target.value)
    return
  }
  // The draft stays until the server keeps the comment; the field empties without wiping it out.
  tree.send(text.value, props.parentId)
  text.value = ''
  if (reply.value) emit('close')
  else open.value = false
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
    event.preventDefault()
    submit()
  } else if (event.key === 'Escape' && reply.value) {
    emit('close')
  }
}

onMounted(() => {
  if (reply.value || tree.resume.value === target.value) field.value?.focus()
})
</script>

<template>
  <div class="comment-form" :class="{ 'comment-form--reply': reply }" data-testid="comment-form">
    <button
      v-if="!open"
      type="button"
      class="comment-form__folded"
      data-testid="comment-form-open"
      @click="unfold"
    >
      Комментарий…
    </button>
    <form v-else class="comment-form__open" @submit.prevent="submit">
      <label :for="fieldId" class="visually-hidden">{{ reply ? 'Текст ответа' : 'Текст комментария' }}</label>
      <textarea
        :id="fieldId"
        ref="field"
        v-model="text"
        class="comment-form__field"
        :placeholder="reply ? 'Ответ…' : 'Комментарий…'"
        :maxlength="maxLength"
        rows="3"
        :aria-invalid="failure ? 'true' : undefined"
        :aria-describedby="failure ? `${fieldId}-error` : undefined"
        data-testid="comment-form-text"
        @input="onInput"
        @keydown="onKeydown"
      />
      <p
        v-if="failure"
        :id="`${fieldId}-error`"
        class="comment-form__error"
        role="alert"
        data-testid="comment-form-error"
      >
        {{ failure.message }}
      </p>
      <div class="comment-form__actions">
        <UiButton
          v-if="reply"
          variant="ghost"
          size="sm"
          data-testid="comment-form-cancel"
          @click="emit('close')"
        >
          Отмена
        </UiButton>
        <UiButton type="submit" size="sm" :disabled="blank" data-testid="comment-form-send"
          >Отправить</UiButton
        >
      </div>
    </form>
  </div>
</template>

<style scoped>
.comment-form {
  min-width: 0;
}

.comment-form--reply {
  margin-top: var(--space-2);
}

.comment-form__folded {
  width: 100%;
  min-height: var(--size-touch);
  padding: var(--space-2) var(--space-3);
  border: var(--size-border) solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: var(--color-muted);
  font: inherit;
  text-align: left;
  cursor: text;
}

.comment-form__folded:hover {
  border-color: var(--color-border-strong);
}

.comment-form__open {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.comment-form__field {
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

.comment-form__field:focus-visible {
  outline: var(--size-focus-ring) solid var(--color-focus);
  outline-offset: 0;
}

.comment-form__field[aria-invalid='true'] {
  border-color: var(--color-danger);
}

.comment-form__error {
  margin: 0;
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}

.comment-form__actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--space-2);
}
</style>
