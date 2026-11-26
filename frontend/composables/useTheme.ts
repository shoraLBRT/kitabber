// Colour theme choice of the left menu and of «Основные» in the settings (E02-05, E04-08, FR-UI, ADR-0012,
// ADR-0070): light, dark or system. The choice sets data-theme on <html> (brand/tokens.css) and is remembered in
// this browser; "system" removes the attribute so prefers-color-scheme decides. Signed in, the account's saved choice
// wins over the browser's when the site starts (adoptAccountTheme), and a new choice is also saved to the account
// through the saver that plugins/preferences.client.ts sets; a failed save puts the previous choice back.
import { ref, type Ref } from 'vue'

export type ThemeChoice = 'system' | 'light' | 'dark'
export const themeChoices: readonly ThemeChoice[] = ['system', 'light', 'dark']
export const themeStorageKey = 'theme'

const choice = ref<ThemeChoice>('system')
let loaded = false
let saver: ((value: ThemeChoice) => Promise<boolean>) | null = null

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

function choose(value: ThemeChoice): void {
  choice.value = value
  apply(value)
  try {
    if (value === 'system') window.localStorage.removeItem(themeStorageKey)
    else window.localStorage.setItem(themeStorageKey, value)
  } catch {
    // Storage may be blocked; the choice still holds for this page.
  }
}

/**
 * Applies and remembers the choice at once; signed in, saves it to the account too. Resolves to whether it held:
 * `false` when the account refused or could not be reached, and then the previous choice is back.
 */
export async function setTheme(value: ThemeChoice): Promise<boolean> {
  const before = choice.value
  choose(value)
  if (!saver || value === before) return true
  const saved = await saver(value)
  if (!saved && choice.value === value) choose(before)
  return saved
}

/** The choice saved to the account, read when the site starts: it replaces this browser's. */
export function adoptAccountTheme(value: ThemeChoice): void {
  if (!themeChoices.includes(value)) return
  loaded = true
  choose(value)
}

/** Where a new choice is saved besides this browser: the account's settings while signed in, `null` for none. */
export function saveThemeWith(save: ((value: ThemeChoice) => Promise<boolean>) | null): void {
  saver = save
}

export function useTheme(): { theme: Readonly<Ref<ThemeChoice>>; setTheme: typeof setTheme } {
  return { theme: choice, setTheme }
}

/** Test hook: forget the loaded state and the saver between tests. */
export function resetThemeForTests(): void {
  loaded = false
  saver = null
  choice.value = 'system'
}
