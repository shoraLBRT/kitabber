import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import HiddenList from '../../components/settings/HiddenList.vue'
import TopicHide from '../../components/topic/TopicHide.vue'
import type { HiddenApi, HiddenList as HiddenListView, HiddenOutcome } from '../../api/hidden'
import { useToast } from '../../composables/useToast'

// Hiding from the feeds (E11-07, FR-SUB, FR-TOPIC, ADR-0119): the list of hidden topics and posts in the settings with
// its states (loading, empty, error) and «Вернуть в ленты», and «Скрыть из лент» on a topic's page. «Скрыть» of a post's
// «…» menu is in tests/post/PostMenu.spec.ts; the pages against the fake API are in e2e/hidden.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' }, teleport: true }
const books = '0195f3a0-0000-7000-8000-0000000000e1'
const post = '0195f3a0-0000-7000-8000-0000000000c1'
const gone = '0195f3a0-0000-7000-8000-0000000000c2'

const list: HiddenListView = {
  topics: [{ blogId: books, hiddenAt: '2026-11-27T10:00:00Z', name: 'Книги', nickname: 'books' }],
  posts: [
    { postId: post, hiddenAt: '2026-11-28T10:00:00Z', title: 'Привет, мир', gone: false },
    { postId: gone, hiddenAt: '2026-11-26T10:00:00Z', title: null, gone: true },
  ],
  max: 1000,
}

function fakeApi(overrides: Partial<HiddenApi> = {}): HiddenApi {
  return {
    load: vi.fn(async () => ({ status: 200, body: list })),
    topicState: vi.fn(async () => ({ status: 200, body: { hidden: false } })),
    hide: vi.fn(async () => ({ status: 204 })),
    show: vi.fn(async () => ({ status: 204 })),
    ...overrides,
  }
}

let wrapper: VueWrapper | undefined
const toasts = () => useToast().toasts.map((toast) => toast.message)

afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
  vi.restoreAllMocks()
  useToast().clear()
})

async function mountList(api: HiddenApi) {
  wrapper = mount(HiddenList, { props: { api }, global: { stubs } })
  await flushPromises()
  return wrapper
}

describe('HiddenList', () => {
  it('shows a skeleton until the lists are read', async () => {
    let answer: (value: HiddenOutcome) => void = () => {}
    const api = fakeApi({ load: vi.fn(() => new Promise<HiddenOutcome>((resolve) => (answer = resolve))) })
    wrapper = mount(HiddenList, { props: { api }, global: { stubs } })
    await flushPromises()
    expect(wrapper.find('[data-testid="hidden-list-loading"]').exists()).toBe(true)

    answer({ status: 200, body: list })
    await flushPromises()
    expect(wrapper.find('[data-testid="hidden-list-loading"]').exists()).toBe(false)
  })

  it('lists the hidden topics and posts with their links, a gone post too', async () => {
    const view = await mountList(fakeApi())
    const topic = view.get('[data-testid="hidden-topic"]')
    expect(topic.text()).toContain('Книги')
    expect(topic.get('a').attributes('href')).toBe('/books')
    const posts = view.findAll('[data-testid="hidden-post"]')
    expect(posts.map((item) => item.find('.hidden__name').text())).toEqual(['Привет, мир', 'Пост удалён'])
    expect(posts[0]!.get('a').attributes('href')).toBe(`/u/-/${post}`)
    expect(posts[1]!.find('a').exists()).toBe(false)
  })

  it('says so when nothing is hidden', async () => {
    const view = await mountList(
      fakeApi({ load: vi.fn(async () => ({ status: 200, body: { topics: [], posts: [], max: 1000 } })) }),
    )
    expect(view.get('[data-testid="hidden-topics-empty"]').text()).toBe('Скрытых тем нет.')
    expect(view.get('[data-testid="hidden-posts-empty"]').text()).toBe('Скрытых постов нет.')
  })

  it('offers «Повторить» when the lists cannot be read', async () => {
    const load = vi
      .fn()
      .mockResolvedValueOnce({ status: 500 })
      .mockResolvedValueOnce({ status: 200, body: list })
    const view = await mountList(fakeApi({ load }))
    expect(view.get('[role="alert"]').text()).toBe('Не удалось загрузить скрытое.')

    await view.get('button').trigger('click')
    await flushPromises()
    expect(view.findAll('[data-testid="hidden-post"]')).toHaveLength(2)
  })

  it('brings a topic and a post back to the feeds', async () => {
    const api = fakeApi()
    const view = await mountList(api)

    await view.get('button[aria-label="Вернуть в ленты: Книги"]').trigger('click')
    await flushPromises()
    expect(api.show).toHaveBeenCalledWith('topics', books)
    expect(view.find('[data-testid="hidden-topic"]').exists()).toBe(false)
    expect(view.get('[role="status"]').text()).toBe('«Книги» снова в ваших лентах.')

    await view.get('button[aria-label="Вернуть в ленты: Пост удалён"]').trigger('click')
    await flushPromises()
    expect(api.show).toHaveBeenCalledWith('posts', gone)
    expect(view.findAll('[data-testid="hidden-post"]')).toHaveLength(1)
  })

  it('keeps the item and says so when bringing it back fails', async () => {
    const view = await mountList(fakeApi({ show: vi.fn(async () => ({ status: 500 })) }))
    await view.get('button[aria-label="Вернуть в ленты: Привет, мир"]').trigger('click')
    await flushPromises()
    expect(view.findAll('[data-testid="hidden-post"]')).toHaveLength(2)
    expect(view.get('[role="alert"]').text()).toBe('Не удалось вернуть. Попробуйте ещё раз.')
  })
})

describe('TopicHide', () => {
  async function mountButton(api: HiddenApi) {
    wrapper = mount(TopicHide, { props: { blogId: books, api }, global: { stubs } })
    await flushPromises()
    return wrapper
  }

  it('hides the topic and shows it again', async () => {
    const api = fakeApi()
    const view = await mountButton(api)
    expect(api.topicState).toHaveBeenCalledWith(books)
    expect(view.get('button').text()).toBe('Скрыть из лент')

    await view.get('button').trigger('click')
    await flushPromises()
    expect(api.hide).toHaveBeenCalledWith('topics', books)
    expect(view.get('button').text()).toBe('Показать в лентах')
    expect(view.get('button').attributes('aria-pressed')).toBe('true')
    expect(view.find('[data-testid="topic-hidden-note"]').exists()).toBe(true)
    expect(toasts()).toContain('Тема скрыта из ваших лент.')

    await view.get('button').trigger('click')
    await flushPromises()
    expect(api.show).toHaveBeenCalledWith('topics', books)
    expect(view.get('button').text()).toBe('Скрыть из лент')
  })

  it('starts as hidden when the topic is hidden', async () => {
    const view = await mountButton(
      fakeApi({ topicState: vi.fn(async () => ({ status: 200, body: { hidden: true } })) }),
    )
    expect(view.get('button').text()).toBe('Показать в лентах')
  })

  it('shows nothing when the state cannot be read', async () => {
    const view = await mountButton(fakeApi({ topicState: vi.fn(async () => ({ status: 0 })) }))
    expect(view.find('[data-testid="topic-hide"]').exists()).toBe(false)
  })

  it('keeps the button and says so when hiding fails', async () => {
    const view = await mountButton(fakeApi({ hide: vi.fn(async () => ({ status: 500 })) }))
    await view.get('button').trigger('click')
    await flushPromises()
    expect(view.get('button').text()).toBe('Скрыть из лент')
    expect(toasts()).toContain('Не удалось скрыть. Попробуйте ещё раз.')
  })
})
