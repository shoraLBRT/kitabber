import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import RegistrationForm from '../../components/auth/RegistrationForm.vue'
import {
  formatCountdown,
  retryAfterSeconds,
  type RegistrationApi,
  type RegistrationOutcome,
} from '../../utils/registration'

const id = '0190f3a2-0000-7000-8000-000000000001'

function fakeApi(overrides: Partial<RegistrationApi> = {}): RegistrationApi {
  return {
    register: vi.fn(async () => ({ status: 202, body: { registrationId: id, resendAfterSeconds: 60 } })),
    resend: vi.fn(async () => ({ status: 202, body: { registrationId: id, resendAfterSeconds: 60 } })),
    confirm: vi.fn(async () => ({ status: 204 })),
    ...overrides,
  }
}

async function fillForm(
  wrapper: ReturnType<typeof mount>,
  email = 'anna@example.com',
  password = 'secret-1',
) {
  await wrapper.get('input[name="email"]').setValue(email)
  await wrapper.get('input[name="password"]').setValue(password)
  await wrapper.get('form').trigger('submit')
  await flushPromises()
}

describe('RegistrationForm', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  it('starts empty: the submit button waits for an email and a password', async () => {
    const wrapper = mount(RegistrationForm, { props: { api: fakeApi() } })
    const submit = wrapper.get('button[type="submit"]')
    expect(submit.text()).toBe('Зарегистрироваться')
    expect(submit.attributes('disabled')).toBeDefined()
    await wrapper.get('input[name="email"]').setValue('anna@example.com')
    await wrapper.get('input[name="password"]').setValue('secret-1')
    expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeUndefined()
    expect(wrapper.get('input[name="password"]').attributes('autocomplete')).toBe('new-password')
  })

  it('shows loading while the registration is sent', async () => {
    let finish: (outcome: RegistrationOutcome) => void = () => {}
    const api = fakeApi({
      register: vi.fn(() => new Promise<RegistrationOutcome>((resolve) => (finish = resolve))),
    })
    const wrapper = mount(RegistrationForm, { props: { api } })
    await wrapper.get('input[name="email"]').setValue(' anna@example.com ')
    await wrapper.get('input[name="password"]').setValue('secret-1')
    await wrapper.get('form').trigger('submit')
    expect(wrapper.get('button[type="submit"]').attributes('aria-busy')).toBe('true')
    expect(api.register).toHaveBeenCalledWith('anna@example.com', 'secret-1')
    finish({ status: 202, body: { registrationId: id, resendAfterSeconds: 60 } })
    await flushPromises()
    expect(wrapper.text()).toContain('Отправили код на anna@example.com')
  })

  it('refuses a short password without calling the API', async () => {
    const api = fakeApi()
    const wrapper = mount(RegistrationForm, { props: { api } })
    await fillForm(wrapper, 'anna@example.com', '12345')
    expect(api.register).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Пароль — не короче 6 символов.')
    expect(wrapper.get('input[name="password"]').attributes('aria-invalid')).toBe('true')
  })

  it.each([
    [{ status: 400, body: { errors: { email: ['x'] } } }, 'Это не похоже на адрес почты.'],
    [{ status: 409, body: { code: 'email_taken' } }, 'Этот адрес уже занят другим аккаунтом.'],
    [{ status: 429, retryAfter: 42 }, 'Слишком часто. Попробуйте через 0:42.'],
    [{ status: 0 }, 'Не удалось связаться с сервером'],
  ])('shows the error of the API: %o', async (outcome, message) => {
    const wrapper = mount(RegistrationForm, {
      props: { api: fakeApi({ register: vi.fn(async () => outcome) }) },
    })
    await fillForm(wrapper)
    expect(wrapper.text()).toContain(message)
    expect(wrapper.find('input[name="code"]').exists()).toBe(false)
  })

  it('counts the resend timer down and then sends a new code', async () => {
    const api = fakeApi()
    const wrapper = mount(RegistrationForm, { props: { api } })
    await fillForm(wrapper)
    expect(wrapper.get('[data-testid="resend-timer"]').text()).toContain('через 1:00')
    expect(wrapper.findAll('button').map((b) => b.text())).not.toContain('Отправить код снова')

    vi.advanceTimersByTime(15_000)
    await flushPromises()
    expect(wrapper.get('[data-testid="resend-timer"]').text()).toContain('через 0:45')

    vi.advanceTimersByTime(45_000)
    await flushPromises()
    expect(wrapper.find('[data-testid="resend-timer"]').exists()).toBe(false)
    const resend = wrapper.findAll('button').find((b) => b.text() === 'Отправить код снова')!
    await resend.trigger('click')
    await flushPromises()
    expect(api.resend).toHaveBeenCalledWith(id)
    expect(wrapper.text()).toContain('Отправили новый код')
    expect(wrapper.get('[data-testid="resend-timer"]').text()).toContain('через 1:00')
  })

  it('takes the timer from the API when it says the code is too soon', async () => {
    const api = fakeApi({ resend: vi.fn(async () => ({ status: 429, retryAfter: 20 })) })
    const wrapper = mount(RegistrationForm, { props: { api } })
    await fillForm(wrapper)
    vi.advanceTimersByTime(60_000)
    await flushPromises()
    await wrapper
      .findAll('button')
      .find((b) => b.text() === 'Отправить код снова')!
      .trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-testid="resend-timer"]').text()).toContain('через 0:20')
  })

  it('refuses a wrong code and emits confirmed for the right one', async () => {
    const confirm = vi
      .fn<RegistrationApi['confirm']>()
      .mockResolvedValueOnce({ status: 400, body: { code: 'confirmation_invalid' } })
      .mockResolvedValueOnce({ status: 204 })
    const wrapper = mount(RegistrationForm, { props: { api: fakeApi({ confirm }) } })
    await fillForm(wrapper)
    const confirmButton = () => wrapper.findAll('button').find((b) => b.text() === 'Подтвердить')!
    expect(confirmButton().attributes('disabled')).toBeDefined()

    await wrapper.get('input[name="code"]').setValue('000000')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.text()).toContain('Неверный или просроченный код')
    expect(wrapper.emitted('confirmed')).toBeUndefined()

    await wrapper.get('input[name="code"]').setValue(' 123456 ')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(confirm).toHaveBeenLastCalledWith(id, '123456')
    expect(wrapper.emitted('confirmed')).toHaveLength(1)
  })

  it('goes back to the form when the registration is gone', async () => {
    const api = fakeApi({ resend: vi.fn(async () => ({ status: 404 })) })
    const wrapper = mount(RegistrationForm, { props: { api } })
    await fillForm(wrapper)
    vi.advanceTimersByTime(60_000)
    await flushPromises()
    await wrapper
      .findAll('button')
      .find((b) => b.text() === 'Отправить код снова')!
      .trigger('click')
    await flushPromises()
    expect(wrapper.find('input[name="email"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Регистрация устарела. Начните заново.')
  })
})

describe('registration helpers', () => {
  it('formats the countdown as minutes and seconds', () => {
    expect([formatCountdown(60), formatCountdown(45), formatCountdown(5), formatCountdown(-1)]).toEqual([
      '1:00',
      '0:45',
      '0:05',
      '0:00',
    ])
  })

  it('reads Retry-After seconds', () => {
    expect(retryAfterSeconds(new Response(null, { status: 429, headers: { 'retry-after': '15' } }))).toBe(15)
    expect(retryAfterSeconds(new Response(null, { status: 429 }))).toBeUndefined()
    expect(retryAfterSeconds(undefined)).toBeUndefined()
  })
})
