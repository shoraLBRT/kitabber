<script setup lang="ts">
// The quick post (E05-29, FR-EDITOR «Быстрый пост», docs/ui/editor.md, ADR-0055): a post written as easily as a tweet.
// A text field without a title, photos attached under it, the topic or «Без темы» (the author's own blog), «Развернуть»
// and «Опубликовать». The text and the photos become the post document of the full editor (editor/quick-post.ts), so
// publishing runs the same checks (E05-09, ADR-0046): an empty post is not sent, the server checks it again, and its
// refusal is a message in the form, which keeps what was written. «Развернуть» hands the document and the topic to the
// full editor; while a photo is still uploading it waits. A guest sees an invitation to sign in instead of the form.
// Ctrl+Enter in the text publishes. A published post is the page's to open (`published` with the post's address).
import { ChevronDown, ImagePlus, X } from 'lucide-vue-next'
import { computed, ref, useId } from 'vue'
import UiButton from '../ui/UiButton.vue'
import UiMenu from '../ui/UiMenu.vue'
import { isEmptyDocument } from '../../editor/document'
import { imageMessages, measureInBrowser, rememberPreview, type ImageUpload } from '../../editor/image'
import { quickPhotoLimit, quickPostDocument, type QuickPostHandoff } from '../../editor/quick-post'
import type { PublishInput, PublishOutcome } from '../../api/publish'
import type { Topic } from '../../api/topics'
import { t } from '../../i18n'

const props = defineProps<{
  topics: Topic[]
  signedIn: boolean
  publish: (input: PublishInput) => Promise<PublishOutcome>
  uploadImage: (file: File) => Promise<ImageUpload>
  /** The picture's size in pixels; the browser reads it by default, tests pass their own. */
  measure?: (url: string) => Promise<{ width: number; height: number } | null>
  /** A link that shows a picked file; the browser's object URL by default. */
  preview?: (file: File) => string
}>()

const emit = defineEmits<{
  published: [path: string]
  expand: [handoff: QuickPostHandoff]
  unauthorized: []
  'sign-in': []
}>()

interface Photo {
  key: number
  file: File
  url: string
  state: 'uploading' | 'ready' | 'error'
  message?: string
  media?: string
  width?: number
  height?: number
}

const noTopic = 'none'
const text = ref('')
const photos = ref<Photo[]>([])
const topicId = ref<string | null>(null)
const publishing = ref(false)
const error = ref('')
const picker = ref<HTMLInputElement>()
const textId = useId()
let nextKey = 1
let draftId: string | null = null

const topicName = computed(
  () => props.topics.find((topic) => topic.id === topicId.value)?.name ?? t('editor.noTopic'),
)
const topicItems = computed(() => [
  { id: noTopic, label: t('editor.noTopic') },
  ...props.topics.map((topic) => ({ id: topic.id, label: topic.name })),
])
const uploading = computed(() => photos.value.some((photo) => photo.state === 'uploading'))
const readyPhotos = computed(() =>
  photos.value
    .filter((photo) => photo.state === 'ready')
    .map((photo) => ({ media: photo.media!, width: photo.width!, height: photo.height! })),
)
const document = () => quickPostDocument(text.value, readyPhotos.value)
const empty = computed(() => text.value.trim() === '' && readyPhotos.value.length === 0)

function chooseTopic(id: string) {
  topicId.value = id === noTopic ? null : id
}

function pick() {
  picker.value?.click()
}

function onPicked(event: Event) {
  const input = event.target as HTMLInputElement
  const room = quickPhotoLimit - photos.value.length
  for (const file of [...(input.files ?? [])].slice(0, Math.max(room, 0))) void attach(file)
  input.value = ''
}

async function attach(file: File) {
  const photo: Photo = {
    key: nextKey++,
    file,
    url: (props.preview ?? URL.createObjectURL)(file),
    state: 'uploading',
  }
  photos.value = [...photos.value, photo]
  await upload(photo.key)
}

function update(key: number, change: Partial<Photo>) {
  photos.value = photos.value.map((photo) => (photo.key === key ? { ...photo, ...change } : photo))
}

async function upload(key: number) {
  const photo = photos.value.find((item) => item.key === key)
  if (!photo) return
  update(key, { state: 'uploading', message: undefined })
  const size = await (props.measure ?? measureInBrowser)(photo.url)
  if (!size) return update(key, { state: 'error', message: imageMessages.unreadable })
  const outcome = await props.uploadImage(photo.file).catch((): ImageUpload => ({ status: 'error' }))
  if (!photos.value.some((item) => item.key === key)) return
  if (outcome.status !== 'uploaded')
    return update(key, { state: 'error', message: imageMessages[outcome.status] })
  // The full editor shows the picture from this link after «Развернуть», without asking the server.
  rememberPreview(outcome.media, photo.url)
  update(key, { state: 'ready', media: outcome.media, ...size })
}

function remove(key: number) {
  photos.value = photos.value.filter((photo) => photo.key !== key)
}

function reset() {
  text.value = ''
  photos.value = []
  error.value = ''
  draftId = null
}

async function submit() {
  if (publishing.value || uploading.value) return
  const body = document()
  if (isEmptyDocument(body)) return

  publishing.value = true
  error.value = ''
  try {
    const outcome = await props.publish({ draftId, title: null, blogId: topicId.value, body })
    if (outcome.status === 'published') {
      reset()
      emit('published', outcome.path)
      return
    }
    if (outcome.status === 'unauthorized') return emit('unauthorized')
    draftId = outcome.draftId
    error.value =
      outcome.status === 'email-not-confirmed'
        ? t('editor.emailNotConfirmed')
        : outcome.status === 'invalid'
          ? Object.keys(outcome.errors).every((field) => field === 'blogId')
            ? t('editor.topicUnavailable')
            : Object.values(outcome.errors).join(' ')
          : t('editor.publishFailed')
  } finally {
    publishing.value = false
  }
}

function expand() {
  if (uploading.value) return
  emit('expand', { document: document(), topicId: topicId.value })
  reset()
}

function onTextKeydown(event: KeyboardEvent) {
  if (event.key !== 'Enter' || !(event.ctrlKey || event.metaKey)) return
  event.preventDefault()
  void submit()
}
</script>

<template>
  <section class="quick-post" :aria-label="t('editor.quickPost')" data-testid="quick-post">
    <div v-if="!signedIn" class="quick-post__guest" data-testid="quick-post-guest">
      <p class="quick-post__muted">{{ t('editor.quickPostSignIn') }}</p>
      <UiButton data-testid="quick-post-sign-in" @click="emit('sign-in')">{{ t('common.signIn') }}</UiButton>
    </div>

    <form v-else class="quick-post__form" @submit.prevent="submit">
      <label :for="textId" class="visually-hidden">{{ t('editor.quickPostText') }}</label>
      <textarea
        :id="textId"
        v-model="text"
        dir="auto"
        class="quick-post__text"
        rows="2"
        :placeholder="t('editor.quickPostPlaceholder')"
        data-testid="quick-post-text"
        @keydown="onTextKeydown"
      />

      <ul v-if="photos.length > 0" class="quick-post__photos" data-testid="quick-post-photos">
        <li
          v-for="photo in photos"
          :key="photo.key"
          class="quick-post__photo"
          :data-state="photo.state"
          :aria-busy="photo.state === 'uploading' ? 'true' : 'false'"
          data-testid="quick-post-photo"
        >
          <img :src="photo.url" alt="" class="quick-post__picture" />
          <span v-if="photo.state === 'uploading'" class="quick-post__status" role="status">
            {{ imageMessages.uploading }}
          </span>
          <span v-else-if="photo.state === 'error'" class="quick-post__status" role="alert">
            {{ photo.message }}
            <button
              v-if="photo.message !== imageMessages.unreadable"
              type="button"
              class="quick-post__retry"
              data-testid="quick-post-photo-retry"
              @click="upload(photo.key)"
            >
              {{ imageMessages.retry }}
            </button>
          </span>
          <button
            type="button"
            class="quick-post__remove"
            :aria-label="t('editor.removePhoto')"
            data-testid="quick-post-photo-remove"
            @click="remove(photo.key)"
          >
            <X aria-hidden="true" />
          </button>
        </li>
      </ul>

      <p v-if="error" class="quick-post__error" role="alert" data-testid="quick-post-error">{{ error }}</p>

      <div class="quick-post__bar">
        <button
          type="button"
          class="quick-post__icon"
          :aria-label="t('editor.attachPhoto')"
          :title="t('editor.attachPhoto')"
          data-testid="quick-post-attach"
          :disabled="photos.length >= quickPhotoLimit"
          @click="pick"
        >
          <ImagePlus aria-hidden="true" />
        </button>
        <input
          ref="picker"
          type="file"
          accept="image/jpeg,image/png,image/gif,image/webp"
          multiple
          hidden
          data-testid="quick-post-file"
          @change="onPicked"
        />
        <UiMenu
          :items="topicItems"
          :label="t('editor.topic', { name: topicName })"
          class="quick-post__topic"
          data-testid="quick-post-topic"
          @select="chooseTopic"
        >
          <template #trigger>
            <span class="quick-post__topic-name">{{ topicName }}</span>
            <ChevronDown class="quick-post__chevron" aria-hidden="true" />
          </template>
        </UiMenu>
        <UiButton
          variant="ghost"
          :disabled="uploading"
          :title="t('editor.openFull')"
          data-testid="quick-post-expand"
          @click="expand"
        >
          {{ t('editor.expandShort') }}
        </UiButton>
        <UiButton
          type="submit"
          :disabled="empty || uploading"
          :loading="publishing"
          :title="t('editor.publishHint')"
          data-testid="quick-post-publish"
        >
          {{ t('editor.publish') }}
        </UiButton>
      </div>
    </form>
  </section>
</template>

<style scoped>
.quick-post {
  padding: var(--space-4) var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.quick-post__guest {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
}

.quick-post__muted {
  margin: 0;
  color: var(--color-muted);
}

.quick-post__form {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}

.quick-post__text {
  display: block;
  width: 100%;
  min-height: calc(var(--space-12) + var(--space-2));
  margin: 0;
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--color-text);
  font: inherit;
  font-size: var(--font-size-md);
  line-height: var(--line-height-normal);
  resize: none;
  field-sizing: content;
}

.quick-post__text::placeholder {
  color: var(--color-muted);
}

.quick-post__text:focus-visible {
  outline: none;
}

.quick-post__photos {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  margin: 0;
  padding: 0;
  list-style: none;
}

.quick-post__photo {
  position: relative;
  display: flex;
  flex-direction: column;
  width: calc(var(--space-12) * 2);
  overflow: hidden;
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}

.quick-post__picture {
  display: block;
  width: 100%;
  height: calc(var(--space-12) * 2);
  object-fit: cover;
}

.quick-post__photo[data-state='uploading'] .quick-post__picture,
.quick-post__photo[data-state='error'] .quick-post__picture {
  opacity: 0.5;
}

.quick-post__status {
  padding: var(--space-1) var(--space-2);
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.quick-post__photo[data-state='error'] .quick-post__status {
  color: var(--color-danger);
}

.quick-post__retry {
  display: block;
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--color-accent);
  font: inherit;
  cursor: pointer;
}

.quick-post__remove {
  position: absolute;
  top: var(--space-1);
  right: var(--space-1);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: var(--space-6);
  height: var(--space-6);
  border: 0;
  border-radius: var(--radius-full);
  background: var(--color-overlay);
  color: var(--color-on-accent);
  cursor: pointer;
}

.quick-post__remove svg {
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
}

.quick-post__error {
  margin: 0;
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}

.quick-post__bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
  padding-top: var(--space-3);
  border-top: var(--size-border) solid var(--color-border);
}

.quick-post__icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: var(--size-touch);
  height: var(--size-touch);
  border: 0;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--color-muted);
  cursor: pointer;
}

.quick-post__icon:hover:not(:disabled) {
  background: var(--color-surface-subtle);
  color: var(--color-text);
}

.quick-post__icon:disabled {
  opacity: 0.4;
  cursor: default;
}

.quick-post__icon svg {
  width: var(--size-icon-md);
  height: var(--size-icon-md);
}

.quick-post__topic {
  margin-right: auto;
}

.quick-post__topic-name {
  max-width: var(--size-menu);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.quick-post__chevron {
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
}

@media (max-width: 639px) {
  .quick-post {
    padding: 0;
    border-radius: 0;
    background: transparent;
  }
}
</style>
