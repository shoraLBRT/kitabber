// The account's settings in this browser (E04-08, ADR-0070). Signed in, the site reads them once when it starts and
// applies them — so a choice made on another device holds here too — and every new theme choice, from the left menu
// or from «Основные», is saved to the account. A failed save shows a toast and the previous choice comes back.
import { ref } from 'vue'
import type { Preferences, PreferencesApi } from '../api/preferences'
import { adoptAccountTheme, saveThemeWith, type ThemeChoice } from './useTheme'
import { useToast } from './useToast'
import { t } from '../i18n'

type LoadState = 'idle' | 'loading' | 'loaded' | 'error'

const state = ref<LoadState>('idle')
let source: PreferencesApi | null = null

async function load(): Promise<Preferences | null> {
  if (!source) return null
  state.value = 'loading'
  const preferences = await source.load()
  state.value = preferences ? 'loaded' : 'error'
  if (preferences) adoptAccountTheme(preferences.general.theme as ThemeChoice)
  return preferences
}

/** Connects the signed-in account: reads its settings and saves new choices to it. */
export function connectAccountPreferences(api: PreferencesApi): Promise<Preferences | null> {
  source = api
  saveThemeWith(async (theme) => {
    const saved = await api.saveGeneral(theme)
    if (!saved) useToast().show(t('settings.saveFailed'), { tone: 'error' })
    return saved
  })
  return load()
}

/** Whether the account's settings are read: the settings sections show a skeleton, then the choice or an error. */
export function useAccountPreferences() {
  return {
    state,
    /** Reads them again, after a failure. */
    retry: load,
  }
}

/** Test hook: forget the connection between tests. */
export function resetAccountPreferencesForTests(): void {
  source = null
  state.value = 'idle'
  saveThemeWith(null)
}
