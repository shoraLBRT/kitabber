import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import SignInForm from '../../components/auth/SignInForm.vue'
import { afterSignIn, type SignInApi, type SignInOutcome } from '../../api/sign-in'

const id = '7c9e6679-7425-40de-944b-e07fc1f90ae7'

function fakeApi(overrides: Partial<SignInApi> = {}): SignInApi {
  return {
    withPassword: vi.fn(async () => ({ status: 204 })),
    requestCode: vi.fn(async () => ({ status: 202, body: { challengeId: id, resendAfterSeconds: 60 } })),
    withCode: vi.fn(async () => ({ status: 204 })),
    ...overrides,
  }
}

const stubs = { NuxtLink: { template: '<a><slot /></a>' } }

function mountForm(api: SignInApi) {
  return mount(SignInForm, { props: { api }, global: { stubs } })
}

function button(wrapper: ReturnType<typeof mount>, text: string) {
  return wrapper.findAll('button').find((b) => b.text() === text)!
}

async function toCodeStep(wrapper: ReturnType<typeof mount>, email = 'anna@example.com') {
  await button(wrapper, 'Войти по коду из письма').trigger('click')
  await wrapper.get('input[name="email"]').setValue(email)
  await wrapper.get('form').trigger('submit')
  await flushPromises()
}

describe('SignInForm', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  it('starts empty: the submit button waits for an email and a password', async () => {
    const wrapper = mountForm(fakeApi())
    expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeDefined()
    await wrapper.get('input[name="email"]').setValue('anna@example.com')
    await wrapper.get('input[name="password"]').setValue('secret-1')
    expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeUndefined()
    expect(wrapper.get('input[name="password"]').attributes('autocomplete')).toBe('current-password')
  })

  it('shows loading while the password is checked, then signs in', async () => {
    let finish: (outcome: SignInOutcome) => void = () => {}
    const api = fakeApi({
      withPassword: vi.fn(() => new Promise<SignInOutcome>((resolve) => (finish = resolve))),
    })
    const wrapper = mountForm(api)
    await wrapper.get('input[name="email"]').setValue(' anna@example.com ')
    await wrapper.get('input[name="password"]').setValue('secret-1')
    await wrapper.get('form').trigger('submit')
    expect(wrapper.get('button[type="submit"]').attributes('aria-busy')).toBe('true')
    expect(api.withPassword).toHaveBeenCalledWith('anna@example.com', 'secret-1')
    finish({ status: 204 })
    await flushPromises()
    expect(wrapper.emitted('signed-in')).toHaveLength(1)
  })

  it('answers a refused password without telling whether the address exists', async () => {
    const wrapper = mountForm(fakeApi({ withPassword: vi.fn(async () => ({ status: 400 })) }))
    await wrapper.get('input[name="email"]').setValue('anna@example.com')
    await wrapper.get('input[name="password"]').setValue('wrong')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    const password = wrapper.get('input[name="password"]')
    expect(password.attributes('aria-invalid')).toBe('true')
    expect(wrapper.get(`[id="${password.attributes('aria-describedby')}"]`).text()).toBe(
      'Неверная почта или пароль.',
    )
    expect(wrapper.find('[role="alert"]').exists()).toBe(false)
    expect(wrapper.emitted('signed-in')).toBeUndefined()
  })

  it('reports too many attempts and an unreachable server', async () => {
    const withPassword = vi
      .fn<SignInApi['withPassword']>()
      .mockResolvedValueOnce({ status: 429, retryAfter: 125 })
      .mockResolvedValueOnce({ status: 0 })
    const wrapper = mountForm(fakeApi({ withPassword }))
    await wrapper.get('input[name="email"]').setValue('anna@example.com')
    await wrapper.get('input[name="password"]').setValue('secret-1')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('через 2:05')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('Не удалось связаться с сервером')
  })

  it('signs in with a code: the timer, a wrong code, a new code, then the right one', async () => {
    const withCode = vi
      .fn<SignInApi['withCode']>()
      .mockResolvedValueOnce({ status: 400 })
      .mockResolvedValueOnce({ status: 204 })
    const api = fakeApi({ withCode })
    const wrapper = mountForm(api)
    await toCodeStep(wrapper)
    expect(api.requestCode).toHaveBeenCalledWith('anna@example.com')
    expect(wrapper.text()).toContain('Отправили код на anna@example.com')
    expect(wrapper.get('[data-testid="resend-timer"]').text()).toContain('1:00')

    await wrapper.get('input[name="code"]').setValue('123456')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.text()).toContain('Неверный или просроченный код')

    vi.advanceTimersByTime(60_000)
    await flushPromises()
    await button(wrapper, 'Отправить код снова').trigger('click')
    await flushPromises()
    expect(api.requestCode).toHaveBeenCalledTimes(2)
    expect(wrapper.get('[role="status"]').text()).toContain('Отправили новый код')

    await wrapper.get('input[name="code"]').setValue('654321')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(withCode).toHaveBeenLastCalledWith(id, '654321')
    expect(wrapper.emitted('signed-in')).toHaveLength(1)
  })

  it('shows the validation error of the address and the resend timer of the API', async () => {
    const requestCode = vi
      .fn<SignInApi['requestCode']>()
      .mockResolvedValueOnce({ status: 400 })
      .mockResolvedValueOnce({ status: 429, retryAfter: 42 })
    const wrapper = mountForm(fakeApi({ requestCode }))
    await toCodeStep(wrapper, 'not-an-email')
    expect(wrapper.text()).toContain('Это не похоже на адрес почты.')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('через 0:42')
  })
})

describe('afterSignIn', () => {
  it('goes back into the authorization request of the provider, and nowhere else', () => {
    expect(afterSignIn('/connect/authorize?client_id=admin&response_type=code')).toBe(
      '/connect/authorize?client_id=admin&response_type=code',
    )
    expect(afterSignIn('https://evil.example/connect/authorize')).toBe('/auth/sign-in?returnTo=/')
    expect(afterSignIn('//evil.example/connect/authorize')).toBe('/auth/sign-in?returnTo=/')
    expect(afterSignIn(undefined)).toBe('/auth/sign-in?returnTo=/')
  })
})
