import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import UiToaster from '../../components/ui/UiToaster.vue'
import { toastDurations, useToast } from '../../composables/useToast'

const toast = useToast()
let wrapper: ReturnType<typeof mount>
const texts = (testid: string) => wrapper.findAll(`[data-testid="${testid}"] li`).map((li) => li.text())

beforeEach(() => {
  vi.useFakeTimers()
  wrapper = mount(UiToaster, { attachTo: document.body })
})

afterEach(() => {
  wrapper.unmount()
  toast.clear()
  vi.useRealTimers()
})

describe('UiToast and UiToaster', () => {
  it('announces errors assertively and the rest politely', async () => {
    expect(wrapper.get('[data-testid="toasts-polite"]').attributes('aria-live')).toBe('polite')
    expect(wrapper.get('[data-testid="toasts-assertive"]').attributes('role')).toBe('alert')
    toast.show('Ссылка скопирована')
    toast.show('Нет сети', { tone: 'error' })
    await flushPromises()
    expect(texts('toasts-polite')).toEqual(['Ссылка скопирована'])
    expect(texts('toasts-assertive')).toEqual(['Нет сети'])
  })

  it('hides a toast after its duration', async () => {
    toast.show('Пост опубликован', { tone: 'success' })
    await flushPromises()
    vi.advanceTimersByTime(toastDurations.success - 1)
    await flushPromises()
    expect(texts('toasts-polite')).toHaveLength(1)
    vi.advanceTimersByTime(1)
    await flushPromises()
    expect(texts('toasts-polite')).toHaveLength(0)
  })

  it('keeps an error longer than other toasts', () => {
    expect(toastDurations.error).toBeGreaterThan(toastDurations.info)
  })

  it('pauses while hovered or focused and resumes after', async () => {
    toast.show('Сохранено', { duration: 1000 })
    await flushPromises()
    const item = wrapper.get('li')
    await item.trigger('mouseenter')
    vi.advanceTimersByTime(5000)
    await flushPromises()
    expect(texts('toasts-polite')).toHaveLength(1)
    await item.trigger('mouseleave')
    vi.advanceTimersByTime(1000)
    await flushPromises()
    expect(texts('toasts-polite')).toHaveLength(0)
  })

  it('stays with duration 0 until closed with its button', async () => {
    toast.show('Важное', { duration: 0 })
    await flushPromises()
    vi.advanceTimersByTime(60_000)
    await flushPromises()
    const close = wrapper.get('li button')
    expect(close.attributes('aria-label')).toBe('Закрыть уведомление')
    await close.trigger('click')
    expect(texts('toasts-polite')).toHaveLength(0)
  })
})
