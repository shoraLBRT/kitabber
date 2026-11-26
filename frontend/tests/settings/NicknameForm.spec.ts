import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import NicknameForm from '../../components/settings/NicknameForm.vue'
import { useToast } from '../../composables/useToast'
import type { NicknameSave, Profile } from '../../api/profiles'

// The nickname of the own blog (E04-05, ADR-0090): the shape checked as the person types, the API's refusals under
// the field, and the lock for 30 days after a change. e2e/nickname.spec.ts checks the flow in a browser.
const anna: Profile = {
  id: '0195f3a0-0000-7000-8000-0000000000b1',
  type: 'personal',
  name: 'Анна',
  nickname: 'anna',
  description: null,
  registeredAt: '2026-03-14T10:00:00Z',
  avatarId: null,
  avatarUrl: null,
  avatarSources: [],
  subscriberCount: 0,
  subscribed: false,
  cover: null,
  nicknameChangeableFrom: null,
}

const now = () => new Date('2026-11-26T12:00:00Z')
const toast = useToast()
afterEach(() => toast.clear())

function mountForm(save: (nickname: string) => Promise<NicknameSave>, profile: Profile = anna) {
  return mount(NicknameForm, { props: { profile, save, now } })
}

describe('NicknameForm', () => {
  it('saves a new nickname without @ and in lower case', async () => {
    const save = vi.fn(async (nickname: string): Promise<NicknameSave> => ({
      status: 'saved',
      profile: { ...anna, nickname, nicknameChangeableFrom: '2026-12-26T12:00:00Z' },
    }))
    const form = mountForm(save)

    await form.find('input').setValue('@Anna_K')
    await form.find('form').trigger('submit')
    await flushPromises()

    expect(save).toHaveBeenCalledWith('anna_k')
    expect(form.emitted('saved')?.[0]?.[0]).toMatchObject({ nickname: 'anna_k' })
    expect(toast.toasts.at(-1)?.message).toBe('Никнейм изменён')
  })

  it('checks the shape before sending', async () => {
    const save = vi.fn()
    const form = mountForm(save)

    await form.find('input').setValue('1abc')
    await form.find('form').trigger('submit')

    expect(save).not.toHaveBeenCalled()
    expect(form.text()).toContain('Никнейм — от 3 до 32 латинских букв')
  })

  it('shows a taken or reserved nickname under the field', async () => {
    const form = mountForm(async () => ({
      status: 'invalid',
      errors: { nickname: 'Этот никнейм уже занят блогом или темой.' },
    }))

    await form.find('input').setValue('taken')
    await form.find('form').trigger('submit')
    await flushPromises()

    expect(form.text()).toContain('Этот никнейм уже занят блогом или темой.')
  })

  it('is locked while the 30 days run and says until when', () => {
    const form = mountForm(vi.fn(), { ...anna, nicknameChangeableFrom: '2026-12-20T12:00:00Z' })

    expect(form.find('input').attributes('disabled')).toBeDefined()
    expect(form.find('[data-testid="nickname-locked"]').text()).toContain('20 декабря 2026')
  })

  it('locks itself when the API says it is too soon', async () => {
    const form = mountForm(async () => ({
      status: 'too-soon',
      message: 'Никнейм можно менять раз в 30 дней.',
    }))

    await form.find('input').setValue('anna_new')
    await form.find('form').trigger('submit')
    await flushPromises()

    expect(form.find('[data-testid="nickname-locked"]').text()).toBe('Никнейм можно менять раз в 30 дней.')
  })

  it('keeps what was typed when the save fails', async () => {
    const form = mountForm(async () => ({ status: 'error' }))

    await form.find('input').setValue('anna_new')
    await form.find('form').trigger('submit')
    await flushPromises()

    expect((form.find('input').element as HTMLInputElement).value).toBe('anna_new')
    expect(toast.toasts.at(-1)?.message).toBe('Не удалось сохранить. Попробуйте ещё раз.')
  })
})
