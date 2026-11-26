// E02-13 (#66, ADR-0066): the cookie settings show the saved choice and change it.
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import CookieSettingsForm from '../../components/settings/CookieSettingsForm.vue'
import { hasCookieConsent, resetCookieConsentForTests } from '../../composables/useCookieConsent'
import { useToast } from '../../composables/useToast'
import { readCookie } from '../../utils/cookie-consent'

let wrapper: ReturnType<typeof mount> | undefined
function clearConsent() {
  document.cookie = 'cookie_consent=; path=/; max-age=0'
}
beforeEach(() => {
  resetCookieConsentForTests()
  clearConsent()
  useToast().clear()
})
afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
  clearConsent()
})

async function mountForm() {
  wrapper = mount(CookieSettingsForm)
  await flushPromises()
  return wrapper
}

const checkboxes = () => wrapper!.findAll<HTMLInputElement>('input[type="checkbox"]')

describe('CookieSettingsForm', () => {
  it('keeps the necessary cookies on and says nothing is chosen yet', async () => {
    const form = await mountForm()
    expect(form.get('legend').text()).toBe('Категории cookie')
    const [necessary, analytics] = checkboxes()
    expect(necessary!.element.checked).toBe(true)
    expect(necessary!.element.disabled).toBe(true)
    expect(analytics!.element.checked).toBe(false)
    expect(form.text()).toContain('Вы ещё не сделали выбор')
  })

  it('shows the saved choice', async () => {
    document.cookie = 'cookie_consent=v1.analytics-1; path=/'
    const form = await mountForm()
    expect(checkboxes()[1]!.element.checked).toBe(true)
    expect(form.text()).not.toContain('Вы ещё не сделали выбор')
  })

  it('saves a changed choice', async () => {
    document.cookie = 'cookie_consent=v1.analytics-1; path=/'
    const form = await mountForm()
    await checkboxes()[1]!.setValue(false)
    await form.get('form').trigger('submit')
    expect(readCookie(document.cookie, 'cookie_consent')).toBe('v1.analytics-0')
    expect(hasCookieConsent('analytics')).toBe(false)
    expect(useToast().toasts.map((toast) => toast.message)).toEqual(['Выбор сохранён'])
  })
})
