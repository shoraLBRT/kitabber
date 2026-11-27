<script setup lang="ts">
// Pull to refresh (E08-14, FR-FEED, docs/spec.md section 5 «Мобильная версия», docs/ui/feed.md, ADR-0111): on a touch
// screen, a finger pulling the feed down from the very top of the page draws the mark above it, and letting go past
// `pullThreshold` reads the feed again by the page's own `refresh` (the fresh feed puts new posts on top, as
// «Показать новые» does; the others read their first page again). A mouse and a page scrolled down never pull; the
// browser's own pull (which would reload the whole site) is turned off while the feed is on the screen.
import { RefreshCw } from 'lucide-vue-next'
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { pullDistance, pullThreshold } from '../../utils/pull'
import { t } from '../../i18n'

const props = defineProps<{ refresh: () => Promise<unknown> }>()

const distance = ref(0)
const refreshing = ref(false)
let start: number | null = null

const state = computed(() => {
  if (refreshing.value) return 'refreshing'
  if (distance.value >= pullThreshold) return 'ready'
  return distance.value > 0 ? 'pulling' : 'idle'
})
const text = computed(() => {
  if (state.value === 'refreshing') return t('feed.refreshing')
  if (state.value === 'ready') return t('feed.pullRelease')
  return t('feed.pullHint')
})

function onStart(event: TouchEvent) {
  start =
    !refreshing.value && event.touches.length === 1 && window.scrollY <= 0 ? event.touches[0]!.clientY : null
}

function onMove(event: TouchEvent) {
  if (start === null) return
  // The page moved under the finger after all: this is a scroll, not a pull.
  if (window.scrollY > 0) {
    start = null
    distance.value = 0
    return
  }
  distance.value = pullDistance(event.touches[0]!.clientY - start)
}

async function onEnd() {
  if (start === null) return
  start = null
  const pulled = distance.value >= pullThreshold
  if (!pulled) {
    distance.value = 0
    return
  }
  refreshing.value = true
  distance.value = pullThreshold
  try {
    await props.refresh()
  } catch {
    // The feed shows its own failure with «Повторить»; the mark only goes away.
  } finally {
    refreshing.value = false
    distance.value = 0
  }
}

let overscroll = ''
onMounted(() => {
  overscroll = document.documentElement.style.overscrollBehaviorY
  document.documentElement.style.overscrollBehaviorY = 'contain'
})
onBeforeUnmount(() => {
  document.documentElement.style.overscrollBehaviorY = overscroll
})
</script>

<template>
  <div
    class="pull"
    data-testid="pull-refresh"
    @touchstart.passive="onStart"
    @touchmove.passive="onMove"
    @touchend="onEnd()"
    @touchcancel="onEnd()"
  >
    <div
      class="pull__mark"
      :class="{ 'pull__mark--moving': state === 'pulling' || state === 'ready' }"
      data-testid="pull-refresh-mark"
      :data-state="state"
      :style="{ height: `${distance}px` }"
    >
      <RefreshCw
        class="pull__icon"
        :class="{ 'pull__icon--spinning': refreshing }"
        :style="refreshing ? undefined : { transform: `rotate(${(distance / pullThreshold) * 270}deg)` }"
        aria-hidden="true"
      />
      <span v-if="state !== 'idle'" class="pull__text" aria-hidden="true">{{ text }}</span>
    </div>
    <span class="visually-hidden" role="status">{{ refreshing ? t('feed.refreshing') : '' }}</span>
    <slot />
  </div>
</template>

<style scoped>
.pull__mark {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  overflow: hidden;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
  transition: height 0.2s ease;
}

.pull__mark--moving {
  transition: none;
}

.pull__icon {
  flex: none;
  width: var(--space-5);
  height: var(--space-5);
}

.pull__icon--spinning {
  animation: pull-spin 0.8s linear infinite;
}

@keyframes pull-spin {
  to {
    transform: rotate(360deg);
  }
}

@media (prefers-reduced-motion: reduce) {
  .pull__mark {
    transition: none;
  }

  .pull__icon--spinning {
    animation: none;
  }
}
</style>
