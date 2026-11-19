<script setup lang="ts">
// Tabs (E02-04, ADR-0013), WAI-ARIA "tabs" pattern with automatic activation: only the active tab is in the Tab
// order; ArrowLeft/ArrowRight move and select (wrapping, disabled tabs skipped), Home/End jump to the ends.
// Each tab's panel is the slot named after its id; v-model holds the active id (the first tab by default).
import { computed, nextTick, ref, useId } from 'vue'
import type { TabItem } from '../../types/ui'
import { nextIndex } from '../../utils/focus'

const props = defineProps<{
  tabs: TabItem[]
  /** Accessible name of the tab list. */
  label: string
}>()

const model = defineModel<string>()
const firstEnabled = () => props.tabs.find((tab) => !tab.disabled)?.id
const current = computed(() => model.value ?? firstEnabled())

const list = ref<HTMLElement>()
const base = useId()
const tabId = (id: string) => `${base}-tab-${id}`
const panelId = (id: string) => `${base}-panel-${id}`

async function select(index: number) {
  const tab = props.tabs[index]
  if (!tab || tab.disabled) return
  model.value = tab.id
  await nextTick()
  list.value?.querySelectorAll<HTMLButtonElement>('[role="tab"]')[index]?.focus()
}

function onKeydown(event: KeyboardEvent) {
  const count = props.tabs.length
  const from = props.tabs.findIndex((tab) => tab.id === current.value)
  const disabled = (index: number) => Boolean(props.tabs[index]?.disabled)
  const target = {
    ArrowRight: () => nextIndex(from, 1, count, disabled),
    ArrowLeft: () => nextIndex(from, -1, count, disabled),
    Home: () => nextIndex(-1, 1, count, disabled),
    End: () => nextIndex(count, -1, count, disabled),
  }[event.key]
  if (!target) return
  event.preventDefault()
  select(target())
}
</script>

<template>
  <div class="ui-tabs">
    <div ref="list" role="tablist" class="ui-tabs__list" :aria-label="label" @keydown="onKeydown">
      <button
        v-for="(tab, index) in tabs"
        :id="tabId(tab.id)"
        :key="tab.id"
        type="button"
        role="tab"
        class="ui-tabs__tab"
        :aria-selected="tab.id === current ? 'true' : 'false'"
        :aria-controls="panelId(tab.id)"
        :tabindex="tab.id === current ? 0 : -1"
        :disabled="tab.disabled"
        @click="select(index)"
      >
        {{ tab.label }}
      </button>
    </div>
    <div
      v-for="tab in tabs"
      :id="panelId(tab.id)"
      :key="tab.id"
      role="tabpanel"
      class="ui-tabs__panel"
      :aria-labelledby="tabId(tab.id)"
      :hidden="tab.id !== current"
      tabindex="0"
    >
      <slot :name="tab.id" />
    </div>
  </div>
</template>

<style scoped>
.ui-tabs__list {
  display: flex;
  gap: var(--space-1);
  overflow-x: auto;
  border-bottom: var(--size-border) solid var(--color-border);
}

.ui-tabs__tab {
  flex: none;
  min-height: var(--space-10);
  padding: var(--space-2) var(--space-3);
  border: 0;
  border-bottom: var(--size-focus-ring) solid transparent;
  background: transparent;
  color: var(--color-muted);
  font: inherit;
  font-weight: var(--font-weight-medium);
  cursor: pointer;
}

.ui-tabs__tab:hover:not(:disabled) {
  color: var(--color-text);
}

.ui-tabs__tab[aria-selected='true'] {
  border-bottom-color: var(--color-accent);
  color: var(--color-text);
}

.ui-tabs__tab:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.ui-tabs__tab:focus-visible {
  outline-offset: calc(-1 * var(--size-focus-ring));
}

.ui-tabs__panel {
  padding-top: var(--space-4);
}
</style>
