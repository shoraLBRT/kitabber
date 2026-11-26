// The person's cookie choice in this browser (E02-13, FR-UI, ADR-0066). Read once on the client from the
// `cookie_consent` cookie; until then, and while nothing is chosen, every optional category is off. The banner
// (components/shell/ShellCookieBanner.vue) asks while there is no choice; /settings/cookies changes it later.
// Code that would set an optional cookie starts only through whenConsented(), and names its cookies there, so a
// withdrawal removes them at once.
import { computed, ref, type ComputedRef } from 'vue'
import {
  consentCategories,
  consentCookie,
  consentCookieName,
  expiredCookie,
  parseConsent,
  readCookie,
  type ConsentCategory,
  type ConsentChoice,
} from '../utils/cookie-consent'

const choice = ref<ConsentChoice | null>(null)
const loaded = ref(false)

interface Waiter {
  start: () => void
  cookies: readonly string[]
  started: boolean
}
const waiters = new Map<ConsentCategory, Set<Waiter>>(
  consentCategories.map((category) => [category, new Set()]),
)

function allowed(category: ConsentCategory): boolean {
  return choice.value?.[category] === true
}

function startAllowed(): void {
  for (const [category, set] of waiters) {
    if (!allowed(category)) continue
    for (const waiter of set) {
      if (waiter.started) continue
      waiter.started = true
      waiter.start()
    }
  }
}

/** Call from onMounted (or other client-only code): reads the saved choice once. */
export function loadCookieConsent(): void {
  if (loaded.value || typeof document === 'undefined') return
  choice.value = parseConsent(readCookie(document.cookie, consentCookieName))
  loaded.value = true
  startAllowed()
}

/** Records a choice: saves it for a year, removes the cookies of what is now refused, starts what is now allowed. */
export function setCookieConsent(next: ConsentChoice): void {
  choice.value = { ...next }
  loaded.value = true
  if (typeof document === 'undefined') return
  document.cookie = consentCookie(next, window.location.protocol === 'https:')
  for (const [category, set] of waiters) {
    if (allowed(category)) continue
    for (const waiter of set) {
      waiter.started = false
      for (const name of waiter.cookies) document.cookie = expiredCookie(name)
    }
  }
  startAllowed()
}

/** Whether the person has allowed the category; false while nothing is chosen. */
export function hasCookieConsent(category: ConsentCategory): boolean {
  return allowed(category)
}

/**
 * Runs `start` once the category is allowed — now, or when the person allows it — and again after a withdrawal and a
 * new consent. `cookies` are the names it sets; they are removed when the category is withdrawn. Returns a function
 * that stops waiting.
 */
export function whenConsented(
  category: ConsentCategory,
  start: () => void,
  cookies: readonly string[] = [],
): () => void {
  const waiter: Waiter = { start, cookies, started: false }
  waiters.get(category)!.add(waiter)
  if (loaded.value) startAllowed()
  return () => waiters.get(category)!.delete(waiter)
}

export function useCookieConsent(): {
  choice: ComputedRef<ConsentChoice | null>
  /** The saved choice has been read: the banner may decide whether to show. */
  loaded: ComputedRef<boolean>
  /** The person has chosen; the banner does not ask again. */
  decided: ComputedRef<boolean>
  setCookieConsent: typeof setCookieConsent
} {
  return {
    choice: computed(() => choice.value),
    loaded: computed(() => loaded.value),
    decided: computed(() => choice.value !== null),
    setCookieConsent,
  }
}

/** Test hook: forget the loaded choice and the waiters between tests. */
export function resetCookieConsentForTests(): void {
  choice.value = null
  loaded.value = false
  for (const set of waiters.values()) set.clear()
}
