<script setup lang="ts">
// «Поделиться» of the post's action panel (E07-07, FR-POST, docs/ui/post.md, ADR-0103). On a phone whose browser has
// the Web Share API the button opens the system share menu with the post's canonical link and title; closing that
// menu does nothing, and a browser that refuses it gets the link copied instead. Elsewhere (a wide screen, or a phone
// without the API) the button opens a list over the panel: «Скопировать ссылку» and the social networks, each a plain
// share address opened in a new tab — no widget and no code from another site. The server draws the list's button;
// the system menu takes over once the page is in the browser.
import { onMounted, ref } from 'vue'
import { Share2 } from 'lucide-vue-next'
import UiMenu from '../ui/UiMenu.vue'
import { useToast } from '../../composables/useToast'
import { copyPostLink } from '../../utils/post-menu'
import {
  canonicalUrl,
  networkShareUrl,
  shareItems,
  usesSystemShare,
  type ShareItemId,
} from '../../utils/share'
import { t } from '../../i18n'

const props = withDefaults(defineProps<{ path: string; title?: string | null }>(), { title: null })

const toast = useToast()
const label = t('actions.share')
const items = shareItems()
const system = ref(false)

onMounted(() => {
  system.value = usesSystemShare()
})

const link = () => canonicalUrl(props.path, window.location.origin)

async function shareBySystem() {
  try {
    await navigator.share(props.title ? { title: props.title, url: link() } : { url: link() })
  } catch (error) {
    // The person closed the menu: nothing to say. Any other refusal falls back to copying the link.
    if (error instanceof DOMException && error.name === 'AbortError') return
    await copyPostLink(props.path, toast)
  }
}

function choose(id: string) {
  const item = id as ShareItemId
  if (item === 'copy-link') void copyPostLink(props.path, toast)
  else window.open(networkShareUrl(item, link(), props.title ?? ''), '_blank', 'noopener,noreferrer')
}
</script>

<template>
  <div class="post-share" data-testid="post-share">
    <button v-if="system" type="button" class="post-share__button" @click="shareBySystem">
      <Share2 class="post-share__icon" aria-hidden="true" />
      <span class="post-share__label">{{ label }}</span>
    </button>
    <UiMenu v-else :items="items" :label="label" align="end" side="above" @select="choose">
      <template #trigger>
        <Share2 class="post-share__icon" aria-hidden="true" />
        <span class="post-share__label">{{ label }}</span>
      </template>
    </UiMenu>
  </div>
</template>

<style scoped>
.post-share {
  display: flex;
}

/* The same chip as the panel's other buttons (PostActions). */
.post-share__button,
.post-share :deep(.ui-menu__trigger) {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  min-height: var(--size-touch);
  padding: 0 var(--space-4);
  border: 0;
  border-radius: var(--radius-full);
  background: transparent;
  color: var(--color-text);
  font: inherit;
  font-size: var(--font-size-sm);
  cursor: pointer;
}

.post-share__button:hover,
.post-share :deep(.ui-menu__trigger:hover),
.post-share :deep(.ui-menu__trigger[aria-expanded='true']) {
  background: var(--color-surface-subtle);
}

.post-share__icon {
  width: var(--size-icon-md);
  height: var(--size-icon-md);
}

@media (max-width: 639px) {
  .post-share__button,
  .post-share :deep(.ui-menu__trigger) {
    padding: 0 var(--space-2);
  }

  /* Icon only on a phone, as the panel's other buttons; the label stays for screen readers. */
  .post-share__label {
    position: absolute;
    width: var(--size-border);
    height: var(--size-border);
    overflow: hidden;
    clip-path: inset(50%);
    white-space: nowrap;
  }
}
</style>
