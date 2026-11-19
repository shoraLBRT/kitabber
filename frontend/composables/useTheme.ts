// Colour theme choice of the left menu (E02-05, FR-UI, ADR-0012): light, dark or system. The choice sets
// data-theme on <html> (brand/tokens.css) and is remembered in this browser; "system" removes the attribute so
// prefers-color-scheme decides. Saving it to the account belongs to the settings screen (docs/ui/settings.md).
import { ref, type Ref } from 'vue'

export type ThemeChoice = 'system' | 'light' | 'dark'
export const themeChoices: readonly ThemeChoice[] = ['system', 'light', 'dark']
export const themeStorageKey = 'theme'

const choice = ref<ThemeChoice>('system')
let loaded = false

function apply(value: ThemeChoice) {
  if (typeof document === 'undefined') return
  if (value === 'system') delete document.documentElement.dataset.theme
  else document.documentElement.dataset.theme = value
}

function read(): ThemeChoice {
  try {
    const saved = window.localStorage.getItem(themeStorageKey)
    return themeChoices.includes(saved as ThemeChoice) ? (saved as ThemeChoice) : 'system'
  } catch {
    return 'system'
  }
}

/** Call from onMounted (or other client-only code): reads the saved choice once and applies it. */
export function loadTheme(): void {
  if (loaded || typeof window === 'undefined') return
  loaded = true
  choice.value = read()
  apply(choice.value)
}

export function setTheme(value: ThemeChoice): void {
  choice.value = value
  apply(value)
  try {
    if (value === 'system') window.localStorage.removeItem(themeStorageKey)
    else window.localStorage.setItem(themeStorageKey, value)
  } catch {
    // Storage may be blocked; the choice still holds for this page.
  }
}

export function useTheme(): { theme: Readonly<Ref<ThemeChoice>>; setTheme: typeof setTheme } {
  return { theme: choice, setTheme }
}

/** Test hook: forget the loaded state between tests. */
export function resetThemeForTests(): void {
  loaded = false
  choice.value = 'system'
}
