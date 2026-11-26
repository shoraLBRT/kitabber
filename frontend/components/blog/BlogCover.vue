<script setup lang="ts">
// The cover of a blog's header (E04-04, FR-PROFILE, docs/ui/blog.md, ADR-0079). The header keeps one proportion on
// every screen (coverRatio) and shows the whole width of the picture and a band of its height at the saved position,
// so a desktop and a phone show the same band. Without a cover the header is the plain subtle background. The owner
// uploads a cover, moves it — by dragging with a mouse or a finger (pointer events), or with the arrow keys on the
// slider — removes it, or replaces it. A new picture is uploaded only on «Сохранить», as the avatar is (ADR-0034).
import { computed, onBeforeUnmount, ref } from 'vue'
import UiButton from '../ui/UiButton.vue'
import { useToast } from '../../composables/useToast'
import type { MediaUpload } from '../../api/media'
import type { CoverInput, Profile, ProfileCover, ProfileSave } from '../../api/profiles'
import {
  clampCoverPosition,
  coverOverflow,
  coverPositionDefault,
  coverRatio,
  dragCoverPosition,
  keyCoverPosition,
} from '../../utils/cover-position'
import { t } from '../../i18n'

/** What the owner's page can do with the cover; without it the cover is only shown. */
export interface CoverActions {
  upload: (file: File) => Promise<MediaUpload>
  save: (input: CoverInput) => Promise<ProfileSave>
  remove: () => Promise<ProfileSave>
}

const props = defineProps<{
  cover: ProfileCover | null
  actions?: CoverActions
}>()
const emit = defineEmits<{ saved: [profile: Profile]; unauthorized: [] }>()

/** What the media service accepts as an image (ADR-0030) and its size limit (Media:MaxBytes:Image). */
const imageTypes = ['image/jpeg', 'image/png', 'image/webp', 'image/gif']
const imageMaxBytes = 20 * 1024 * 1024
/** The header is at most the feed column wide: the browser picks the source for that width. */
const sizes = '(max-width: 640px) 100vw, 640px'

const toast = useToast()
const editing = ref(false)
const busy = ref(false)
const error = ref<string>()
/** The position while the owner moves the picture, and the picture chosen but not uploaded yet. */
const position = ref(coverPositionDefault)
const pendingFile = ref<File | null>(null)
const pendingUrl = ref<string>()
const box = ref<HTMLElement>()
const img = ref<HTMLImageElement>()
const fileInput = ref<HTMLInputElement>()
let drag: { pointer: number; startY: number; start: number; overflow: number } | null = null

const sorted = computed(() => [...(props.cover?.sources ?? [])].sort((a, b) => a.width - b.width))
const src = computed(() => pendingUrl.value ?? sorted.value.at(-1)?.url)
const srcset = computed(() =>
  pendingUrl.value || sorted.value.length === 0
    ? undefined
    : sorted.value.map((source) => `${source.url} ${source.width}w`).join(', '),
)
const shown = computed(() =>
  editing.value ? position.value : (props.cover?.position ?? coverPositionDefault),
)
// Only a colour the server makes (#rrggbb) goes into the style.
const color = computed(() =>
  props.cover?.preview && /^#[0-9a-f]{6}$/i.test(props.cover.preview.color)
    ? props.cover.preview.color
    : undefined,
)

function setPending(file: File | null) {
  if (pendingUrl.value) URL.revokeObjectURL(pendingUrl.value)
  pendingFile.value = file
  pendingUrl.value = file ? URL.createObjectURL(file) : undefined
}
onBeforeUnmount(() => setPending(null))

function chooseFile() {
  fileInput.value?.click()
}

function onFile(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  error.value = undefined
  if (!imageTypes.includes(file.type)) {
    error.value = t('blog.coverType')
    return
  }
  if (file.size > imageMaxBytes) {
    error.value = t('blog.coverTooLarge')
    return
  }
  setPending(file)
  position.value = coverPositionDefault
  editing.value = true
}

function startMoving() {
  error.value = undefined
  position.value = props.cover?.position ?? coverPositionDefault
  editing.value = true
}

function cancel() {
  setPending(null)
  drag = null
  editing.value = false
  error.value = undefined
}

function overflow(): number {
  const frame = box.value
  const picture = img.value
  if (!frame || !picture) return 0
  return coverOverflow(frame.clientWidth, frame.clientHeight, picture.naturalWidth, picture.naturalHeight)
}

function onPointerDown(event: PointerEvent) {
  if (!editing.value || busy.value) return
  try {
    // The picture keeps following a finger or a mouse that leaves the header while moving it.
    ;(event.currentTarget as HTMLElement).setPointerCapture(event.pointerId)
  } catch {
    // Without capture the drag still works while the pointer stays over the header.
  }
  drag = { pointer: event.pointerId, startY: event.clientY, start: position.value, overflow: overflow() }
}

function onPointerMove(event: PointerEvent) {
  if (!drag || event.pointerId !== drag.pointer) return
  position.value = dragCoverPosition(drag.start, event.clientY - drag.startY, drag.overflow)
}

function onPointerUp(event: PointerEvent) {
  if (drag && event.pointerId === drag.pointer) drag = null
}

function onKey(event: KeyboardEvent) {
  const next = keyCoverPosition(position.value, event.key, event.shiftKey)
  if (next === null) return
  event.preventDefault()
  position.value = next
}

const uploadErrors: Record<Exclude<MediaUpload['status'], 'uploaded'>, string> = {
  unsupported: t('blog.coverUnsupported'),
  'too-large': t('blog.coverTooLarge'),
  unauthorized: '',
  error: t('blog.coverFailed'),
}

function finish(result: ProfileSave, message: string): boolean {
  if (result.status === 'saved') {
    toast.show(message, { tone: 'success' })
    emit('saved', result.profile)
    return true
  }
  if (result.status === 'unauthorized') emit('unauthorized')
  else error.value = t('blog.coverFailed')
  return false
}

async function save() {
  const actions = props.actions
  if (!actions || busy.value) return
  busy.value = true
  error.value = undefined
  try {
    let mediaId = props.cover?.id
    if (pendingFile.value) {
      const uploaded = await actions.upload(pendingFile.value)
      if (uploaded.status === 'unauthorized') return emit('unauthorized')
      if (uploaded.status !== 'uploaded') {
        error.value = uploadErrors[uploaded.status]
        return
      }
      mediaId = uploaded.media.id
    }
    if (!mediaId) return
    const result = await actions.save({ mediaId, position: clampCoverPosition(position.value) })
    if (finish(result, t('blog.coverSaved'))) cancel()
  } finally {
    busy.value = false
  }
}

async function remove() {
  const actions = props.actions
  if (!actions || busy.value) return
  busy.value = true
  error.value = undefined
  try {
    finish(await actions.remove(), t('blog.coverRemoved'))
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="cover" data-testid="blog-cover" :data-position="src ? shown : undefined">
    <div
      ref="box"
      class="cover__box"
      :class="{ 'cover__box--editing': editing }"
      :style="{ aspectRatio: String(coverRatio), backgroundColor: color }"
    >
      <img
        v-if="src"
        ref="img"
        class="cover__img"
        :src="src"
        :srcset="srcset"
        :sizes="srcset ? sizes : undefined"
        :style="{ objectPosition: `50% ${shown}%` }"
        alt=""
        draggable="false"
        fetchpriority="high"
        data-testid="blog-cover-image"
      />
      <div
        v-if="editing"
        class="cover__handle"
        role="slider"
        tabindex="0"
        :aria-label="t('blog.coverPosition')"
        aria-orientation="vertical"
        :aria-valuemin="0"
        :aria-valuemax="100"
        :aria-valuenow="position"
        :aria-valuetext="`${position}%`"
        aria-describedby="blog-cover-hint"
        data-testid="blog-cover-handle"
        @pointerdown="onPointerDown"
        @pointermove="onPointerMove"
        @pointerup="onPointerUp"
        @pointercancel="onPointerUp"
        @lostpointercapture="onPointerUp"
        @keydown="onKey"
      >
        <span id="blog-cover-hint" class="cover__hint">{{ t('blog.coverHint') }}</span>
      </div>
    </div>

    <div v-if="actions" class="cover__actions" role="group" :aria-label="t('blog.cover')">
      <template v-if="editing">
        <UiButton
          variant="secondary"
          size="sm"
          :disabled="busy"
          data-testid="blog-cover-cancel"
          @click="cancel"
        >
          {{ t('blog.coverCancel') }}
        </UiButton>
        <UiButton size="sm" :loading="busy" data-testid="blog-cover-save" @click="save">
          {{ t('blog.coverSave') }}
        </UiButton>
      </template>
      <template v-else-if="cover">
        <UiButton
          variant="secondary"
          size="sm"
          :disabled="busy"
          data-testid="blog-cover-move"
          @click="startMoving"
        >
          {{ t('blog.coverMove') }}
        </UiButton>
        <UiButton
          variant="secondary"
          size="sm"
          :disabled="busy"
          data-testid="blog-cover-replace"
          @click="chooseFile"
        >
          {{ t('blog.coverChange') }}
        </UiButton>
        <UiButton
          variant="secondary"
          size="sm"
          :loading="busy"
          data-testid="blog-cover-remove"
          @click="remove"
        >
          {{ t('blog.coverRemove') }}
        </UiButton>
      </template>
      <UiButton v-else variant="secondary" size="sm" data-testid="blog-cover-upload" @click="chooseFile">
        {{ t('blog.coverUpload') }}
      </UiButton>
      <input
        ref="fileInput"
        class="visually-hidden"
        type="file"
        :accept="imageTypes.join(',')"
        tabindex="-1"
        aria-hidden="true"
        data-testid="blog-cover-file"
        @change="onFile"
      />
    </div>
    <p v-if="error" class="cover__error" role="alert" data-testid="blog-cover-error">{{ error }}</p>
  </div>
</template>

<style scoped>
.cover {
  position: relative;
}

.cover__box {
  position: relative;
  overflow: hidden;
  width: 100%;
  background: var(--color-surface-subtle);
}

.cover__img {
  display: block;
  width: 100%;
  height: 100%;
  object-fit: cover;
  user-select: none;
}

.cover__handle {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: flex-end;
  justify-content: center;
  padding: var(--space-2);
  cursor: grab;
  /* The page does not scroll under a finger that moves the picture, and a mouse drag selects no text. */
  touch-action: none;
  user-select: none;
  outline: var(--size-focus-ring) dashed var(--color-surface);
  outline-offset: calc(-1 * var(--space-2));
}

.cover__handle:active {
  cursor: grabbing;
}

.cover__handle:focus-visible {
  outline-style: solid;
}

.cover__hint {
  padding: var(--space-1) var(--space-2);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: var(--color-text);
  font-size: var(--font-size-xs);
}

.cover__actions {
  position: absolute;
  top: var(--space-2);
  right: var(--space-2);
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: var(--space-2);
}

.cover__error {
  margin: 0;
  padding: var(--space-2) var(--space-5) 0;
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}
</style>
