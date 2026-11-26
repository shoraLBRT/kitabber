<script setup lang="ts">
// Colour theme switch of the left menu (E02-05, docs/spec.md section 5): a native radio group, so arrows move
// between the options and the screen reader announces "1 of 3" without extra code.
import { Monitor, Moon, Sun } from 'lucide-vue-next'
import { onMounted, useId } from 'vue'
import { loadTheme, useTheme, type ThemeChoice } from '../../composables/useTheme'
import { t } from '../../i18n'

const options: { value: ThemeChoice; label: string; icon: typeof Sun }[] = [
  { value: 'system', label: t('shell.themeSystem'), icon: Monitor },
  { value: 'light', label: t('shell.themeLight'), icon: Sun },
  { value: 'dark', label: t('shell.themeDark'), icon: Moon },
]

const { theme, setTheme } = useTheme()
const name = `theme-${useId()}`
onMounted(loadTheme)
</script>

<template>
  <fieldset class="theme-switch" data-testid="theme-switch">
    <legend class="theme-switch__legend">{{ t('shell.theme') }}</legend>
    <div class="theme-switch__options">
      <label
        v-for="option in options"
        :key="option.value"
        class="theme-switch__option"
        :class="{ 'theme-switch__option--checked': theme === option.value }"
        :title="option.label"
      >
        <input
          type="radio"
          class="visually-hidden"
          :name="name"
          :value="option.value"
          :checked="theme === option.value"
          @change="setTheme(option.value)"
        />
        <component :is="option.icon" class="theme-switch__icon" aria-hidden="true" />
        <span class="visually-hidden">{{ option.label }}</span>
      </label>
    </div>
  </fieldset>
</template>

<style scoped>
.theme-switch {
  margin: 0;
  padding: 0;
  border: 0;
}

.theme-switch__legend {
  padding: 0;
  margin-bottom: var(--space-2);
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.theme-switch__options {
  display: inline-flex;
  gap: var(--space-1);
  padding: var(--space-1);
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}

.theme-switch__option {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: var(--space-2);
  border-radius: var(--radius-sm);
  color: var(--color-muted);
  cursor: pointer;
}

.theme-switch__option:hover {
  color: var(--color-text);
}

.theme-switch__option--checked {
  background: var(--color-surface);
  color: var(--color-accent);
}

.theme-switch__option:has(:focus-visible) {
  outline: var(--size-focus-ring) solid var(--color-focus);
  outline-offset: var(--size-focus-ring);
}

.theme-switch__icon {
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
}
</style>
