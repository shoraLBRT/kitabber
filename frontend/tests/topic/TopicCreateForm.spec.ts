import { flushPromises, mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import TopicCreateForm from '../../components/topic/TopicCreateForm.vue'
import { createTopic, topicInputErrors, type TopicCreate, type TopicInput } from '../../api/topics'
import type { ApiClient } from '../../api/client'

// The form of a new topic (E11-11, ADR-0084): the checks as the person types, the empty form, the creating state and
// every answer of the API; the whole flow against the fake API is in e2e/topic-create.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }
const science = { id: '0195f3a0-0000-7000-8000-0000000000c1', name: 'Наука', nickname: 'science' }

function mountForm(
  create: (input: TopicInput) => Promise<TopicCreate> = async () => ({ status: 'created', topic: science }),
) {
  const createSpy = vi.fn(create)
  const wrapper = mount(TopicCreateForm, {
    props: { create: createSpy },
    attachTo: document.body,
    global: { stubs },
  })
  return { wrapper, createSpy }
}

const field = (wrapper: ReturnType<typeof mount>, name: string) => wrapper.get(`[data-field="${name}"]`)

async function fill(wrapper: ReturnType<typeof mount>, name: string, nickname: string, description = '') {
  await field(wrapper, 'name').setValue(name)
  await field(wrapper, 'nickname').setValue(nickname)
  await field(wrapper, 'description').setValue(description)
}

describe('TopicCreateForm', () => {
  it('starts empty, without errors, and shows where the topic will live', async () => {
    const { wrapper } = mountForm()
    expect(wrapper.find('[aria-invalid="true"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('0/25')
    await field(wrapper, 'nickname').setValue('  @Science ')
    expect(wrapper.text()).toContain('Страница темы: /science')
    wrapper.unmount()
  })

  it('refuses an empty form at once and focuses the first field', async () => {
    const { wrapper, createSpy } = mountForm()
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(createSpy).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('Название — от 2 до 25 символов.')
    expect(wrapper.text()).toContain('Адрес — от 3 до 32 латинских букв')
    expect(document.activeElement).toBe(field(wrapper, 'name').element)
    wrapper.unmount()
  })

  it('creates the topic with the trimmed name and the stored form of the address', async () => {
    const { wrapper, createSpy } = mountForm()
    await fill(wrapper, ' Наука ', 'Science', 'Открытия')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(createSpy).toHaveBeenCalledWith({ name: 'Наука', nickname: 'science', description: 'Открытия' })
    expect(wrapper.emitted('created')).toEqual([[science]])
    wrapper.unmount()
  })

  it('shows the creating state and sends once', async () => {
    let finish: (result: TopicCreate) => void = () => {}
    const { wrapper, createSpy } = mountForm(() => new Promise((resolve) => (finish = resolve)))
    await fill(wrapper, 'Наука', 'science')
    await wrapper.get('form').trigger('submit')
    await wrapper.get('form').trigger('submit')
    expect(createSpy).toHaveBeenCalledTimes(1)
    expect(wrapper.get('[data-testid="topic-create-submit"]').attributes('aria-busy')).toBe('true')
    finish({ status: 'created', topic: science })
    await flushPromises()
    wrapper.unmount()
  })

  it('puts the API field errors under the fields and clears one when it is edited', async () => {
    const { wrapper } = mountForm(async () => ({
      status: 'invalid',
      errors: { name: 'Тема с таким названием уже есть.', nickname: 'Такой адрес выбрать нельзя.' },
    }))
    await fill(wrapper, 'Игры', 'settings')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.text()).toContain('Тема с таким названием уже есть.')
    expect(wrapper.text()).toContain('Такой адрес выбрать нельзя.')
    expect(document.activeElement).toBe(field(wrapper, 'name').element)
    await field(wrapper, 'nickname').setValue('games_club')
    expect(wrapper.text()).not.toContain('Такой адрес выбрать нельзя.')
    wrapper.unmount()
  })

  it.each([
    [{ status: 'limit', message: 'Одному аккаунту можно создать не больше 5 тем.' }, 'не больше 5 тем'],
    [{ status: 'unconfirmed' }, 'Подтвердите почту, чтобы создавать темы.'],
    [{ status: 'error' }, 'Не удалось создать тему.'],
  ] as [TopicCreate, string][])(
    'says why the topic was not created (%o) and keeps what was typed',
    async (result, text) => {
      const { wrapper } = mountForm(async () => result)
      await fill(wrapper, 'Наука', 'science')
      await wrapper.get('form').trigger('submit')
      await flushPromises()
      expect(wrapper.get('[data-testid="topic-create-error"]').text()).toContain(text)
      expect((field(wrapper, 'name').element as HTMLInputElement).value).toBe('Наука')
      expect(wrapper.find('a[href="/settings/account"]').exists()).toBe(result.status === 'unconfirmed')
      wrapper.unmount()
    },
  )

  it('hands an expired session to the page', async () => {
    const { wrapper } = mountForm(async () => ({ status: 'unauthorized' }))
    await fill(wrapper, 'Наука', 'science')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.emitted('unauthorized')).toHaveLength(1)
    wrapper.unmount()
  })
})

describe('topicInputErrors', () => {
  it('checks the lengths and the address form the API checks', () => {
    expect(topicInputErrors({ name: 'Наука', nickname: 'science', description: '' })).toEqual({})
    expect(
      Object.keys(topicInputErrors({ name: 'Я', nickname: 'id42', description: 'а'.repeat(161) })).sort(),
    ).toEqual(['description', 'name', 'nickname'])
  })
})

describe('createTopic', () => {
  const answer = (status: number, body: unknown) =>
    ({
      POST: vi.fn(async () => ({
        data: status === 201 ? body : undefined,
        error: status === 201 ? undefined : body,
        response: new Response(null, { status }),
      })),
    }) as unknown as ApiClient

  it('reads every answer of POST /topics', async () => {
    const input = { name: 'Наука', nickname: 'science', description: null }
    expect(await createTopic(answer(201, science), input)).toEqual({ status: 'created', topic: science })
    expect(await createTopic(answer(400, { errors: { name: ['Занято.'] } }), input)).toEqual({
      status: 'invalid',
      errors: { name: 'Занято.' },
    })
    expect(await createTopic(answer(401, {}), input)).toEqual({ status: 'unauthorized' })
    expect(await createTopic(answer(403, {}), input)).toEqual({ status: 'unconfirmed' })
    expect(await createTopic(answer(409, { detail: 'Не больше 5 тем.' }), input)).toEqual({
      status: 'limit',
      message: 'Не больше 5 тем.',
    })
    expect(await createTopic(answer(500, {}), input)).toEqual({ status: 'error' })
  })
})
