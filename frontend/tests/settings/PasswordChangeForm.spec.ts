import { flushPromises, mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import PasswordChangeForm from '../../components/settings/PasswordChangeForm.vue'
import type { PasswordChangeApi, PasswordOutcome } from '../../api/password'

function fakeApi(
  change: PasswordChangeApi['change'] = vi.fn(async () => ({ status: 204 })),
): PasswordChangeApi {
  return { change }
}

async function submit(wrapper: ReturnType<typeof mount>, current: string, next: string) {
  await wrapper.get('input[name="current-password"]').setValue(current)
  await wrapper.get('input[name="new-password"]').setValue(next)
  await wrapper.get('form').trigger('submit')
  await flushPromises()
}

describe('PasswordChangeForm', () => {
  it('starts empty: saving waits for both passwords', async () => {
    const wrapper = mount(PasswordChangeForm, { props: { api: fakeApi() } })
    const button = wrapper.get('button[type="submit"]')
    expect(button.attributes('disabled')).toBeDefined()
    await wrapper.get('input[name="current-password"]').setValue('secret-1')
    await wrapper.get('input[name="new-password"]').setValue('brand-new-2')
    expect(button.attributes('disabled')).toBeUndefined()
    expect(wrapper.get('input[name="current-password"]').attributes('autocomplete')).toBe('current-password')
    expect(wrapper.get('input[name="new-password"]').attributes('autocomplete')).toBe('new-password')
  })

  it('shows loading, then says the password changed and clears the fields', async () => {
    let finish: (outcome: PasswordOutcome) => void = () => {}
    const change = vi.fn(() => new Promise<PasswordOutcome>((resolve) => (finish = resolve)))
    const wrapper = mount(PasswordChangeForm, { props: { api: fakeApi(change) } })
    await wrapper.get('input[name="current-password"]').setValue('secret-1')
    await wrapper.get('input[name="new-password"]').setValue('brand-new-2')
    await wrapper.get('form').trigger('submit')

    expect(change).toHaveBeenCalledWith('secret-1', 'brand-new-2')
    expect(wrapper.get('button[type="submit"]').attributes('aria-busy')).toBe('true')
    finish({ status: 204 })
    await flushPromises()

    expect(wrapper.get('[role="status"]').text()).toContain('Пароль изменён')
    expect((wrapper.get('input[name="current-password"]').element as HTMLInputElement).value).toBe('')
  })

  it('refuses a short new password before calling the API', async () => {
    const change = vi.fn<PasswordChangeApi['change']>()
    const wrapper = mount(PasswordChangeForm, { props: { api: fakeApi(change) } })
    await submit(wrapper, 'secret-1', '12345')

    expect(change).not.toHaveBeenCalled()
    expect(wrapper.get('input[name="new-password"]').attributes('aria-invalid')).toBe('true')
  })

  it('shows a wrong current password at its field', async () => {
    const wrapper = mount(PasswordChangeForm, {
      props: {
        api: fakeApi(vi.fn(async () => ({ status: 400, body: { code: 'current_password_invalid' } }))),
      },
    })
    await submit(wrapper, 'wrong-one', 'brand-new-2')

    expect(wrapper.get('input[name="current-password"]').attributes('aria-invalid')).toBe('true')
    expect(wrapper.text()).toContain('Неверный пароль.')
    expect(wrapper.find('[role="status"]').exists()).toBe(false)
  })

  it('shows the API password rule at the new password', async () => {
    const wrapper = mount(PasswordChangeForm, {
      props: {
        api: fakeApi(
          vi.fn(async () => ({
            status: 400,
            body: { code: 'validation_failed', errors: { newPassword: ['x'] } },
          })),
        ),
      },
    })
    await submit(wrapper, 'secret-1', '       x')
    expect(wrapper.get('input[name="new-password"]').attributes('aria-invalid')).toBe('true')
  })

  it('shows an ended session, too many tries and a failure', async () => {
    const change = vi
      .fn<PasswordChangeApi['change']>()
      .mockResolvedValueOnce({ status: 401 })
      .mockResolvedValueOnce({ status: 429 })
      .mockResolvedValueOnce({ status: 0 })
    const wrapper = mount(PasswordChangeForm, { props: { api: fakeApi(change) } })

    await submit(wrapper, 'secret-1', 'brand-new-2')
    expect(wrapper.get('[role="alert"]').text()).toContain('Войдите снова')
    await submit(wrapper, 'secret-1', 'brand-new-2')
    expect(wrapper.get('[role="alert"]').text()).toContain('Слишком много попыток')
    await submit(wrapper, 'secret-1', 'brand-new-2')
    expect(wrapper.get('[role="alert"]').text()).toContain('Не удалось сохранить пароль')
  })
})
