<script setup lang="ts">
// The row of reactions with their counts (E10-02, E10-03, FR-REACT, docs/ui/post.md, ADR-0072, ADR-0101): the kinds
// put on a post or a comment, most popular first, each drawn with its picture of the set (ReactionIcon, ADR-0095), the
// person's own one marked (accent, aria-pressed). The kinds in `always` stay in the row with no count, so there is
// something to press. A press on a chip asks the parent for that kind (useLikes: the own one is taken back, another one
// becomes the person's). The whole set opens over the row: on a desktop by hovering «Выбрать реакцию», on a phone by a
// long press on a chip or on that button, and anywhere by pressing it; choosing one in it changes the reaction in one
// step.
// The counts follow the locale ("1 000"). `panel` is the post's action panel, whose labels hide on a phone; `compact` is
// a comment's line, the label only for screen readers.
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { SmilePlus } from 'lucide-vue-next'
import ReactionIcon from './ReactionIcon.vue'
import type { ReactionSetState } from './ReactionSet.vue'
import UiButton from '../ui/UiButton.vue'
import { sortReactions, type ReactionCount, type ReactionKind } from '../../api/reactions'
import { reactionGraphic } from '../../utils/reaction-graphics'
import { formatNumber, t } from '../../i18n'

const props = withDefaults(
  defineProps<{
    reactions: ReactionCount[]
    /** Kinds shown even with no one's reaction: the button to press. */
    always?: ReactionKind[]
    /** The person may put, change and take back a reaction here; otherwise the row only shows the counts. */
    pressable?: boolean
    /** The person's last change is not answered yet. */
    busy?: boolean
    /** Why the row is not pressable, as a hint (one's own post or comment). */
    hint?: string
    variant?: 'panel' | 'compact'
    /** The test id of the like chip; its count gets `<testid>-count`, another kind's chip `<testid>-<kind>`. */
    testid?: string
    /** The set of reactions to choose from (the directory); `null` until it is first opened. */
    set?: ReactionSetState | null
  }>(),
  {
    always: () => ['like'],
    pressable: true,
    busy: false,
    hint: undefined,
    variant: 'panel',
    testid: 'like',
    set: null,
  },
)

const emit = defineEmits<{
  /** A kind is chosen, on a chip or in the set. */
  choose: [kind: ReactionKind]
  /** The set is opened: the parent reads it if it has not yet. */
  open: []
}>()

/** How long a finger stays on a chip before the set opens, and how long a pointer rests on the row (ms). */
const longPress = 500
const hoverDelay = 300

// The names come from the directory when it is read; until then, from the dictionary.
const names = computed<Record<string, string>>(() =>
  props.set?.status === 'found' ? Object.fromEntries(props.set.items.map((i) => [i.code, i.title])) : {},
)
const fallbackNames: Record<ReactionKind, () => string> = {
  like: () => t('reactions.names.like'),
  heart: () => t('reactions.names.heart'),
  smile: () => t('reactions.names.smile'),
  clap: () => t('reactions.names.clap'),
  idea: () => t('reactions.names.idea'),
  wow: () => t('reactions.names.wow'),
  sad: () => t('reactions.names.sad'),
}
const nameOf = (kind: ReactionKind) => names.value[kind] ?? fallbackNames[kind]?.() ?? kind

// A kind this build has no picture for (a newer server) is left out rather than drawn blank.
const shown = computed(() =>
  sortReactions(props.reactions, props.always).filter((r) => reactionGraphic(r.kind) !== null),
)
const mine = computed(() => props.reactions.find((r) => r.mine)?.kind ?? null)
const options = computed(() =>
  props.set?.status === 'found' ? props.set.items.filter((item) => reactionGraphic(item.code)) : [],
)
const chipTestid = (kind: ReactionKind) => (kind === 'like' ? props.testid : `${props.testid}-${kind}`)

const open = ref(false)
const root = ref<HTMLElement | null>(null)
const chooser = ref<HTMLElement | null>(null)
let hoverTimer: ReturnType<typeof setTimeout> | undefined
let pressTimer: ReturnType<typeof setTimeout> | undefined
let pressStart: { x: number; y: number } | null = null
/** A long press opened the set: the click that ends it is not a press on the chip. */
let swallowClick = false

function show() {
  if (!props.pressable || open.value) return
  open.value = true
  emit('open')
}

function hide(focusChooser = false) {
  clearTimeout(hoverTimer)
  if (!open.value) return
  open.value = false
  if (focusChooser) chooser.value?.focus()
}

// Hovering «Выбрать реакцию» with a mouse or a pen opens the set after a short rest, so a press on a chip does not; leaving
// the button and the set closes it.
function enter(event: PointerEvent) {
  if (event.pointerType === 'touch') return
  clearTimeout(hoverTimer)
  hoverTimer = setTimeout(show, hoverDelay)
}

function leave(event: PointerEvent) {
  if (event.pointerType === 'touch') return
  clearTimeout(hoverTimer)
  hoverTimer = setTimeout(() => hide(), hoverDelay)
}

// A finger held on a chip opens the set; moving it away first is a scroll, not a press.
function pressDown(event: PointerEvent) {
  // A phone may send no click after a long press: the next press starts afresh.
  swallowClick = false
  if (event.pointerType !== 'touch' || !props.pressable) return
  pressStart = { x: event.clientX, y: event.clientY }
  clearTimeout(pressTimer)
  pressTimer = setTimeout(() => {
    swallowClick = true
    show()
  }, longPress)
}

function pressMove(event: PointerEvent) {
  if (!pressStart) return
  if (Math.hypot(event.clientX - pressStart.x, event.clientY - pressStart.y) > 10) pressEnd()
}

function pressEnd() {
  clearTimeout(pressTimer)
  pressStart = null
}

function press(kind: ReactionKind) {
  if (swallowClick) {
    swallowClick = false
    return
  }
  hide()
  emit('choose', kind)
}

function pick(kind: string) {
  hide(true)
  emit('choose', kind as ReactionKind)
}

function toggleSet() {
  if (swallowClick) {
    swallowClick = false
    return
  }
  if (open.value) hide()
  else show()
}

// A press outside the row and the set closes it, as Escape does.
function outside(event: PointerEvent) {
  if (root.value && !root.value.contains(event.target as Node)) hide()
}

watch(open, (now) => {
  if (now) document.addEventListener('pointerdown', outside)
  else document.removeEventListener('pointerdown', outside)
})

onBeforeUnmount(() => {
  clearTimeout(hoverTimer)
  clearTimeout(pressTimer)
  document.removeEventListener('pointerdown', outside)
})
</script>

<template>
  <span
    ref="root"
    class="reactions"
    :class="`reactions--${variant}`"
    role="group"
    :aria-label="t('reactions.row')"
    data-testid="reaction-row"
    @keydown.escape="hide(true)"
  >
    <button
      v-for="reaction in shown"
      :key="reaction.kind"
      type="button"
      class="reaction"
      :class="{ 'reaction--mine': reaction.mine, 'reaction--bare': reaction.count === 0 }"
      :disabled="!pressable"
      :aria-pressed="pressable ? (reaction.mine ? 'true' : 'false') : undefined"
      :aria-busy="busy ? 'true' : undefined"
      :title="pressable ? undefined : hint"
      :data-kind="reaction.kind"
      :data-testid="chipTestid(reaction.kind)"
      @pointerdown="pressDown"
      @pointermove="pressMove"
      @pointerup="pressEnd"
      @pointercancel="pressEnd"
      @contextmenu="swallowClick && $event.preventDefault()"
      @click="press(reaction.kind)"
    >
      <ReactionIcon :code="reaction.kind" :size="variant === 'panel' ? 'md' : 'sm'" />
      <span class="reaction__label">{{ nameOf(reaction.kind) }}</span>
      <b
        v-if="reaction.count > 0"
        class="reaction__count"
        :data-testid="`${chipTestid(reaction.kind)}-count`"
        >{{ formatNumber(reaction.count) }}</b
      >
    </button>
    <button
      v-if="pressable"
      ref="chooser"
      type="button"
      class="reaction reaction--chooser"
      :aria-label="t('reactions.choose')"
      :title="t('reactions.choose')"
      :aria-expanded="open ? 'true' : 'false'"
      aria-haspopup="true"
      :data-testid="`${testid}-choose`"
      @pointerenter="enter"
      @pointerleave="leave"
      @pointerdown="pressDown"
      @pointermove="pressMove"
      @pointerup="pressEnd"
      @pointercancel="pressEnd"
      @click="toggleSet"
    >
      <SmilePlus class="reaction__chooser-icon" aria-hidden="true" />
    </button>

    <span
      v-if="open"
      class="reactions__set"
      role="group"
      :aria-label="t('reactions.set.list')"
      :aria-busy="set === null || set.status === 'loading' ? 'true' : undefined"
      data-testid="reaction-picker"
      @pointerenter="enter"
      @pointerleave="leave"
    >
      <span
        v-if="set === null || set.status === 'loading'"
        class="reactions__note"
        role="status"
        data-testid="reaction-picker-loading"
        >{{ t('reactions.set.loading') }}</span
      >
      <span
        v-else-if="set.status === 'error'"
        class="reactions__note"
        role="alert"
        data-testid="reaction-picker-error"
      >
        {{ t('reactions.set.failed') }}
        <UiButton variant="secondary" size="sm" @click="emit('open')">{{ t('common.retry') }}</UiButton>
      </span>
      <span v-else-if="options.length === 0" class="reactions__note" data-testid="reaction-picker-empty">{{
        t('reactions.set.empty')
      }}</span>
      <template v-else>
        <button
          v-for="item in options"
          :key="item.code"
          type="button"
          class="reactions__option"
          :class="{ 'reactions__option--mine': mine === item.code }"
          :aria-label="item.title"
          :title="item.title"
          :aria-pressed="mine === item.code ? 'true' : 'false'"
          :disabled="busy"
          :data-testid="`reaction-option-${item.code}`"
          @click="pick(item.code)"
        >
          <ReactionIcon :code="item.code" size="md" />
        </button>
      </template>
    </span>
  </span>
</template>

<style scoped>
.reactions {
  position: relative;
  display: inline-flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-1);
}

.reaction {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 0;
  background: transparent;
  font: inherit;
  font-size: var(--font-size-sm);
  cursor: pointer;
  user-select: none;
  -webkit-touch-callout: none;
}

.reaction__count {
  font-variant-numeric: tabular-nums;
}

.reaction:disabled {
  cursor: default;
}

/* The label is for screen readers, except on the panel's bare chip on a desktop: the call to press it. */
.reaction__label {
  position: absolute;
  width: var(--size-border);
  height: var(--size-border);
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}

/* The post's action panel: pill buttons like the panel's other items. */
.reactions--panel .reaction {
  gap: var(--space-2);
  min-height: var(--size-touch);
  padding: 0 var(--space-3);
  border-radius: var(--radius-full);
  color: var(--color-text);
}

.reactions--panel .reaction:hover:not(:disabled) {
  background: var(--color-surface-subtle);
}

.reactions--panel .reaction--bare .reaction__label {
  position: static;
  width: auto;
  height: auto;
  overflow: visible;
  clip-path: none;
}

.reactions--panel .reaction__count {
  font-weight: var(--font-weight-bold);
}

.reactions--panel .reaction:disabled {
  color: var(--color-muted);
}

/* A comment's line: picture and count only, muted until pressed. */
.reactions--compact .reaction {
  gap: var(--space-1);
  padding: 0 var(--space-1);
  border-radius: var(--radius-full);
  color: var(--color-muted);
}

.reactions--compact .reaction:hover:not(:disabled) {
  color: var(--color-accent);
}

.reaction--mine,
.reactions--panel .reaction--mine:hover:not(:disabled) {
  color: var(--color-accent);
  box-shadow: inset 0 0 0 var(--size-border) var(--color-accent);
}

.reaction--chooser {
  color: var(--color-muted);
}

.reactions--panel .reaction--chooser {
  padding: 0 var(--space-2);
}

.reaction__chooser-icon {
  width: var(--size-icon-md);
  height: var(--size-icon-md);
}

.reactions--compact .reaction__chooser-icon {
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
}

/* The set over the row, from its start. */
.reactions__set {
  position: absolute;
  bottom: 100%;
  left: 0;
  z-index: 6;
  display: flex;
  align-items: center;
  gap: var(--space-1);
  margin-bottom: var(--space-1);
  padding: var(--space-1);
  border: var(--size-border) solid var(--color-border);
  border-radius: var(--radius-full);
  background: var(--color-surface);
  box-shadow: 0 var(--space-1) var(--space-3) var(--color-shadow);
  white-space: nowrap;
}

.reactions__note {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  padding: 0 var(--space-3);
  min-height: var(--size-touch);
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.reactions__option {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: var(--size-touch);
  height: var(--size-touch);
  border: 0;
  border-radius: var(--radius-full);
  background: transparent;
  cursor: pointer;
  transition: transform 0.1s ease-out;
}

.reactions__option:hover:not(:disabled),
.reactions__option:focus-visible {
  background: var(--color-surface-subtle);
  transform: scale(1.15);
}

.reactions__option--mine {
  box-shadow: inset 0 0 0 var(--size-border) var(--color-accent);
}

@media (prefers-reduced-motion: reduce) {
  .reactions__option {
    transition: none;
  }

  .reactions__option:hover:not(:disabled),
  .reactions__option:focus-visible {
    transform: none;
  }
}

@media (max-width: 639px) {
  .reactions--panel .reaction {
    padding: 0 var(--space-2);
  }

  .reactions--panel .reaction--bare .reaction__label {
    position: absolute;
    width: var(--size-border);
    height: var(--size-border);
    overflow: hidden;
    clip-path: inset(50%);
  }
}
</style>
