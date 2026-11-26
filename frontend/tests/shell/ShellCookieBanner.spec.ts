// E02-13 (#66, ADR-0066): the cookie banner asks until a choice, and «Разрешить все» and «Только необходимые» record
// it.
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import ShellCookieBanner from '../../components/shell/ShellCookieBanner.vue'
import { resetCookieConsentForTests } from '../../composables/useCookieConsent'
import { readCookie } from '../../utils/cookie-consent'
import { global } from './fixtures'

let wrapper: ReturnType<typeof mount> | undefined
function clearConsent() {
  document.cookie = 'cookie_consent=; path=/; max-age=0'
}
beforeEach(() => {
  resetCookieConsentForTests()
  clearConsent()
})
afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
  clearConsent()
})

async function mountBanner() {
  wrapper = mount(ShellCookieBanner, { global })
  await flushPromises()
  return wrapper
}

describe('ShellCookieBanner', () => {
  it('is a named region with two equal choices and a link to the settings', async () => {
    const banner = await mountBanner()
    const region = banner.get('section')
    expect(region.attributes('aria-label')).toBe('Согласие на cookie')
    const buttons = banner.findAll('button')
    expect(buttons.map((button) => button.text())).toEqual(['Только необходимые', 'Разрешить все'])
    expect(new Set(buttons.map((button) => button.classes().join(' '))).size).toBe(1)
    expect(banner.get('a').attributes('href')).toBe('/settings/cookies')
    expect(banner.get('a').text()).toBe('Настроить')
  })

  it.each([
    ['Только необходимые', 'v1.analytics-0'],
    ['Разрешить все', 'v1.analytics-1'],
  ])('«%s» records the choice and hides the banner', async (label, value) => {
    const banner = await mountBanner()
    await banner
      .findAll('button')
      .find((button) => button.text() === label)!
      .trigger('click')
    expect(readCookie(document.cookie, 'cookie_consent')).toBe(value)
    expect(banner.find('section').exists()).toBe(false)
  })

  it('does not show once a choice is saved', async () => {
    document.cookie = 'cookie_consent=v1.analytics-0; path=/'
    const banner = await mountBanner()
    expect(banner.find('section').exists()).toBe(false)
  })
})
