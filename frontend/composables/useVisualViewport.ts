// The part of the window the reader sees (E05-10, ADR-0047): on a phone the on-screen keyboard covers the bottom of
// the window without making it smaller (iOS Safari, Android Chrome by default), and only the visual viewport shrinks.
// A screen that must stay above the keyboard takes its box from here. Null on the server, until mounted, without
// window.visualViewport and while the page is zoomed in by a pinch (then the box is the zoomed part, not the screen).
import { onBeforeUnmount, onMounted, ref, type Ref } from 'vue'

export interface ViewportBox {
  /** Pixels from the top of the window to the top of what is seen. */
  top: number
  height: number
  /** The keyboard (or another panel of the system) covers the bottom of the window. */
  keyboard: boolean
}

/** Less than this is the address bar of the browser hiding and showing, not a keyboard. */
export const keyboardMinHeight = 120

export function useVisualViewport(): Ref<ViewportBox | null> {
  const box = ref<ViewportBox | null>(null)
  let viewport: VisualViewport | null = null

  const update = () => {
    if (!viewport || viewport.scale > 1.01) {
      box.value = null
      return
    }
    const top = Math.max(0, viewport.offsetTop)
    const height = viewport.height
    const next = { top, height, keyboard: window.innerHeight - height >= keyboardMinHeight }
    const current = box.value
    if (current && current.top === next.top && current.height === next.height) return
    box.value = next
  }

  onMounted(() => {
    viewport = window.visualViewport ?? null
    if (!viewport) return
    update()
    viewport.addEventListener('resize', update)
    viewport.addEventListener('scroll', update)
  })

  onBeforeUnmount(() => {
    viewport?.removeEventListener('resize', update)
    viewport?.removeEventListener('scroll', update)
    viewport = null
  })

  return box
}
