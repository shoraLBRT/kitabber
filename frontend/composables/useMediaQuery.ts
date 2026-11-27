// Reactive CSS media query (E02-04, ADR-0013). False on the server and until the component is mounted.
import { onBeforeUnmount, onMounted, ref, type Ref } from 'vue'
import { breakpoints } from '../../brand/tokens'

/** Phone layout of docs/spec.md, section 5: narrower than the first breakpoint (640 px). */
export const phoneQuery = `(max-width: ${breakpoints.sm - 1}px)`

/** The desktop shell shows its right column (E02-05): from the third breakpoint (1240 px). */
export const asideQuery = `(min-width: ${breakpoints.lg}px)`

export function useMediaQuery(query: string): Ref<boolean> {
  const matches = ref(false)
  let list: MediaQueryList | undefined
  const update = () => {
    matches.value = list?.matches ?? false
  }
  onMounted(() => {
    if (typeof window.matchMedia !== 'function') return
    list = window.matchMedia(query)
    update()
    list.addEventListener('change', update)
  })
  onBeforeUnmount(() => list?.removeEventListener('change', update))
  return matches
}
