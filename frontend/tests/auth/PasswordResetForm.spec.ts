import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import PasswordResetForm from '../../components/auth/PasswordResetForm.vue'
import SignInForm from '../../components/auth/SignInForm.vue'
import type { PasswordOutcome, PasswordResetApi } from '../../api/password'
import type { SignInApi } from '../../api/sign-in'

const id = '0b6b2a4e-4f8c-4d65-9f7b-2f1f0e3c9a11'

function fakeApi(overrides: Partial<PasswordResetApi> = {}): PasswordResetApi {
  return {
    request: vi.fn(async () => ({ status: 202, body: { resetId: id, resendAfterSeconds: 60 } })),
    confirm: vi.fn(async () => ({ status: 204 })),
    ...overrides,
  }
}

function button(wrapper: ReturnType<typeof mount>, text: string) {
  return wrapper.findAll('button').find((b) => b.text() === text)!
}

async function toCodeStep(wrapper: ReturnType<typeof mount>, email = 'anna@example.com') {
  await wrapper.get('input[name="email"]').setValue(email)
  await wrapper.get('form').trigger('submit')
  await flushPromises()
}

async function enter(wrapper: ReturnType<typeof mount>, code: string, password: string) {
  await wrapper.get('input[name="code"]').setValue(code)
  await wrapper.get('input[name="new-password"]').setValue(password)
  await wrapper.get('form').trigger('submit')
  await flushPromises()
}

describe('PasswordResetForm', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  it('starts with the email from the sign-in form and waits for an address', async () => {
    const empty = mount(PasswordResetForm, { props: { api: fakeApi() } })
    expect(empty.get('button[type="submit"]').attributes('disabled')).toBeDefined()

    const prefilled = mount(PasswordResetForm, { props: { api: fakeApi(), email: 'anna@example.com' } })
    expect((prefilled.get('input[name="email"]').element as HTMLInputElement).value).toBe('anna@example.com')
    expect(prefilled.get('button[type="submit"]').attributes('disabled')).toBeUndefined()
  })

  it('asks for the code and the new password, then signs in', async () => {
    const api = fakeApi()
    const wrapper = mount(PasswordResetForm, { props: { api } })
    await toCodeStep(wrapper)

    expect(api.request).toHaveBeenCalledWith('anna@example.com')
    expect(wrapper.text()).toContain('Если у anna@example.com есть аккаунт')
    expect(wrapper.get('[data-testid="resend-timer"]').text()).toContain('1:00')
    expect(wrapper.get('input[name="new-password"]').attributes('autocomplete')).toBe('new-password')

    await enter(wrapper, '482913', 'brand-new-2')
    expect(api.confirm).toHaveBeenCalledWith(id, '482913', 'brand-new-2')
    expect(wrapper.emitted('signed-in')).toHaveLength(1)
  })

  it('shows loading while the new password is saved', async () => {
    let finish: (outcome: PasswordOutcome) => void = () => {}
    const api = fakeApi({
      confirm: vi.fn(() => new Promise<PasswordOutcome>((resolve) => (finish = resolve))),
    })
    const wrapper = mount(PasswordResetForm, { props: { api } })
    await toCodeStep(wrapper)
    await wrapper.get('input[name="code"]').setValue('482913')
    await wrapper.get('input[name="new-password"]').setValue('brand-new-2')
    await wrapper.get('form').trigger('submit')

    expect(wrapper.get('button[type="submit"]').attributes('aria-busy')).toBe('true')
    finish({ status: 204 })
    await flushPromises()
    expect(wrapper.emitted('signed-in')).toHaveLength(1)
  })

  it('refuses a short password before calling the API', async () => {
    const api = fakeApi()
    const wrapper = mount(PasswordResetForm, { props: { api } })
    await toCodeStep(wrapper)
    await enter(wrapper, '482913', '12345')

    expect(api.confirm).not.toHaveBeenCalled()
    expect(wrapper.get('input[name="new-password"]').attributes('aria-invalid')).toBe('true')
    expect(wrapper.text()).toContain('Не короче 6 символов.')
  })

  it('shows a wrong or expired code at its field, and the API password rule at the password', async () => {
    const confirm = vi
      .fn<PasswordResetApi['confirm']>()
      .mockResolvedValueOnce({ status: 400, body: { code: 'confirmation_invalid' } })
      .mockResolvedValueOnce({
        status: 400,
        body: { code: 'validation_failed', errors: { password: ['x'] } },
      })
    const wrapper = mount(PasswordResetForm, { props: { api: fakeApi({ confirm }) } })
    await toCodeStep(wrapper)

    await enter(wrapper, '000000', 'brand-new-2')
    expect(wrapper.get('input[name="code"]').attributes('aria-invalid')).toBe('true')
    expect(wrapper.text()).toContain('Неверный или просроченный код')

    await enter(wrapper, '482913', '      x')
    expect(wrapper.get('input[name="new-password"]').attributes('aria-invalid')).toBe('true')
    expect(wrapper.emitted('signed-in')).toBeUndefined()
  })

  it('sends a new code once the timer runs out', async () => {
    const request = vi
      .fn<PasswordResetApi['request']>()
      .mockResolvedValue({ status: 202, body: { resetId: id, resendAfterSeconds: 60 } })
    const wrapper = mount(PasswordResetForm, { props: { api: fakeApi({ request }) } })
    await toCodeStep(wrapper)
    expect(button(wrapper, 'Отправить код снова')).toBeUndefined()

    vi.advanceTimersByTime(60_000)
    await flushPromises()
    await button(wrapper, 'Отправить код снова').trigger('click')
    await flushPromises()
    expect(request).toHaveBeenCalledTimes(2)
    expect(wrapper.text()).toContain('Отправили новый код')
  })

  it('shows a bad address, too many tries and an unreachable server', async () => {
    const request = vi
      .fn<PasswordResetApi['request']>()
      .mockResolvedValueOnce({ status: 400 })
      .mockResolvedValueOnce({ status: 429, retryAfter: 90 })
      .mockResolvedValueOnce({ status: 0 })
    const wrapper = mount(PasswordResetForm, { props: { api: fakeApi({ request }) } })

    await toCodeStep(wrapper, 'not-an-email')
    expect(wrapper.text()).toContain('Это не похоже на адрес почты.')
    await toCodeStep(wrapper)
    expect(wrapper.get('[role="alert"]').text()).toContain('1:30')
    await toCodeStep(wrapper)
    expect(wrapper.get('[role="alert"]').text()).toContain('Не удалось связаться с сервером')
  })

  it('goes back to the sign-in', async () => {
    const wrapper = mount(PasswordResetForm, { props: { api: fakeApi() } })
    await button(wrapper, 'Вернуться ко входу').trigger('click')
    expect(wrapper.emitted('back')).toHaveLength(1)
  })
})

describe('SignInForm with the password reset', () => {
  const signInApi: SignInApi = {
    withPassword: vi.fn(async () => ({ status: 400 })),
    requestCode: vi.fn(async () => ({ status: 202 })),
    withCode: vi.fn(async () => ({ status: 204 })),
  }
  const stubs = { NuxtLink: { template: '<a><slot /></a>' } }

  it('offers «Забыли пароль?» only with the reset calls', () => {
    const without = mount(SignInForm, { props: { api: signInApi }, global: { stubs } })
    expect(button(without, 'Забыли пароль?')).toBeUndefined()
  })

  it('turns into the reset with the typed email, signs in after it and comes back', async () => {
    const resetApi = fakeApi()
    const wrapper = mount(SignInForm, { props: { api: signInApi, resetApi }, global: { stubs } })
    await wrapper.get('input[name="email"]').setValue('anna@example.com ')
    await button(wrapper, 'Забыли пароль?').trigger('click')

    expect(wrapper.get('h1').text()).toBe('Восстановление пароля')
    expect((wrapper.get('input[name="email"]').element as HTMLInputElement).value).toBe('anna@example.com')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    await enter(wrapper, '482913', 'brand-new-2')
    expect(wrapper.emitted('signed-in')).toHaveLength(1)

    const again = mount(SignInForm, { props: { api: signInApi, resetApi }, global: { stubs } })
    await button(again, 'Забыли пароль?').trigger('click')
    await button(again, 'Вернуться ко входу').trigger('click')
    expect(again.get('h1').text()).toBe('Вход')
    expect(again.find('input[name="password"]').exists()).toBe(true)
  })
})
