// The header hides while the page is scrolled down and comes back as soon as it is scrolled up (E08-14, FR-FEED,
// docs/spec.md section 5 «Мобильная версия», ADR-0111). The state is kept on every screen; only the phone shell (below
// 925 px) acts on it, in ShellLayout's styles, so a desktop header never moves. Near the top of the page the header is
// always shown, and a small movement either way (a finger resting on the screen) changes nothing.
import { onBeforeUnmount, onMounted, ref } from 'vue'

/** Within this distance of the top the header is always shown, in pixels. */
export const headerRevealTop = 64
/** A movement shorter than this keeps the header as it is, in pixels. */
export const headerScrollSlack = 8

/** Whether the header is hidden after the page moved from `from` to `to` (scroll offsets). */
export function headerHidden(hidden: boolean, from: number, to: number): boolean {
  if (to <= headerRevealTop) return false
  if (to - from > headerScrollSlack) return true
  if (from - to > headerScrollSlack) return false
  return hidden
}

export function useHideOnScroll() {
  const hidden = ref(false)
  let last = 0

  function onScroll() {
    const y = Math.max(0, window.scrollY)
    const next = headerHidden(hidden.value, last, y)
    // The offset moves on only once it has counted, so a slow scroll still adds up past the slack.
    if (next !== hidden.value || Math.abs(y - last) > headerScrollSlack || y <= headerRevealTop) last = y
    hidden.value = next
  }

  /** Shows the header again: the keyboard's focus came into it, or a page asked for it. */
  function reveal() {
    hidden.value = false
    last = Math.max(0, window.scrollY)
  }

  onMounted(() => {
    last = Math.max(0, window.scrollY)
    window.addEventListener('scroll', onScroll, { passive: true })
  })
  onBeforeUnmount(() => window.removeEventListener('scroll', onScroll))

  return { hidden, reveal }
}
