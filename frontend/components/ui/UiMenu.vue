<script setup lang="ts">
// Menu button (E02-04, ADR-0013), WAI-ARIA "menu button" pattern: the trigger opens a role="menu" list.
// Keyboard: Enter/Space/ArrowDown open on the first item, ArrowUp on the last; arrows move (wrapping, disabled
// items skipped), Home/End jump, Enter/Space choose, Escape closes and returns focus to the trigger, Tab closes.
// A click outside closes the menu too.
import { ChevronDown } from 'lucide-vue-next'
import { nextTick, onBeforeUnmount, ref, useId, watch } from 'vue'
import type { MenuItem } from '../../types/ui'
import { nextIndex } from '../../utils/focus'

const props = withDefaults(
  defineProps<{
    items: MenuItem[]
    /** Trigger text; also its accessible name when the trigger slot shows only an icon or an avatar. */
    label: string
    align?: 'start' | 'end'
  }>(),
  { align: 'start' },
)

const emit = defineEmits<{ select: [id: string] }>()

const open = ref(false)
const active = ref(-1)
const root = ref<HTMLElement>()
const trigger = ref<HTMLButtonElement>()
const itemAt = (index: number) =>
  root.value?.querySelectorAll<HTMLButtonElement>('[role="menuitem"]')[index]
const menuId = useId()

const isDisabled = (index: number) => Boolean(props.items[index]?.disabled)

async function openAt(index: number) {
  open.value = true
  active.value = index
  await nextTick()
  if (index >= 0) itemAt(index)?.focus()
}

function close(returnFocus: boolean) {
  open.value = false
  active.value = -1
  if (returnFocus) trigger.value?.focus()
}

const first = () => nextIndex(-1, 1, props.items.length, isDisabled)
const last = () => nextIndex(props.items.length, -1, props.items.length, isDisabled)

function onTriggerClick() {
  if (open.value) close(false)
  else openAt(first())
}

function onTriggerKeydown(event: KeyboardEvent) {
  if (event.key === 'ArrowDown') {
    event.preventDefault()
    openAt(first())
  } else if (event.key === 'ArrowUp') {
    event.preventDefault()
    openAt(last())
  }
}

function move(index: number) {
  if (index < 0) return
  active.value = index
  itemAt(index)?.focus()
}

function onMenuKeydown(event: KeyboardEvent) {
  const count = props.items.length
  switch (event.key) {
    case 'ArrowDown':
      event.preventDefault()
      move(nextIndex(active.value, 1, count, isDisabled))
      break
    case 'ArrowUp':
      event.preventDefault()
      move(nextIndex(active.value, -1, count, isDisabled))
      break
    case 'Home':
      event.preventDefault()
      move(first())
      break
    case 'End':
      event.preventDefault()
      move(last())
      break
    case 'Escape':
      // Stop here so a dialog around the menu stays open.
      event.preventDefault()
      event.stopPropagation()
      close(true)
      break
    case 'Tab':
      close(false)
      break
  }
}

function choose(item: MenuItem) {
  if (item.disabled) return
  close(true)
  emit('select', item.id)
}

function onDocumentPointerdown(event: Event) {
  if (root.value && !root.value.contains(event.target as Node)) close(false)
}

watch(open, (isOpen) => {
  if (isOpen) document.addEventListener('pointerdown', onDocumentPointerdown)
  else document.removeEventListener('pointerdown', onDocumentPointerdown)
})
onBeforeUnmount(() => document.removeEventListener('pointerdown', onDocumentPointerdown))
</script>

<template>
  <div ref="root" class="ui-menu">
    <button
      ref="trigger"
      type="button"
      class="ui-menu__trigger"
      aria-haspopup="menu"
      :aria-expanded="open ? 'true' : 'false'"
      :aria-controls="open ? menuId : undefined"
      :aria-label="$slots.trigger ? label : undefined"
      @click="onTriggerClick"
      @keydown="onTriggerKeydown"
    >
      <slot name="trigger">
        <span>{{ label }}</span>
        <ChevronDown class="ui-menu__chevron" aria-hidden="true" />
      </slot>
    </button>
    <ul
      v-if="open"
      :id="menuId"
      role="menu"
      class="ui-menu__list"
      :class="`ui-menu__list--${align}`"
      :aria-label="label"
      @keydown="onMenuKeydown"
    >
      <li v-for="(item, index) in items" :key="item.id" role="none">
        <button
          type="button"
          role="menuitem"
          tabindex="-1"
          class="ui-menu__item"
          :class="{ 'ui-menu__item--danger': item.danger }"
          :aria-disabled="item.disabled ? 'true' : undefined"
          :data-id="item.id"
          @click="choose(item)"
          @mousemove="!item.disabled && active !== index && move(index)"
        >
          <component :is="item.icon" v-if="item.icon" class="ui-menu__icon" aria-hidden="true" />
          {{ item.label }}
        </button>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.ui-menu {
  position: relative;
  display: inline-block;
}

.ui-menu__trigger {
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
  min-height: var(--space-10);
  padding: var(--space-1) var(--space-2);
  border: 0;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--color-text);
  font: inherit;
  cursor: pointer;
}

.ui-menu__trigger:hover,
.ui-menu__trigger[aria-expanded='true'] {
  background: var(--color-surface-subtle);
}

.ui-menu__chevron {
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
}

.ui-menu__list {
  position: absolute;
  z-index: 20;
  top: calc(100% + var(--space-1));
  min-width: calc(var(--size-menu) - var(--space-8));
  margin: 0;
  padding: var(--space-1);
  border: var(--size-border) solid var(--color-border);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  box-shadow: 0 var(--space-2) var(--space-6) var(--color-shadow);
  list-style: none;
}

.ui-menu__list--start {
  left: 0;
}

.ui-menu__list--end {
  right: 0;
}

.ui-menu__item {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  width: 100%;
  min-height: var(--space-10);
  padding: var(--space-2) var(--space-3);
  border: 0;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--color-text);
  font: inherit;
  text-align: left;
  cursor: pointer;
}

.ui-menu__item:hover,
.ui-menu__item:focus-visible {
  background: var(--color-surface-subtle);
}

.ui-menu__item:focus-visible {
  outline-offset: calc(-1 * var(--size-focus-ring));
}

.ui-menu__item--danger {
  color: var(--color-danger);
}

.ui-menu__item[aria-disabled='true'] {
  opacity: 0.5;
  cursor: not-allowed;
}

.ui-menu__icon {
  flex: none;
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
}
</style>
