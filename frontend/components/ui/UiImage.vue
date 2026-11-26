<script setup lang="ts">
// Image (E06-04, ADR-0040): the one way the site shows a picture. The element takes the picture's proportions from
// width and height before a byte arrives, so the layout does not jump; the browser picks from the sources (a srcset
// of signed imgproxy links, ADR-0037) the one that fits the box at the screen's density, and fetches a picture off
// the screen only when it comes near. Pass eager for the picture a page opens with. States (data-state): empty (no
// link), loading, loaded, error; empty and error keep the box and show a placeholder in place of the picture.
import { ImageIcon, ImageOff } from 'lucide-vue-next'
import { computed, getCurrentInstance, onMounted, ref, watch } from 'vue'
import type { ImageSource } from '../../types/ui'
import { t } from '../../i18n'

const props = withDefaults(
  defineProps<{
    /** The picture's link; without it the widest of the sources. */
    src?: string | null
    /** The same picture in several widths, for the srcset. */
    sources?: readonly ImageSource[]
    /** How wide the picture is shown, the sizes attribute: the browser picks a source from it. */
    sizes?: string
    /** The picture's proportions, in pixels: the box is reserved from them. */
    width: number
    height: number
    alt: string
    /** Load at once and ahead of other pictures: the first screen of a page. */
    eager?: boolean
    fit?: 'cover' | 'contain'
  }>(),
  { src: undefined, sources: () => [], sizes: '100vw', fit: 'cover' },
)

const emit = defineEmits<{ load: []; error: [] }>()

const status = ref<'loading' | 'loaded' | 'error'>('loading')
const img = ref<HTMLImageElement>()

const sorted = computed(() => [...props.sources].sort((a, b) => a.width - b.width))
const link = computed(() => props.src || sorted.value.at(-1)?.url)
const srcset = computed(() =>
  sorted.value.length > 0
    ? sorted.value.map((source) => `${source.url} ${source.width}w`).join(', ')
    : undefined,
)
const state = computed(() => (link.value ? status.value : 'empty'))
const ratio = computed(() => `${props.width} / ${props.height}`)

function loaded() {
  status.value = 'loaded'
  emit('load')
}

function failed() {
  status.value = 'error'
  emit('error')
}

// A picture rendered on the server may load or fail before hydration, when @load and @error are not listened to yet.
// Hydration hands the component its element from the server's HTML before setup; a picture created on the client
// reports to the listeners. A lazy picture the browser has not fetched yet has no current source: still loading.
const hydrating = !!getCurrentInstance()?.vnode.el
onMounted(() => {
  const element = img.value
  if (!hydrating || !element?.complete || element.currentSrc === '') return
  // A tiny picture taken for a dense screen can have a natural width under one pixel, reported as 0: only decoding
  // tells it from a broken one.
  if (element.naturalWidth > 0) loaded()
  else element.decode().then(loaded, failed)
})

watch([link, srcset], () => {
  status.value = 'loading'
})
</script>

<template>
  <img
    v-if="state === 'loading' || state === 'loaded'"
    ref="img"
    class="ui-image"
    :class="`ui-image--${fit}`"
    :data-state="state"
    :style="{ aspectRatio: ratio }"
    :src="link"
    :srcset="srcset"
    :sizes="srcset ? sizes : undefined"
    :width="width"
    :height="height"
    :alt="alt"
    :loading="eager ? 'eager' : 'lazy'"
    :fetchpriority="eager ? 'high' : undefined"
    decoding="async"
    @load="loaded"
    @error="failed"
  />
  <span
    v-else
    class="ui-image ui-image--placeholder"
    :data-state="state"
    :style="{ aspectRatio: ratio }"
    :role="alt ? 'img' : undefined"
    :aria-label="alt || undefined"
  >
    <ImageOff v-if="state === 'error'" class="ui-image__icon" aria-hidden="true" />
    <ImageIcon v-else class="ui-image__icon" aria-hidden="true" />
    <span v-if="state === 'error'" class="ui-image__caption" aria-hidden="true">{{
      t('common.loadFailed')
    }}</span>
  </span>
</template>

<style scoped>
/* Defaults at the lowest specificity, so a class from the parent (a size, a shape) overrides them. */
:where(.ui-image) {
  display: block;
  width: 100%;
  height: auto;
}

/* The box keeps the picture's proportions in every state; the subtle surface shows until the picture paints. */
.ui-image {
  max-width: 100%;

  background: var(--color-surface-subtle);
}

.ui-image--cover {
  object-fit: cover;
}

.ui-image--contain {
  object-fit: contain;
}

.ui-image--placeholder {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  align-items: center;
  justify-content: center;
  overflow: hidden;
  color: var(--color-muted);
  font-size: var(--font-size-xs);
  text-align: center;
}

.ui-image__icon {
  width: var(--space-6);
  max-width: 50%;
  height: var(--space-6);
  max-height: 50%;
}

.ui-image__caption {
  max-width: 100%;
  padding-inline: var(--space-2);
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}
</style>
