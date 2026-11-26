<script setup lang="ts">
// The row of reactions with their counts (E10-03, FR-REACT, docs/ui/post.md, ADR-0072): the kinds put on a post or a
// comment, most popular first, the person's own one marked (accent colour, filled icon, aria-pressed). The kinds in
// `always` stay in the row with no count, so there is something to press; a press asks the parent to turn the kind
// over (useLikes). The counts follow the locale ("1 000"). Only «Нравится» exists for now: the set of reactions and
// the choice by a long press (E02-12, E10-02) add kinds, and the row shows them as they come. `panel` is the post's
// action panel, whose labels hide on a phone; `compact` is a comment's line, the label only for screen readers.
import { computed, type Component } from 'vue'
import { Heart } from 'lucide-vue-next'
import { sortReactions, type ReactionCount, type ReactionKind } from '../../api/reactions'
import { formatNumber, t } from '../../i18n'

const props = withDefaults(
  defineProps<{
    reactions: ReactionCount[]
    /** Kinds shown even with no one's reaction: the button to press. */
    always?: ReactionKind[]
    /** The person may put and take back a reaction here; otherwise the row only shows the counts. */
    pressable?: boolean
    /** The person's last change is not answered yet. */
    busy?: boolean
    /** Why the row is not pressable, as a hint (one's own post or comment). */
    hint?: string
    variant?: 'panel' | 'compact'
    /** The test id of the like chip; its count gets `<testid>-count`. */
    testid?: string
  }>(),
  { always: () => ['like'], pressable: true, busy: false, hint: undefined, variant: 'panel', testid: 'like' },
)

const emit = defineEmits<{ toggle: [kind: ReactionKind] }>()

const icons: Record<ReactionKind, Component> = { like: Heart }
const labels: Record<ReactionKind, () => string> = { like: () => t('actions.like') }

// A kind this page has no icon for (a newer server) is left out rather than drawn blank.
const shown = computed(() => sortReactions(props.reactions, props.always).filter((r) => r.kind in labels))
const chipTestid = (kind: ReactionKind) => (kind === 'like' ? props.testid : `${props.testid}-${kind}`)
</script>

<template>
  <span
    class="reactions"
    :class="`reactions--${variant}`"
    role="group"
    :aria-label="t('reactions.row')"
    data-testid="reaction-row"
  >
    <button
      v-for="reaction in shown"
      :key="reaction.kind"
      type="button"
      class="reaction"
      :class="{ 'reaction--mine': reaction.mine }"
      :disabled="!pressable"
      :aria-pressed="pressable ? (reaction.mine ? 'true' : 'false') : undefined"
      :aria-busy="busy ? 'true' : undefined"
      :title="pressable ? undefined : hint"
      :data-kind="reaction.kind"
      :data-testid="chipTestid(reaction.kind)"
      @click="emit('toggle', reaction.kind)"
    >
      <component :is="icons[reaction.kind]" class="reaction__icon" aria-hidden="true" />
      <span class="reaction__label">{{ labels[reaction.kind]() }}</span>
      <b
        v-if="reaction.count > 0"
        class="reaction__count"
        :data-testid="`${chipTestid(reaction.kind)}-count`"
        >{{ formatNumber(reaction.count) }}</b
      >
    </button>
  </span>
</template>

<style scoped>
.reactions {
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
}

.reaction__count {
  font-variant-numeric: tabular-nums;
}

.reaction--mine .reaction__icon {
  fill: currentcolor;
}

.reaction:disabled {
  cursor: default;
}

/* The post's action panel: pill buttons like the panel's other items. */
.reactions--panel .reaction {
  gap: var(--space-2);
  min-height: var(--size-touch);
  padding: 0 var(--space-4);
  border-radius: var(--radius-full);
  color: var(--color-text);
}

.reactions--panel .reaction:hover:not(:disabled) {
  background: var(--color-surface-subtle);
}

.reactions--panel .reaction__icon {
  width: var(--size-icon-md);
  height: var(--size-icon-md);
}

.reactions--panel .reaction__count {
  font-weight: var(--font-weight-bold);
}

.reactions--panel .reaction:disabled {
  color: var(--color-muted);
}

/* A comment's line: icon and count only, muted until pressed. */
.reactions--compact .reaction {
  gap: var(--space-1);
  padding: 0;
  color: var(--color-muted);
}

.reactions--compact .reaction:hover:not(:disabled) {
  color: var(--color-accent);
}

.reactions--compact .reaction__icon {
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
}

.reaction--mine,
.reactions--panel .reaction--mine:hover:not(:disabled) {
  color: var(--color-accent);
}

/* The label stays for screen readers: on a comment always, in the panel on a phone, as docs/ui/post.md draws it. */
.reactions--compact .reaction__label {
  position: absolute;
  width: var(--size-border);
  height: var(--size-border);
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}

@media (max-width: 639px) {
  .reactions--panel .reaction {
    padding: 0 var(--space-2);
  }

  .reactions--panel .reaction__label {
    position: absolute;
    width: var(--size-border);
    height: var(--size-border);
    overflow: hidden;
    clip-path: inset(50%);
    white-space: nowrap;
  }
}
</style>
