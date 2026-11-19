<script setup lang="ts">
// One widget of the right column (E02-05) with the shared FR-UI states of docs/ui/README.md: skeleton while
// loading, a message with "Повторить" on error, a quiet note when empty, the default slot when ready.
import { CircleAlert } from 'lucide-vue-next'
import { useId } from 'vue'
import UiButton from '../ui/UiButton.vue'

export type WidgetState = 'ready' | 'loading' | 'error' | 'empty'

withDefaults(
  defineProps<{
    title: string
    state?: WidgetState
    emptyText?: string
  }>(),
  { state: 'empty', emptyText: 'Пока здесь пусто' },
)

const emit = defineEmits<{ retry: [] }>()
const headingId = `widget-${useId()}`
</script>

<template>
  <section
    class="shell-widget"
    :aria-labelledby="headingId"
    :aria-busy="state === 'loading' ? 'true' : undefined"
    :data-state="state"
  >
    <h2 :id="headingId" class="shell-widget__title">{{ title }}</h2>
    <div v-if="state === 'loading'" class="shell-widget__skeleton" data-testid="widget-skeleton">
      <span class="visually-hidden">Загрузка</span>
      <span v-for="n in 3" :key="n" class="shell-widget__bar" aria-hidden="true" />
    </div>
    <div v-else-if="state === 'error'" class="shell-widget__error" role="alert">
      <p class="shell-widget__message">
        <CircleAlert class="shell-widget__icon" aria-hidden="true" />
        Не удалось загрузить
      </p>
      <UiButton variant="secondary" size="sm" @click="emit('retry')">Повторить</UiButton>
    </div>
    <p v-else-if="state === 'empty'" class="shell-widget__empty">{{ emptyText }}</p>
    <slot v-else />
  </section>
</template>

<style scoped>
.shell-widget {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  padding: var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.shell-widget__title {
  margin: 0;
  font-size: var(--font-size-md);
  font-weight: var(--font-weight-bold);
}

.shell-widget__skeleton {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.shell-widget__bar {
  height: var(--space-4);
  border-radius: var(--radius-sm);
  background: var(--color-surface-subtle);
}

.shell-widget__bar:last-child {
  width: 60%;
}

.shell-widget__error {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: var(--space-2);
}

.shell-widget__message,
.shell-widget__empty {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  margin: 0;
  font-size: var(--font-size-sm);
}

.shell-widget__empty {
  color: var(--color-muted);
}

.shell-widget__icon {
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
  color: var(--color-danger);
}
</style>
