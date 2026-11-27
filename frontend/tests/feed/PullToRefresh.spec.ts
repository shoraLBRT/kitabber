import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import PullToRefresh from '../../components/feed/PullToRefresh.vue'
import { pullDistance, pullLimit, pullThreshold } from '../../utils/pull'

// Pull to refresh (E08-14, ADR-0111): how far the mark opens, when letting go reads the feed again, and the browser's
// own pull turned off while the feed is shown. The pages against the fake API, by a real touch, are in
// e2e/feed-gestures.spec.ts.
describe('pullDistance', () => {
  it('follows the finger at half its speed', () => {
    expect(pullDistance(40)).toBe(20)
    expect(pullDistance(2 * pullThreshold)).toBe(pullThreshold)
  })

  it('stops at the limit and opens nothing for a finger moved up', () => {
    expect(pullDistance(1000)).toBe(pullLimit)
    expect(pullDistance(-50)).toBe(0)
  })
})

// happy-dom gives `scrollY` a getter only.
const setScrollY = (value: number) => Object.defineProperty(window, 'scrollY', { value, configurable: true })

let wrapper: ReturnType<typeof mount> | undefined
afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
  setScrollY(0)
})

const touch = (clientY: number) => ({ touches: [{ clientY }] })

function mountPull(refresh: () => Promise<unknown>) {
  wrapper = mount(PullToRefresh, {
    props: { refresh },
    slots: { default: '<ul data-testid="feed-list"></ul>' },
    attachTo: document.body,
  })
  return wrapper
}

async function pull(w: ReturnType<typeof mount>, from: number, to: number) {
  const root = w.get('[data-testid="pull-refresh"]')
  await root.trigger('touchstart', touch(from))
  await root.trigger('touchmove', touch(to))
  return root
}

describe('PullToRefresh', () => {
  it('reads the feed again when let go past the threshold and shows that it is refreshing', async () => {
    let done: () => void = () => {}
    const refresh = vi.fn(() => new Promise<void>((resolve) => (done = resolve)))
    const w = mountPull(refresh)
    const mark = () => w.get('[data-testid="pull-refresh-mark"]')

    const root = await pull(w, 100, 100 + 2 * pullThreshold + 10)
    expect(mark().attributes('data-state')).toBe('ready')
    expect(mark().text()).toBe('Отпустите, чтобы обновить')

    await root.trigger('touchend')
    expect(refresh).toHaveBeenCalledOnce()
    expect(mark().attributes('data-state')).toBe('refreshing')
    expect(w.get('[role="status"]').text()).toBe('Обновляем ленту…')

    // A second pull while the feed is being read asks for nothing more.
    await pull(w, 100, 400)
    await root.trigger('touchend')
    expect(refresh).toHaveBeenCalledOnce()

    done()
    await flushPromises()
    expect(mark().attributes('data-state')).toBe('idle')
    expect(mark().attributes('style')).toContain('height: 0px')
    expect(w.get('[role="status"]').text()).toBe('')
  })

  it('does nothing for a short pull', async () => {
    const refresh = vi.fn(() => Promise.resolve())
    const w = mountPull(refresh)
    const root = await pull(w, 100, 140)
    expect(w.get('[data-testid="pull-refresh-mark"]').attributes('data-state')).toBe('pulling')
    expect(w.get('[data-testid="pull-refresh-mark"]').text()).toBe('Потяните, чтобы обновить')
    await root.trigger('touchend')
    expect(refresh).not.toHaveBeenCalled()
    expect(w.get('[data-testid="pull-refresh-mark"]').attributes('data-state')).toBe('idle')
  })

  it('never pulls a page scrolled down', async () => {
    const refresh = vi.fn(() => Promise.resolve())
    setScrollY(300)
    const w = mountPull(refresh)
    const root = await pull(w, 100, 400)
    await root.trigger('touchend')
    expect(refresh).not.toHaveBeenCalled()
    expect(w.get('[data-testid="pull-refresh-mark"]').attributes('data-state')).toBe('idle')
  })

  it('puts the mark away when the refresh fails; the feed shows its own failure', async () => {
    const refresh = vi.fn(() => Promise.reject(new Error('offline')))
    const w = mountPull(refresh)
    const root = await pull(w, 0, 400)
    await root.trigger('touchend')
    await flushPromises()
    expect(refresh).toHaveBeenCalledOnce()
    expect(w.get('[data-testid="pull-refresh-mark"]').attributes('data-state')).toBe('idle')
  })

  it("turns the browser's own pull off while it is shown and gives it back after", () => {
    const w = mountPull(() => Promise.resolve())
    expect(document.documentElement.style.overscrollBehaviorY).toBe('contain')
    w.unmount()
    wrapper = undefined
    expect(document.documentElement.style.overscrollBehaviorY).toBe('')
  })
})
