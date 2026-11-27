import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import { nextTick } from 'vue'
import ShellLayout from '../../components/shell/ShellLayout.vue'
import { headerHidden, headerRevealTop, headerScrollSlack } from '../../composables/useHideOnScroll'
import { global } from './fixtures'

// The header hides while the page is scrolled down and comes back when it is scrolled up (E08-14, ADR-0111). Only the
// phone shell acts on it (the styles below 925 px); the phone and desktop screens against the site are in
// e2e/feed-gestures.spec.ts.
describe('headerHidden', () => {
  it('hides the header for a scroll down and shows it for a scroll up', () => {
    expect(headerHidden(false, 200, 300)).toBe(true)
    expect(headerHidden(true, 300, 200)).toBe(false)
  })

  it('keeps the header as it is for a movement within the slack', () => {
    expect(headerHidden(false, 200, 200 + headerScrollSlack)).toBe(false)
    expect(headerHidden(true, 200, 200 - headerScrollSlack)).toBe(true)
  })

  it('always shows the header near the top of the page', () => {
    expect(headerHidden(true, 0, headerRevealTop)).toBe(false)
    expect(headerHidden(false, 0, headerRevealTop + headerScrollSlack + 1)).toBe(true)
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

async function scrollTo(y: number) {
  setScrollY(y)
  window.dispatchEvent(new Event('scroll'))
  await nextTick()
}

describe('ShellLayout header on scroll', () => {
  it('marks the shell while the header is hidden', async () => {
    wrapper = mount(ShellLayout, {
      props: { currentPath: '/new' },
      slots: { default: '<h1>Экран</h1>' },
      global,
      attachTo: document.body,
    })
    const shell = wrapper.get('[data-testid="shell"]')
    expect(shell.classes()).not.toContain('shell--header-hidden')

    await scrollTo(400)
    expect(shell.classes()).toContain('shell--header-hidden')
    // A slow scroll up adds up past the slack.
    await scrollTo(396)
    expect(shell.classes()).toContain('shell--header-hidden')
    await scrollTo(390)
    expect(shell.classes()).not.toContain('shell--header-hidden')

    await scrollTo(800)
    expect(shell.classes()).toContain('shell--header-hidden')
    await scrollTo(0)
    expect(shell.classes()).not.toContain('shell--header-hidden')
  })
})
