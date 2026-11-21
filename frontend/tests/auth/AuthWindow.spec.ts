// E03-04 (#30): the sign-in window — a modal on the desktop, a sheet over the whole screen on a phone, the sign-in
// and the registration forms with a link between them, `signed-in` once the provider has signed the person in.
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import AuthWindow from '../../components/auth/AuthWindow.vue'
import type { RegistrationApi } from '../../api/registration'
import type { SignInApi } from '../../api/sign-in'

const id = '7c9e6679-7425-40de-944b-e07fc1f90ae7'

function signInApi(): SignInApi {
  return {
    withPassword: vi.fn(async () => ({ status: 204 })),
    requestCode: vi.fn(async () => ({ status: 202, body: { challengeId: id, resendAfterSeconds: 60 } })),
    withCode: vi.fn(async () => ({ status: 204 })),
  }
}

function registrationApi(): RegistrationApi {
  return {
    register: vi.fn(async () => ({ status: 202, body: { registrationId: id, resendAfterSeconds: 60 } })),
    resend: vi.fn(async () => ({ status: 202, body: { registrationId: id, resendAfterSeconds: 60 } })),
    confirm: vi.fn(async () => ({ status: 204 })),
  }
}

const phone = (matches: boolean) =>
  vi.spyOn(window, 'matchMedia').mockImplementation(
    (query) =>
      ({
        matches,
        media: query,
        addEventListener: () => {},
        removeEventListener: () => {},
      }) as unknown as MediaQueryList,
  )

let wrapper: ReturnType<typeof mount> | undefined

function mountWindow(mode: 'sign-in' | 'registration' = 'sign-in') {
  wrapper = mount(AuthWindow, {
    props: {
      open: true,
      mode,
      signInApi: signInApi(),
      registrationApi: registrationApi(),
      'onUpdate:open': (value: boolean) => wrapper!.setProps({ open: value }),
      'onUpdate:mode': (value: 'sign-in' | 'registration') => wrapper!.setProps({ mode: value }),
    },
    attachTo: document.body,
    global: { stubs: { teleport: true, NuxtLink: { template: '<a><slot /></a>' } } },
  })
  return wrapper
}

const button = (text: string) => wrapper!.findAll('button').find((b) => b.text() === text)!

afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
  vi.restoreAllMocks()
})

describe('AuthWindow', () => {
  it('is a modal dialog "Вход" on the desktop with the form without a heading of its own', async () => {
    phone(false)
    mountWindow()
    await flushPromises()
    const dialog = wrapper!.get('[role="dialog"]')
    expect(dialog.attributes('aria-modal')).toBe('true')
    expect(wrapper!.get(`[id="${dialog.attributes('aria-labelledby')}"]`).text()).toBe('Вход')
    expect(wrapper!.get('[data-presentation]').attributes('data-presentation')).toBe('modal')
    expect(wrapper!.find('.ui-dialog--fullscreen').exists()).toBe(false)
    expect(wrapper!.findAll('h1')).toHaveLength(0)
    expect(wrapper!.find('[data-testid="sign-in"]').exists()).toBe(true)
  })

  it('fills the whole screen on a phone, without the handle of a bottom sheet', async () => {
    phone(true)
    mountWindow()
    await flushPromises()
    expect(wrapper!.get('[data-presentation]').attributes('data-presentation')).toBe('sheet')
    expect(wrapper!.find('.ui-dialog--fullscreen').exists()).toBe(true)
    expect(wrapper!.find('.ui-dialog__handle').exists()).toBe(false)
  })

  it('switches between the sign-in and the registration', async () => {
    phone(false)
    mountWindow()
    await button('Зарегистрироваться').trigger('click')
    await flushPromises()
    expect(wrapper!.find('[data-testid="registration"]').exists()).toBe(true)
    expect(wrapper!.get('.ui-dialog__title').text()).toBe('Регистрация')
    await button('Войти').trigger('click')
    await flushPromises()
    expect(wrapper!.find('[data-testid="sign-in"]').exists()).toBe(true)
    expect(wrapper!.get('.ui-dialog__title').text()).toBe('Вход')
  })

  it('emits `signed-in` after the sign-in by password', async () => {
    phone(false)
    mountWindow()
    await wrapper!.get('input[name="email"]').setValue('anna@example.com')
    await wrapper!.get('input[name="password"]').setValue('secret-1')
    await wrapper!.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper!.emitted('signed-in')).toHaveLength(1)
  })

  it('emits `signed-in` once the registration code is accepted', async () => {
    phone(false)
    mountWindow('registration')
    await wrapper!.get('input[name="email"]').setValue('anna@example.com')
    await wrapper!.get('input[name="password"]').setValue('secret-1')
    await wrapper!.get('form').trigger('submit')
    await flushPromises()
    await wrapper!.get('input[name="code"]').setValue('123456')
    await wrapper!.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper!.emitted('signed-in')).toHaveLength(1)
  })

  it('closes with the close button', async () => {
    phone(false)
    mountWindow()
    await wrapper!.get('button[aria-label="Закрыть"]').trigger('click')
    await flushPromises()
    expect(wrapper!.emitted('update:open')).toEqual([[false]])
    expect(wrapper!.find('[role="dialog"]').exists()).toBe(false)
  })
})
