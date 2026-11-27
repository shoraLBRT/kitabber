import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import OnboardingSteps from '../../components/onboarding/OnboardingSteps.vue'
import { useToast } from '../../composables/useToast'
import type { MediaUpload } from '../../api/media'
import {
  onboardingNameError,
  onboardingPath,
  safeReturn,
  type OnboardingFinish,
  type OnboardingInput,
} from '../../api/onboarding'
import type { Profile } from '../../api/profiles'
import type { TopicCard } from '../../api/topics'

// The onboarding (E03-10, ADR-0102): the name checked before it is sent, every step skippable,
// the chosen topics and the avatar sent once at the end, and the states of the topic step (loading, error, empty).
// The crop needs a real canvas, so the cropper is a stand-in; e2e/onboarding.spec.ts checks the flow in a browser.
const newcomer: Profile = {
  id: '0195f3a0-0000-7000-8000-0000000000c1',
  type: 'personal',
  name: null,
  nickname: null,
  description: null,
  registeredAt: '2026-11-27T10:00:00Z',
  avatarId: null,
  avatarUrl: null,
  avatarSources: [],
  subscriberCount: 0,
  subscribed: false,
  cover: null,
  onboardingPending: true,
}

const topics: TopicCard[] = [
  { id: 'topic-books', name: 'Книги', nickname: 'books', description: 'Что читать', subscriberCount: 41 },
  { id: 'topic-science', name: 'Наука', nickname: 'science', description: null, subscriberCount: 3 },
]

const cropped = new Blob(['jpeg'], { type: 'image/jpeg' })
const CropperStub = defineComponent({
  emits: ['done', 'cancel', 'failed'],
  setup:
    (_, { emit }) =>
    () =>
      h(
        'button',
        { type: 'button', 'data-testid': 'crop-apply', onClick: () => emit('done', cropped) },
        'ok',
      ),
})

const toast = useToast()
afterEach(() => toast.clear())

const uploaded: MediaUpload = {
  status: 'uploaded',
  media: {
    id: 'media-1',
    kind: 'image',
    contentType: 'image/jpeg',
    size: 4,
    fileName: 'avatar.jpg',
    createdAt: '',
    preview: null,
  },
}

function mountSteps(
  save: (input: OnboardingInput) => Promise<OnboardingFinish> = async () => ({
    status: 'saved',
    profile: { ...newcomer, onboardingPending: false },
  }),
  shown: TopicCard[] | null | 'loading' = topics,
  upload: () => Promise<MediaUpload> = async () => uploaded,
) {
  const saveSpy = vi.fn(save)
  const uploadSpy = vi.fn(upload)
  const wrapper = mount(OnboardingSteps, {
    props: {
      profile: newcomer,
      topics: shown === 'loading' ? undefined : shown,
      save: saveSpy,
      upload: uploadSpy,
    },
    attachTo: document.body,
    global: { stubs: { teleport: true, AvatarCropper: CropperStub } },
  })
  return { wrapper, saveSpy, uploadSpy }
}

const button = (wrapper: ReturnType<typeof mount>, text: string) =>
  wrapper
    .findAll('button')
    .find(
      (b) =>
        b.text() === text ||
        (b.find('.onboarding__topic-name').exists() && b.get('.onboarding__topic-name').text() === text),
    )!

async function click(wrapper: ReturnType<typeof mount>, text: string) {
  await button(wrapper, text).trigger('click')
  await flushPromises()
}

async function submit(wrapper: ReturnType<typeof mount>) {
  await wrapper.get('form').trigger('submit')
  await flushPromises()
}

describe('onboarding rules', () => {
  it('takes a name of 2 to 25 characters as people see them, and an empty one as a skip', () => {
    expect(onboardingNameError('Ян')).toBeNull()
    expect(onboardingNameError('  ')).toBeNull()
    expect(onboardingNameError('📚📚')).toBeNull()
    expect(onboardingNameError('Я')).toBe('Имя — от 2 до 25 символов.')
    expect(onboardingNameError('я'.repeat(26))).toBe('Имя — от 2 до 25 символов.')
  })

  it('leads on to a local page only', () => {
    expect(safeReturn('/my')).toBe('/my')
    expect(safeReturn('//evil.example')).toBe('/')
    expect(safeReturn('https://evil.example')).toBe('/')
    expect(safeReturn(undefined)).toBe('/')
    expect(onboardingPath('/u/anna?tab=1')).toBe('/onboarding?returnTo=%2Fu%2Fanna%3Ftab%3D1')
  })
})

describe('OnboardingSteps', () => {
  it('checks the name before going on and sends every choice once at the end', async () => {
    const { wrapper, saveSpy, uploadSpy } = mountSteps()
    expect(wrapper.text()).toContain('Шаг 1 из 3')
    await wrapper.get('input[data-field="name"]').setValue('Я')
    await submit(wrapper)
    expect(wrapper.text()).toContain('Имя — от 2 до 25 символов.')
    expect(wrapper.text()).toContain('Как вас зовут?')

    await wrapper.get('input[data-field="name"]').setValue('  Ян ')
    await submit(wrapper)
    expect(wrapper.text()).toContain('Шаг 2 из 3')
    const file = new File(['png'], 'me.png', { type: 'image/png' })
    const input = wrapper.get('[data-testid="avatar-file"]')
    Object.defineProperty(input.element, 'files', { value: [file], configurable: true })
    await input.trigger('change')
    await flushPromises()
    await wrapper.get('[data-testid="crop-apply"]').trigger('click')
    await submit(wrapper)

    expect(wrapper.text()).toContain('Выберите темы')
    await click(wrapper, 'Книги')
    expect(wrapper.text()).toContain('Выбрана 1 тема')
    expect(saveSpy).not.toHaveBeenCalled()
    await submit(wrapper)

    expect(uploadSpy).toHaveBeenCalledTimes(1)
    expect(saveSpy).toHaveBeenCalledWith({ name: 'Ян', avatarId: 'media-1', topicIds: ['topic-books'] })
    expect(wrapper.emitted('done')).toHaveLength(1)
    wrapper.unmount()
  })

  it('skips every step and sends an empty answer', async () => {
    const { wrapper, saveSpy, uploadSpy } = mountSteps()
    await wrapper.get('input[data-field="name"]').setValue('Я')
    await click(wrapper, 'Пропустить')
    await click(wrapper, 'Пропустить')
    await click(wrapper, 'Книги')
    await click(wrapper, 'Пропустить')
    expect(uploadSpy).not.toHaveBeenCalled()
    expect(saveSpy).toHaveBeenCalledWith({ name: null, avatarId: null, topicIds: [] })
    expect(wrapper.emitted('done')).toHaveLength(1)
    wrapper.unmount()
  })

  it('goes on when the onboarding was done elsewhere', async () => {
    const { wrapper } = mountSteps(async () => ({ status: 'done' }))
    for (let n = 0; n < 3; n++) await click(wrapper, 'Пропустить')
    expect(wrapper.emitted('done')).toHaveLength(1)
    wrapper.unmount()
  })

  it('brings back the step of an API field error', async () => {
    const { wrapper } = mountSteps(async () => ({
      status: 'invalid',
      errors: { name: 'Имя — от 2 до 25 символов.' },
    }))
    await wrapper.get('input[data-field="name"]').setValue('Ян')
    await submit(wrapper)
    await submit(wrapper)
    await submit(wrapper)
    expect(wrapper.text()).toContain('Как вас зовут?')
    expect(wrapper.text()).toContain('Имя — от 2 до 25 символов.')
    expect(wrapper.emitted('done')).toBeUndefined()
    wrapper.unmount()
  })

  it('keeps the choices and says so when saving fails', async () => {
    const { wrapper, saveSpy } = mountSteps(async () => ({ status: 'error' }))
    for (let n = 0; n < 2; n++) await click(wrapper, 'Пропустить')
    await click(wrapper, 'Наука')
    await submit(wrapper)
    expect(toast.toasts.map((item) => item.message)).toEqual([
      'Не удалось сохранить. Проверьте подключение и попробуйте ещё раз.',
    ])
    expect(wrapper.text()).toContain('Выбрана 1 тема')
    await submit(wrapper)
    expect(saveSpy).toHaveBeenCalledTimes(2)
    wrapper.unmount()
  })

  it.each([
    ['loading', 'onboarding-topics-loading', 'Загрузка'],
    [null, 'onboarding-topics-error', 'Не удалось загрузить темы.'],
    [[], 'onboarding-topics-empty', 'Тем пока нет. Их можно найти позже в каталоге.'],
  ])('shows the topic step with %j topics', async (shown, testId, text) => {
    const { wrapper } = mountSteps(undefined, shown as TopicCard[] | null | 'loading')
    for (let n = 0; n < 2; n++) await click(wrapper, 'Пропустить')
    expect(wrapper.get(`[data-testid="${testId}"]`).text()).toContain(text)
    if (shown === null) {
      await click(wrapper, 'Повторить')
      expect(wrapper.emitted('retryTopics')).toHaveLength(1)
    }
    wrapper.unmount()
  })
})
