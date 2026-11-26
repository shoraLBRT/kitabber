import { flushPromises, mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import EmailChangeForm from '../../components/settings/EmailChangeForm.vue'
import type { AccountEmailState, EmailChangeApi, EmailOutcome } from '../../api/email'

const confirmed: AccountEmailState = {
  email: 'old@example.com',
  confirmedAt: '2026-11-20T10:00:00Z',
  pendingEmail: null,
}

function fakeApi(overrides: Partial<EmailChangeApi> = {}): EmailChangeApi {
  return {
    load: vi.fn(async () => ({ status: 200, body: confirmed })),
    change: vi.fn(async () => ({ status: 202 })),
    confirm: vi.fn(async () => ({ status: 204 })),
    ...overrides,
  }
}

async function mounted(api: EmailChangeApi) {
  const wrapper = mount(EmailChangeForm, { props: { api } })
  await flushPromises()
  return wrapper
}

async function fillAndSubmit(wrapper: ReturnType<typeof mount>, name: string, value: string) {
  const input = wrapper.get(`input[name="${name}"]`)
  await input.setValue(value)
  ;(input.element as HTMLInputElement).form!.dispatchEvent(new Event('submit'))
  await flushPromises()
}

const requestCode = (wrapper: ReturnType<typeof mount>, email: string) =>
  fillAndSubmit(wrapper, 'new-email', email)

const enterCode = (wrapper: ReturnType<typeof mount>, code: string) =>
  fillAndSubmit(wrapper, 'email-code', code)

describe('EmailChangeForm', () => {
  it('shows loading until the email is read, then the confirmed address', async () => {
    let finish: (outcome: EmailOutcome) => void = () => {}
    const load = vi.fn(() => new Promise<EmailOutcome>((resolve) => (finish = resolve)))
    const wrapper = mount(EmailChangeForm, { props: { api: fakeApi({ load }) } })
    await flushPromises()
    expect(wrapper.find('[data-testid="email-loading"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="email-change"]').attributes('aria-busy')).toBe('true')

    finish({ status: 200, body: confirmed })
    await flushPromises()
    expect(wrapper.get('[data-testid="email-current"]').text()).toContain('old@example.com')
    expect(wrapper.get('[data-testid="email-current"]').text()).toContain('подтверждена')
    expect(wrapper.find('input[name="email-code"]').exists()).toBe(false)
    expect(wrapper.get('button[type="submit"]').attributes('disabled')).toBeDefined()
  })

  it('says when the account has no email yet', async () => {
    const wrapper = await mounted(
      fakeApi({
        load: vi.fn(async () => ({
          status: 200,
          body: { email: null, confirmedAt: null, pendingEmail: null },
        })),
      }),
    )
    expect(wrapper.get('[data-testid="email-current"]').text()).toContain('нет подтверждённой почты')
    expect(wrapper.find('[data-testid="email-pending"]').exists()).toBe(false)
  })

  it('offers a retry when the email could not be read', async () => {
    const load = vi
      .fn<EmailChangeApi['load']>()
      .mockResolvedValueOnce({ status: 0 })
      .mockResolvedValueOnce({ status: 200, body: confirmed })
    const wrapper = await mounted(fakeApi({ load }))
    expect(wrapper.get('[role="alert"]').text()).toContain('Не удалось загрузить почту')

    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-testid="email-current"]').text()).toContain('old@example.com')
  })

  it('keeps the confirmed email until the code from the new address is entered', async () => {
    const api = fakeApi({
      load: vi
        .fn<EmailChangeApi['load']>()
        .mockResolvedValueOnce({ status: 200, body: confirmed })
        .mockResolvedValueOnce({
          status: 200,
          body: { email: 'new@example.com', confirmedAt: '2026-11-26T10:00:00Z', pendingEmail: null },
        }),
    })
    const wrapper = await mounted(api)

    await requestCode(wrapper, ' new@example.com ')
    expect(api.change).toHaveBeenCalledWith('new@example.com')
    expect(wrapper.get('[role="status"]').text()).toContain('Код отправлен')
    expect(wrapper.get('[data-testid="email-current"]').text()).toContain('old@example.com')
    expect(wrapper.get('[data-testid="email-pending"]').text()).toContain('new@example.com')

    await enterCode(wrapper, '123456')
    expect(api.confirm).toHaveBeenCalledWith('123456')
    expect(wrapper.get('[data-testid="email-current"]').text()).toContain('new@example.com')
    expect(wrapper.get('[role="status"]').text()).toContain('На прежний адрес мы отправили письмо')
    expect(wrapper.find('[data-testid="email-pending"]').exists()).toBe(false)
  })

  it('shows a wrong code at its field and keeps the old email', async () => {
    const api = fakeApi({
      load: vi.fn(async () => ({ status: 200, body: { ...confirmed, pendingEmail: 'new@example.com' } })),
      confirm: vi.fn(async () => ({ status: 400, body: { code: 'confirmation_invalid' } })),
    })
    const wrapper = await mounted(api)
    await enterCode(wrapper, '000000')

    expect(wrapper.get('input[name="email-code"]').attributes('aria-invalid')).toBe('true')
    expect(wrapper.text()).toContain('Неверный или просроченный код')
    expect(wrapper.get('[data-testid="email-current"]').text()).toContain('old@example.com')
    expect(api.load).toHaveBeenCalledTimes(1)
  })

  it('refuses a taken address without naming its owner, and an invalid one', async () => {
    const change = vi
      .fn<EmailChangeApi['change']>()
      .mockResolvedValueOnce({ status: 409, body: { code: 'email_taken' } })
      .mockResolvedValueOnce({ status: 400, body: { code: 'validation_failed', errors: { email: ['x'] } } })
    const wrapper = await mounted(fakeApi({ change }))

    await requestCode(wrapper, 'taken@example.com')
    expect(wrapper.get('input[name="new-email"]').attributes('aria-invalid')).toBe('true')
    expect(wrapper.text()).toContain('Этот адрес уже занят.')
    expect(wrapper.find('[data-testid="email-pending"]').exists()).toBe(false)

    await requestCode(wrapper, 'not-an-email')
    expect(wrapper.text()).toContain('Введите адрес почты')
  })

  it('says the address is already the account email', async () => {
    const wrapper = await mounted(fakeApi({ change: vi.fn(async () => ({ status: 204 })) }))
    await requestCode(wrapper, 'OLD@example.com')
    expect(wrapper.get('[role="status"]').text()).toContain('Это уже почта аккаунта')
  })

  it('shows an ended session, too many codes and a failure', async () => {
    const change = vi
      .fn<EmailChangeApi['change']>()
      .mockResolvedValueOnce({ status: 401 })
      .mockResolvedValueOnce({ status: 429 })
      .mockResolvedValueOnce({ status: 0 })
    const wrapper = await mounted(fakeApi({ change }))

    await requestCode(wrapper, 'new@example.com')
    expect(wrapper.get('[role="alert"]').text()).toContain('Войдите снова')
    await requestCode(wrapper, 'new@example.com')
    expect(wrapper.get('[role="alert"]').text()).toContain('Слишком много попыток')
    await requestCode(wrapper, 'new@example.com')
    expect(wrapper.get('[role="alert"]').text()).toContain('Не удалось отправить код')
  })
})
