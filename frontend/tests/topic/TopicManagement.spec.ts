import { flushPromises, mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import TopicManagement from '../../components/topic/TopicManagement.vue'
import {
  managementActionOf,
  memberName,
  settingsInputErrors,
  type ManagementAction,
  type ManagementSave,
  type TopicManagement as Management,
} from '../../api/topic-management'

// The management of a topic (E11-12, ADR-0121): the owner's sections, the reader's rules and moderators, the offer to
// the recipient. The page against the fake API is e2e/topic-management.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

const anna = {
  accountId: '0195f3a0-0000-7000-8000-000000000001',
  blogId: '0195f3a0-0000-7000-8000-0000000000b1',
  name: 'Анна',
  nickname: 'anna',
}
const boris = {
  accountId: '0195f3a0-0000-7000-8000-000000000002',
  blogId: '0195f3a0-0000-7000-8000-0000000000b2',
  name: 'Борис',
  nickname: 'boris',
}

const science: Management = {
  id: '0195f3a0-0000-7000-8000-0000000000b5',
  name: 'Наука',
  nickname: 'science',
  description: 'Открытия',
  rules: '1. Без спама',
  role: 'owner',
  owner: anna,
  moderators: [],
  transfer: null,
  transferToYou: false,
}

function mountView(management: Management, change: (action: ManagementAction) => Promise<ManagementSave>) {
  return mount(TopicManagement, {
    props: { management, api: { change } },
    global: { stubs },
  })
}

const saved = (management: Management) => async (): Promise<ManagementSave> => ({
  status: 'saved',
  management,
})

describe('TopicManagement', () => {
  it('lets the owner save the description and the rules', async () => {
    const change = vi.fn(saved({ ...science, rules: 'Новые правила' }))
    const wrapper = mountView(science, change)

    const textareas = wrapper.findAll('textarea')
    await textareas[1]!.setValue('  Новые правила ')
    await wrapper.get('[data-testid="manage-settings"]').trigger('submit')
    await flushPromises()

    expect(change).toHaveBeenCalledWith({
      action: 'settings',
      description: 'Открытия',
      rules: 'Новые правила',
    })
    expect(wrapper.emitted('changed')?.[0]?.[0]).toMatchObject({ rules: 'Новые правила' })
  })

  it('does not send rules longer than the limit and shows the field error', async () => {
    const change = vi.fn(saved(science))
    const wrapper = mountView(science, change)

    await wrapper.findAll('textarea')[1]!.setValue('а'.repeat(2001))
    await wrapper.get('[data-testid="manage-settings"]').trigger('submit')

    expect(change).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Правила — не длиннее')
  })

  it('appoints a moderator by nickname and removes one; an unknown nickname shows the API error', async () => {
    const change = vi
      .fn<(action: ManagementAction) => Promise<ManagementSave>>()
      .mockResolvedValueOnce({ status: 'invalid', errors: { nickname: 'Нет блога с таким никнеймом.' } })
      .mockResolvedValueOnce({ status: 'saved', management: { ...science, moderators: [boris] } })
    const wrapper = mountView(science, change)
    expect(wrapper.find('[data-testid="manage-no-moderators"]').exists()).toBe(true)

    const field = wrapper.get('[data-testid="manage-appoint"] input')
    await field.setValue('nobody')
    await wrapper.get('[data-testid="manage-appoint"]').trigger('submit')
    await flushPromises()
    expect(wrapper.text()).toContain('Нет блога с таким никнеймом.')

    await field.setValue('@boris')
    await wrapper.get('[data-testid="manage-appoint"]').trigger('submit')
    await flushPromises()
    expect(change).toHaveBeenLastCalledWith({ action: 'appoint', nickname: '@boris' })

    await wrapper.setProps({ management: { ...science, moderators: [boris] } })
    expect(wrapper.get('[data-testid="manage-moderator"]').text()).toContain('Борис')
    change.mockResolvedValueOnce({ status: 'saved', management: science })
    await wrapper.get('[data-testid="manage-moderator-remove"]').trigger('click')
    expect(change).toHaveBeenLastCalledWith({ action: 'remove-moderator', accountId: boris.accountId })
  })

  it('offers the topic, shows the pending offer and cancels it', async () => {
    const change = vi.fn(saved(science))
    const wrapper = mountView(science, change)

    await wrapper.get('[data-testid="manage-offer"] input').setValue('boris')
    await wrapper.get('[data-testid="manage-offer"]').trigger('submit')
    await flushPromises()
    expect(change).toHaveBeenCalledWith({ action: 'offer', nickname: 'boris' })

    await wrapper.setProps({
      management: { ...science, transfer: { to: boris, offeredAt: '2026-11-28T12:00:00Z' } },
    })
    expect(wrapper.get('[data-testid="manage-transfer-pending"]').text()).toBe(
      'Тема предложена Борис. Ждём согласия.',
    )
    await wrapper.get('[data-testid="manage-transfer-cancel"]').trigger('click')
    expect(change).toHaveBeenLastCalledWith({ action: 'withdraw' })
  })

  it('shows a reader the rules and the moderators and nothing to edit', () => {
    const wrapper = mountView({ ...science, role: 'none', moderators: [boris] }, vi.fn(saved(science)))

    expect(wrapper.get('[data-testid="manage-rules-text"]').text()).toBe('1. Без спама')
    expect(wrapper.get('[data-testid="manage-owner"]').text()).toContain('Анна')
    expect(wrapper.get('[data-testid="manage-owner"] a').attributes('href')).toBe('/u/anna')
    expect(wrapper.find('[data-testid="manage-settings"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="manage-appoint"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="manage-transfer"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="manage-moderator-remove"]').exists()).toBe(false)
  })

  it('shows a topic without rules as such', () => {
    const wrapper = mountView({ ...science, role: 'moderator', rules: null }, vi.fn(saved(science)))
    expect(wrapper.text()).toContain('У темы пока нет правил.')
  })

  it('lets the person the topic is offered to accept or decline it, and shows a refusal', async () => {
    const offered = { ...science, owner: boris, role: 'none', transferToYou: true }
    const change = vi
      .fn<(action: ManagementAction) => Promise<ManagementSave>>()
      .mockResolvedValueOnce({ status: 'refused', message: 'Эту тему вам не предлагали.' })
      .mockResolvedValueOnce({ status: 'saved', management: { ...science, owner: anna } })
    const wrapper = mountView(offered, change)
    expect(wrapper.get('[data-testid="transfer-offer"]').text()).toContain(
      'Борис предлагает вам стать владельцем темы.',
    )

    await wrapper.get('[data-testid="transfer-accept"]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-testid="transfer-offer"] [role="alert"]').text()).toBe(
      'Эту тему вам не предлагали.',
    )

    await wrapper.get('[data-testid="transfer-decline"]').trigger('click')
    await flushPromises()
    expect(change.mock.calls.map(([action]) => action)).toEqual([
      { action: 'accept' },
      { action: 'withdraw' },
    ])
  })

  it('shows a failure of a change and asks a signed-out person to sign in', async () => {
    const change = vi
      .fn<(action: ManagementAction) => Promise<ManagementSave>>()
      .mockResolvedValueOnce({ status: 'error' })
      .mockResolvedValueOnce({ status: 'unauthorized' })
    const wrapper = mountView(science, change)

    await wrapper.get('[data-testid="manage-settings"]').trigger('submit')
    await flushPromises()
    expect(wrapper.text()).toContain('Не удалось сохранить. Попробуйте ещё раз.')

    await wrapper.get('[data-testid="manage-settings"]').trigger('submit')
    await flushPromises()
    expect(wrapper.emitted('unauthorized')).toHaveLength(1)
  })
})

describe('topic management rules', () => {
  it('checks the lengths of the description and the rules', () => {
    expect(settingsInputErrors({ description: 'а'.repeat(160), rules: 'б'.repeat(2000) })).toEqual({})
    expect(
      Object.keys(settingsInputErrors({ description: 'а'.repeat(161), rules: 'б'.repeat(2001) })),
    ).toEqual(['description', 'rules'])
  })

  it('passes on only the changes it knows', () => {
    expect(managementActionOf({ action: 'appoint', nickname: 'boris' })).toEqual({
      action: 'appoint',
      nickname: 'boris',
    })
    expect(managementActionOf({ action: 'remove-moderator' })).toBeNull()
    expect(managementActionOf({ action: 'delete-topic' })).toBeNull()
    expect(managementActionOf(null)).toBeNull()
    expect(managementActionOf({ action: 'settings', description: 1, rules: 'r' })).toEqual({
      action: 'settings',
      description: null,
      rules: 'r',
    })
  })

  it('names a person by the name, the nickname or «Пользователь»', () => {
    expect(memberName(boris)).toBe('Борис')
    expect(memberName({ ...boris, name: null })).toBe('@boris')
    expect(memberName({ name: null, nickname: null })).toBe('Пользователь')
  })
})
