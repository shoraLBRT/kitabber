<script setup lang="ts">
// «Основные» of the settings (E04-08, FR-UI, docs/ui/settings.md, ADR-0070): the colour theme. A choice applies at
// once and is saved to the account (composables/useTheme.ts), so every device of the person shows it; «Изменения
// сохранены» confirms the save, a failure puts the previous choice back with a toast. Until the account's settings
// are read the section shows a skeleton, and «Повторить» when they could not be.
import { onMounted, useId } from 'vue'
import { useAccountPreferences } from '../../composables/useAccountPreferences'
import { loadTheme, useTheme, type ThemeChoice } from '../../composables/useTheme'
import { useToast } from '../../composables/useToast'
import UiButton from '../ui/UiButton.vue'
import { t } from '../../i18n'

const options: { value: ThemeChoice; label: string }[] = [
  { value: 'system', label: t('shell.themeSystem') },
  { value: 'light', label: t('shell.themeLight') },
  { value: 'dark', label: t('shell.themeDark') },
]

const { theme, setTheme } = useTheme()
const { state, retry } = useAccountPreferences()
const toast = useToast()
const name = `general-theme-${useId()}`
const hint = `${name}-hint`
onMounted(loadTheme)

async function choose(value: ThemeChoice) {
  if (await setTheme(value)) toast.show(t('settings.saved'), { tone: 'success' })
}
</script>

<template>
  <div
    v-if="state === 'idle' || state === 'loading'"
    class="general-settings"
    aria-busy="true"
    data-testid="settings-loading"
  >
    <span class="visually-hidden" role="status">{{ t('settings.loading') }}</span>
    <div class="general-settings__skeleton general-settings__skeleton--label" />
    <div v-for="n in 3" :key="n" class="general-settings__skeleton general-settings__skeleton--option" />
  </div>

  <div v-else-if="state === 'error'" class="general-settings" data-testid="settings-error">
    <h2 class="general-settings__title">{{ t('settings.loadFailed') }}</h2>
    <p class="general-settings__hint" role="alert">{{ t('settings.loadFailedText') }}</p>
    <div>
      <UiButton variant="secondary" @click="retry()">{{ t('common.retry') }}</UiButton>
    </div>
  </div>

  <fieldset v-else class="general-settings" :aria-describedby="hint" data-testid="general-settings">
    <legend class="general-settings__title">{{ t('settings.theme') }}</legend>
    <p :id="hint" class="general-settings__hint">{{ t('settings.themeHint') }}</p>
    <label v-for="option in options" :key="option.value" class="general-settings__option">
      <input
        type="radio"
        class="general-settings__radio"
        :name="name"
        :value="option.value"
        :checked="theme === option.value"
        @change="choose(option.value)"
      />
      <span>{{ option.label }}</span>
    </label>
  </fieldset>
</template>

<style scoped>
.general-settings {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  min-width: 0;
  margin: 0;
  padding: 0;
  border: 0;
}

.general-settings__title {
  margin: 0;
  padding: 0;
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-bold);
}

.general-settings__hint {
  margin: 0 0 var(--space-2);
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.general-settings__option {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  min-height: var(--size-touch);
  cursor: pointer;
}

.general-settings__radio {
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
  margin: 0;
  accent-color: var(--color-accent);
}

.general-settings__skeleton {
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}

.general-settings__skeleton--label {
  width: 40%;
  height: var(--space-6);
}

.general-settings__skeleton--option {
  height: var(--size-touch);
}
</style>
