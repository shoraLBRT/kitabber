// What the browser reports to the view counter (E07-06, FR-POST, ADR-0071). Only from script and only once the page is
// drawn, so a crawler that runs no script never counts:
// - an opened post's page — once per page shown; the API counts a reader once a day;
// - the cards a feed showed — a card counts once at least half of it stayed on screen for a second; the cards are
//   sent together every few seconds and when the page is left;
// - the reading time — the time the post's page was visible, sent when it is hidden or left, and only with the
//   «Аналитические» consent (ADR-0066): it serves the author's statistics, not anything shown on the site.
// Nothing is stored in the browser and no cookie is set.
import { onBeforeUnmount, onMounted, watch, type Ref } from 'vue'
import { maxImpressions, maxReadSeconds, sendViewReport } from '../api/views'
import { hasCookieConsent, loadCookieConsent } from './useCookieConsent'

/** How long half of a card stays on screen before it counts as seen. */
export const impressionDelayMs = 1000

/** How long seen cards wait to be sent together. */
export const impressionFlushMs = 3000

const seen = new Set<string>()
const pending = new Set<string>()
let flushTimer: ReturnType<typeof setTimeout> | null = null
let pageHideHooked = false

/** Sends the cards seen and not sent yet, at most 50 per report. */
export function flushImpressions(): void {
  if (flushTimer) clearTimeout(flushTimer)
  flushTimer = null
  const ids = [...pending]
  pending.clear()
  for (let i = 0; i < ids.length; i += maxImpressions)
    sendViewReport({ kind: 'impressions', postIds: ids.slice(i, i + maxImpressions) })
}

function hookPageHide(): void {
  if (pageHideHooked || typeof window === 'undefined') return
  pageHideHooked = true
  window.addEventListener('pagehide', flushImpressions)
}

/** Counts the card of `postId` as seen in this page's life; a card seen again is not sent again. */
export function markImpression(postId: string): void {
  if (seen.has(postId)) return
  seen.add(postId)
  pending.add(postId)
  hookPageHide()
  flushTimer ??= setTimeout(flushImpressions, impressionFlushMs)
}

/** For tests: forgets the cards seen. */
export function resetImpressions(): void {
  if (flushTimer) clearTimeout(flushTimer)
  flushTimer = null
  seen.clear()
  pending.clear()
}

/** Reports the card in `element` once half of it has stayed on screen for a second. */
export function useCardImpression(element: Ref<HTMLElement | null>, postId: () => string): void {
  let observer: IntersectionObserver | null = null
  let timer: ReturnType<typeof setTimeout> | null = null
  const stop = () => {
    if (timer) clearTimeout(timer)
    timer = null
    observer?.disconnect()
    observer = null
  }
  onMounted(() => {
    if (!element.value || typeof IntersectionObserver === 'undefined' || seen.has(postId())) return
    observer = new IntersectionObserver(
      (entries) => {
        const visible = entries.some((entry) => entry.isIntersecting && entry.intersectionRatio >= 0.5)
        if (visible && !timer) {
          timer = setTimeout(() => {
            markImpression(postId())
            stop()
          }, impressionDelayMs)
        } else if (!visible && timer) {
          clearTimeout(timer)
          timer = null
        }
      },
      { threshold: [0, 0.5] },
    )
    observer.observe(element.value)
  })
  onBeforeUnmount(stop)
}

/**
 * Reports the opening of the published post `postId` (null for a draft or while loading) and, with consent, how long
 * its page was visible.
 */
export function usePostViews(postId: () => string | null): void {
  let current: string | null = null
  let visibleMs = 0
  let visibleSince: number | null = null
  let sentSeconds = 0

  const visible = () => typeof document !== 'undefined' && document.visibilityState === 'visible'
  const pause = () => {
    if (visibleSince !== null) visibleMs += Date.now() - visibleSince
    visibleSince = null
  }
  const reportReading = () => {
    pause()
    const seconds = Math.min(maxReadSeconds, Math.floor(visibleMs / 1000))
    if (!current || seconds < 1 || seconds <= sentSeconds) return
    loadCookieConsent()
    if (!hasCookieConsent('analytics')) return
    sentSeconds = seconds
    sendViewReport({ kind: 'reading', postId: current, seconds })
  }
  const open = (id: string | null) => {
    if (current) reportReading()
    current = id
    visibleMs = 0
    sentSeconds = 0
    visibleSince = id && visible() ? Date.now() : null
    if (id) sendViewReport({ kind: 'open', postId: id })
  }
  const onVisibility = () => {
    if (visible()) {
      if (current && visibleSince === null) visibleSince = Date.now()
    } else {
      reportReading()
    }
  }

  onMounted(() => {
    document.addEventListener('visibilitychange', onVisibility)
    window.addEventListener('pagehide', reportReading)
    watch(postId, (id) => id !== current && open(id), { immediate: true })
  })
  onBeforeUnmount(() => {
    if (typeof document === 'undefined') return
    document.removeEventListener('visibilitychange', onVisibility)
    window.removeEventListener('pagehide', reportReading)
    reportReading()
  })
}
