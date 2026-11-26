<script setup lang="ts">
// A new post in the editor window (E05-09, FR-EDITOR, docs/ui/editor.md, ADR-0046): the optional title, the topic
// or «Без темы» (the author's own blog), the blocks, and «Опубликовать». The button waits for a block with text; the
// title's length is checked here too, and the server checks everything again (an empty post, the title, the size of
// the post and of a block). Its field errors are shown under the title or above the blocks with the focus on the
// first one; any other refusal is a toast and the window stays open. Ctrl+Enter publishes. A published post is the
// page's to open (`published` with the post's address). `initial` starts the post from what the quick post above the
// feed handed over with «Развернуть» (E05-29, ADR-0055).
import { ChevronDown, Redo2, Undo2 } from 'lucide-vue-next'
import { computed, nextTick, onBeforeUnmount, onMounted, ref, useId } from 'vue'
import BlockEditor from './BlockEditor.vue'
import EditorKeyboardBar from './EditorKeyboardBar.vue'
import EditorWindow from './EditorWindow.vue'
import UiButton from '../ui/UiButton.vue'
import UiMenu from '../ui/UiMenu.vue'
import { emptyDocument, isEmptyDocument, type PostDocument } from '../../editor/document'
import type { CreateEngine } from '../../editor/engine'
import { postTitleMaxLength, titleLength } from '../../editor/publish'
import type { PublishInput, PublishOutcome } from '../../api/publish'
import type { Topic } from '../../api/topics'
import type { ImageUpload } from '../../editor/image'
import type { QuickPostHandoff } from '../../editor/quick-post'
import { useToast } from '../../composables/useToast'
import { t } from '../../i18n'

const props = defineProps<{
  topics: Topic[]
  publish: (input: PublishInput) => Promise<PublishOutcome>
  createEngine?: CreateEngine
  /** Uploads a picture of the «Фото» block (E05-06, ADR-0049). */
  uploadImage?: (file: File) => Promise<ImageUpload>
  /** What the quick post above the feed hands over with «Развернуть» (E05-29, ADR-0055): the start of the post. */
  initial?: QuickPostHandoff | null
  /** The topic chosen at the start, such as «Написать в тему» on a topic page (E11-01); the quick post's wins. */
  topicId?: string | null
}>()

const emit = defineEmits<{ close: []; published: [path: string]; unauthorized: [] }>()

const noTopic = 'none'
// A copy through JSON: the hand-off comes from the page's reactive state, which structuredClone cannot copy.
const document = ref<PostDocument>(
  props.initial ? JSON.parse(JSON.stringify(props.initial.document)) : emptyDocument(),
)
const title = ref('')
const topicId = ref<string | null>(props.initial?.topicId ?? props.topicId ?? null)
const publishing = ref(false)
const errors = ref<Record<string, string>>({})
const editor = ref<InstanceType<typeof BlockEditor>>()
const titleField = ref<HTMLTextAreaElement>()
const bodyError = ref<HTMLElement>()
const titleErrorId = useId()
let draftId: string | null = null
const toast = useToast()

const topicName = computed(
  () => props.topics.find((topic) => topic.id === topicId.value)?.name ?? t('editor.noTopic'),
)
const topicItems = computed(() => [
  { id: noTopic, label: t('editor.noTopic') },
  ...props.topics.map((topic) => ({ id: topic.id, label: topic.name })),
])
const empty = computed(() => isEmptyDocument(document.value))
const blockErrors = computed(() =>
  Object.entries(errors.value)
    .filter(([field]) => field === 'body' || field.startsWith('body.'))
    .map(([, message]) => message),
)

function chooseTopic(id: string) {
  topicId.value = id === noTopic ? null : id
}

// The title is one line: Enter goes on to the text.
function onTitleKeydown(event: KeyboardEvent) {
  if (event.key !== 'Enter' || event.ctrlKey || event.metaKey) return
  event.preventDefault()
  titleField.value
    ?.closest('[role="dialog"]')
    ?.querySelector<HTMLElement>('[contenteditable="true"]')
    ?.focus()
}

function onTitleInput() {
  if (errors.value.title) errors.value = { ...errors.value, title: '' }
}

async function showErrors(next: Record<string, string>) {
  errors.value = next
  await nextTick()
  if (next.title) titleField.value?.focus()
  else bodyError.value?.focus()
}

async function submit() {
  if (publishing.value) return
  await editor.value?.flush()
  if (isEmptyDocument(document.value)) return
  if (titleLength(title.value) > postTitleMaxLength)
    return showErrors({ title: t('editor.titleTooLong', { max: postTitleMaxLength }) })

  publishing.value = true
  try {
    const outcome = await props.publish({
      draftId,
      title: title.value.trim() || null,
      blogId: topicId.value,
      body: document.value,
    })
    if (outcome.status === 'published') {
      errors.value = {}
      emit('published', outcome.path)
      return
    }
    if (outcome.status === 'unauthorized') return emit('unauthorized')
    draftId = outcome.draftId
    if (outcome.status === 'invalid' && Object.keys(outcome.errors).some((field) => field !== 'blogId'))
      return showErrors(outcome.errors)
    errors.value = {}
    toast.show(
      outcome.status === 'email-not-confirmed'
        ? t('editor.emailNotConfirmed')
        : outcome.status === 'invalid'
          ? t('editor.topicUnavailable')
          : t('editor.publishFailed'),
      { tone: 'error' },
    )
  } finally {
    publishing.value = false
  }
}

function onKeydown(event: KeyboardEvent) {
  if (event.key !== 'Enter' || !(event.ctrlKey || event.metaKey)) return
  event.preventDefault()
  void submit()
}

onMounted(() => window.document.addEventListener('keydown', onKeydown, true))
onBeforeUnmount(() => window.document.removeEventListener('keydown', onKeydown, true))
</script>

<template>
  <EditorWindow :title="t('post.new')" @close="emit('close')">
    <template #actions>
      <UiMenu
        :items="topicItems"
        :label="t('editor.topic', { name: topicName })"
        class="post-editor__topic"
        data-testid="editor-topic"
        @select="chooseTopic"
      >
        <template #trigger>
          <span class="post-editor__topic-name">{{ topicName }}</span>
          <ChevronDown class="post-editor__chevron" aria-hidden="true" />
        </template>
      </UiMenu>
      <button
        type="button"
        class="editor-window__button"
        :aria-label="t('editor.undo')"
        :title="t('editor.undoHint')"
        data-testid="editor-undo"
        :disabled="!editor?.canUndo"
        @click="editor?.undo()"
      >
        <Undo2 aria-hidden="true" />
      </button>
      <button
        type="button"
        class="editor-window__button"
        :aria-label="t('editor.redo')"
        :title="t('editor.redoHint')"
        data-testid="editor-redo"
        :disabled="!editor?.canRedo"
        @click="editor?.redo()"
      >
        <Redo2 aria-hidden="true" />
      </button>
    </template>

    <textarea
      ref="titleField"
      v-model="title"
      dir="auto"
      class="post-editor__title"
      rows="1"
      :placeholder="t('editor.title')"
      :aria-label="t('editor.title')"
      data-testid="editor-title"
      :aria-invalid="errors.title ? 'true' : undefined"
      :aria-describedby="errors.title ? titleErrorId : undefined"
      @keydown="onTitleKeydown"
      @input="onTitleInput"
    />
    <p v-if="errors.title" :id="titleErrorId" class="post-editor__error" data-testid="editor-title-error">
      {{ errors.title }}
    </p>
    <p
      v-if="blockErrors.length > 0"
      ref="bodyError"
      class="post-editor__error"
      tabindex="-1"
      role="alert"
      data-testid="editor-body-error"
    >
      {{ blockErrors.join(' ') }}
    </p>
    <BlockEditor
      ref="editor"
      v-model:document="document"
      :create-engine="createEngine"
      :upload-image="uploadImage"
    />

    <template #toolbar>
      <EditorKeyboardBar :editor="editor" />
    </template>
    <template #footer>
      <UiButton
        :disabled="empty"
        :loading="publishing"
        :title="t('editor.publishHint')"
        data-testid="editor-publish"
        @click="submit"
      >
        {{ t('editor.publish') }}
      </UiButton>
    </template>
  </EditorWindow>
</template>

<style scoped>
.post-editor__topic-name {
  max-width: var(--size-menu);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.post-editor__chevron {
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
}

.post-editor__topic {
  margin-right: auto;
}

.post-editor__title {
  display: block;
  width: 100%;
  margin: 0 0 var(--space-3);
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--color-text);
  font: inherit;
  font-size: var(--font-size-2xl);
  font-weight: var(--font-weight-bold);
  line-height: var(--line-height-tight);
  resize: none;
  field-sizing: content;
}

.post-editor__title::placeholder {
  color: var(--color-muted);
}

.post-editor__title:focus-visible {
  outline: none;
}

.post-editor__error {
  margin: 0 0 var(--space-3);
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}

.post-editor__error:focus-visible {
  outline: none;
}
</style>
