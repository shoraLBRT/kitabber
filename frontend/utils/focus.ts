// Keyboard focus helpers for the UI components (E02-04, ADR-0013).

const selector = [
  'a[href]',
  'button:not([disabled])',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
  '[contenteditable="true"]',
].join(',')

/** Elements inside the container that Tab reaches, in document order. */
export function focusableIn(container: HTMLElement): HTMLElement[] {
  return [...container.querySelectorAll<HTMLElement>(selector)].filter(
    (el) => !el.closest('[hidden], [inert]') && el.tabIndex >= 0,
  )
}

/** Keeps Tab and Shift+Tab inside the container (modal dialog focus trap). */
export function trapTab(event: KeyboardEvent, container: HTMLElement): void {
  if (event.key !== 'Tab') return
  const items = focusableIn(container)
  if (items.length === 0) {
    event.preventDefault()
    container.focus()
    return
  }
  const first = items[0]!
  const last = items[items.length - 1]!
  const active = document.activeElement
  if (event.shiftKey && (active === first || !container.contains(active))) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && (active === last || !container.contains(active))) {
    event.preventDefault()
    first.focus()
  }
}

/** Index of the next enabled item in a roving list, wrapping around; -1 when every item is disabled. */
export function nextIndex(
  from: number,
  step: 1 | -1,
  count: number,
  disabled: (index: number) => boolean,
): number {
  for (let i = 1; i <= count; i++) {
    const index = (((from + step * i) % count) + count) % count
    if (!disabled(index)) return index
  }
  return -1
}
