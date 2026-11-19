// Toast notifications (FR-UI, E02-04, ADR-0013): any component calls useToast().show(), UiToaster in app.vue
// renders the queue. Toasts are shown only in the browser, so one module-level queue never mixes server requests.
import { readonly, shallowReactive } from 'vue'

export type ToastTone = 'info' | 'success' | 'error'

export interface Toast {
  id: number
  message: string
  tone: ToastTone
  /** Milliseconds before the toast hides itself; 0 keeps it until closed. */
  duration: number
}

export interface ToastOptions {
  tone?: ToastTone
  duration?: number
}

/** How long a toast stays on screen; an error stays longer so it can be read. */
export const toastDurations: Record<ToastTone, number> = { info: 5000, success: 5000, error: 8000 }

const queue = shallowReactive<Toast[]>([])
let nextId = 1

export function useToast() {
  function show(message: string, options: ToastOptions = {}): number {
    if (import.meta.server) return 0
    const tone = options.tone ?? 'info'
    const toast: Toast = { id: nextId++, message, tone, duration: options.duration ?? toastDurations[tone] }
    queue.push(toast)
    return toast.id
  }

  function dismiss(id: number): void {
    const index = queue.findIndex((toast) => toast.id === id)
    if (index >= 0) queue.splice(index, 1)
  }

  function clear(): void {
    queue.splice(0, queue.length)
  }

  return { toasts: readonly(queue), show, dismiss, clear }
}
