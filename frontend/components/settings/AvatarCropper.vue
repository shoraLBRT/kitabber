<script setup lang="ts">
// Cropping the chosen picture to a square avatar (E04-03, docs/ui/settings.md, ADR-0034): the picture lies under a
// round frame; it is moved by dragging (mouse or finger) or by the arrow keys on the frame, and zoomed by the slider
// or by + and −. «Применить» gives the square under the frame as a JPEG (utils/avatar-crop.ts); «Отмена» drops it.
import { computed, onBeforeUnmount, ref, shallowRef, watch } from 'vue'
import UiButton from '../ui/UiButton.vue'
import {
  clampCrop,
  cropAvatar,
  cropScale,
  maxZoom,
  minZoom,
  type CropState,
  type Size,
} from '../../utils/avatar-crop'
import { t } from '../../i18n'

const props = defineProps<{ file: File }>()
const emit = defineEmits<{ done: [avatar: Blob]; cancel: []; failed: [] }>()

/** The side of the frame on screen, in CSS pixels; it fits a 390 px phone with the sheet's padding. */
const frame = 240
const step = 10

const source = ref('')
const image = ref<HTMLImageElement>()
const natural = shallowRef<Size | null>(null)
const state = ref<CropState>({ zoom: minZoom, x: 0, y: 0 })
const busy = ref(false)

watch(
  () => props.file,
  (file) => {
    if (source.value) URL.revokeObjectURL(source.value)
    natural.value = null
    state.value = { zoom: minZoom, x: 0, y: 0 }
    source.value = URL.createObjectURL(file)
  },
  { immediate: true },
)
onBeforeUnmount(() => URL.revokeObjectURL(source.value))

function onLoad() {
  const img = image.value
  if (!img || img.naturalWidth === 0) return emit('failed')
  natural.value = { width: img.naturalWidth, height: img.naturalHeight }
}

function place(next: CropState) {
  if (natural.value) state.value = clampCrop(next, natural.value, frame)
}

const pictureStyle = computed(() => {
  if (!natural.value) return { visibility: 'hidden' as const }
  const scale = cropScale(natural.value, frame, state.value.zoom)
  const width = natural.value.width * scale
  const height = natural.value.height * scale
  return {
    width: `${width}px`,
    height: `${height}px`,
    left: `${frame / 2 + state.value.x - width / 2}px`,
    top: `${frame / 2 + state.value.y - height / 2}px`,
  }
})

let drag: { pointer: number; x: number; y: number; from: CropState } | null = null

function onPointerDown(event: PointerEvent) {
  drag = { pointer: event.pointerId, x: event.clientX, y: event.clientY, from: { ...state.value } }
  ;(event.currentTarget as HTMLElement).setPointerCapture?.(event.pointerId)
}

function onPointerMove(event: PointerEvent) {
  if (!drag || drag.pointer !== event.pointerId) return
  place({ ...drag.from, x: drag.from.x + event.clientX - drag.x, y: drag.from.y + event.clientY - drag.y })
}

function onPointerUp() {
  drag = null
}

const moves: Record<string, [number, number]> = {
  ArrowLeft: [-step, 0],
  ArrowRight: [step, 0],
  ArrowUp: [0, -step],
  ArrowDown: [0, step],
}

function onKeydown(event: KeyboardEvent) {
  const move = moves[event.key]
  if (move) {
    event.preventDefault()
    place({ ...state.value, x: state.value.x + move[0], y: state.value.y + move[1] })
  } else if (event.key === '+' || event.key === '=') {
    event.preventDefault()
    place({ ...state.value, zoom: state.value.zoom + 0.25 })
  } else if (event.key === '-') {
    event.preventDefault()
    place({ ...state.value, zoom: state.value.zoom - 0.25 })
  }
}

function onZoom(event: Event) {
  place({ ...state.value, zoom: Number((event.target as HTMLInputElement).value) })
}

async function apply() {
  if (!image.value || !natural.value) return
  busy.value = true
  try {
    emit('done', await cropAvatar(image.value, state.value, frame))
  } catch {
    emit('failed')
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="avatar-cropper" data-testid="avatar-cropper">
    <div
      class="avatar-cropper__frame"
      :style="{ width: `${frame}px`, height: `${frame}px` }"
      tabindex="0"
      role="group"
      :aria-label="t('settings.cropLabel')"
      data-testid="avatar-frame"
      @pointerdown="onPointerDown"
      @pointermove="onPointerMove"
      @pointerup="onPointerUp"
      @pointercancel="onPointerUp"
      @keydown="onKeydown"
    >
      <img
        ref="image"
        class="avatar-cropper__picture"
        :src="source"
        alt=""
        draggable="false"
        :style="pictureStyle"
        @load="onLoad"
        @error="$emit('failed')"
      />
      <div class="avatar-cropper__mask" aria-hidden="true" />
    </div>
    <label class="avatar-cropper__zoom">
      <span>{{ t('settings.zoom') }}</span>
      <input
        type="range"
        :min="minZoom"
        :max="maxZoom"
        step="0.05"
        :value="state.zoom"
        :disabled="!natural"
        @input="onZoom"
      />
    </label>
    <div class="avatar-cropper__actions">
      <UiButton variant="secondary" @click="$emit('cancel')">{{ t('actions.cancel') }}</UiButton>
      <UiButton :loading="busy" :disabled="!natural" @click="apply">{{ t('settings.apply') }}</UiButton>
    </div>
  </div>
</template>

<style scoped>
.avatar-cropper {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-4);
}

.avatar-cropper__frame {
  position: relative;
  overflow: hidden;
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
  cursor: grab;
  touch-action: none;
  user-select: none;
}

.avatar-cropper__frame:active {
  cursor: grabbing;
}

.avatar-cropper__picture {
  position: absolute;
  max-width: none;
  pointer-events: none;
}

.avatar-cropper__mask {
  position: absolute;
  inset: 0;
  border-radius: var(--radius-full);
  box-shadow: 0 0 0 100vmax color-mix(in srgb, var(--color-text) 45%, transparent);
  pointer-events: none;
}

.avatar-cropper__zoom {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  width: 100%;
  font-size: var(--font-size-sm);
}

.avatar-cropper__zoom input {
  flex: 1;
  min-height: var(--size-touch);
}

.avatar-cropper__actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--space-2);
  width: 100%;
}
</style>
