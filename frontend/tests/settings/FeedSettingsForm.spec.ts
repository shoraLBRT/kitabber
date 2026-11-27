// Acceptance of E08-13 (#102): «Ленты» keeps the home feed, the rating threshold of «Свежее» and the video autoplay in
// the account — each choice saved at once with the whole section, a failure puts the previous choice back with a
// toast; the section shows a skeleton while loading and «Повторить» when it could not be read. The home page's
// redirect is homeFeedPath.
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  homeFeedPath,
  type FeedPreferences,
  type Preferences,
  type PreferencesApi,
} from '../../api/preferences'
import FeedSettingsForm from '../../components/settings/FeedSettingsForm.vue'
import { useToast } from '../../composables/useToast'

let wrapper: ReturnType<typeof mount> | undefined
beforeEach(() => useToast().clear())
afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

const defaults: FeedPreferences = { freshMinRating: null, homeFeed: 'popular', autoplayVideo: true }

function fakeAccount(feeds: Partial<FeedPreferences> = {}, options: { load?: boolean; save?: boolean } = {}) {
  const saved: FeedPreferences[] = []
  const api: Pick<PreferencesApi, 'load' | 'saveFeeds'> = {
    load: vi.fn(async (): Promise<Preferences | null> =>
      options.load === false ? null : { general: { theme: 'system' }, feeds: { ...defaults, ...feeds } },
    ),
    saveFeeds: vi.fn(async (value: FeedPreferences) => {
      if (options.save === false) return false
      saved.push(value)
      return true
    }),
  }
  return { api, saved }
}

const section = (name: string) => wrapper!.get(`[data-testid="feed-settings-${name}"]`)
const checkedIn = (name: string) =>
  section(name)
    .findAll<HTMLInputElement>('input[type="radio"]')
    .find((radio) => radio.element.checked)?.element.value
const autoplay = () => section('video').get<HTMLInputElement>('input[type="checkbox"]')
const messages = () => useToast().toasts.map((toast) => toast.message)

describe('FeedSettingsForm', () => {
  it('shows a skeleton until the account is read', () => {
    const { api } = fakeAccount()
    wrapper = mount(FeedSettingsForm, { props: { api } })
    expect(wrapper.get('[data-testid="settings-loading"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.findAll('input')).toHaveLength(0)
  })

  it('shows the account’s choices', async () => {
    const { api } = fakeAccount({ homeFeed: 'my', freshMinRating: 5, autoplayVideo: false })
    wrapper = mount(FeedSettingsForm, { props: { api } })
    await flushPromises()

    expect(wrapper.findAll('legend').map((legend) => legend.text())).toEqual([
      'Лента на главной',
      'Рейтинг в «Свежем»',
      'Видео',
    ])
    expect(
      section('home')
        .findAll('label')
        .map((label) => label.text()),
    ).toEqual(['Популярное', 'Свежее', 'Моя лента'])
    expect(
      section('fresh')
        .findAll('label')
        .map((label) => label.text()),
    ).toEqual(['Все', 'От −10', 'От +5', 'От +10'])
    expect(checkedIn('home')).toBe('my')
    expect(checkedIn('fresh')).toBe('5')
    expect(autoplay().element.checked).toBe(false)
  })

  it('saves every choice at once with the whole section and says so', async () => {
    const { api, saved } = fakeAccount({ freshMinRating: 10 })
    wrapper = mount(FeedSettingsForm, { props: { api } })
    await flushPromises()

    await section('home').findAll('input')[1]!.setValue(true)
    await flushPromises()
    await section('fresh').findAll('input')[0]!.setValue(true)
    await flushPromises()
    await autoplay().setValue(false)
    await flushPromises()

    expect(saved).toEqual([
      { freshMinRating: 10, homeFeed: 'fresh', autoplayVideo: true },
      { freshMinRating: null, homeFeed: 'fresh', autoplayVideo: true },
      { freshMinRating: null, homeFeed: 'fresh', autoplayVideo: false },
    ])
    expect(messages()).toEqual(['Изменения сохранены', 'Изменения сохранены', 'Изменения сохранены'])
    expect(wrapper.emitted('saved')).toHaveLength(3)
  })

  it('puts the previous choice back when the save fails', async () => {
    const { api } = fakeAccount({}, { save: false })
    wrapper = mount(FeedSettingsForm, { props: { api } })
    await flushPromises()

    await section('home').findAll('input')[2]!.setValue(true)
    await flushPromises()

    expect(checkedIn('home')).toBe('popular')
    expect(messages()).toEqual(['Не удалось сохранить. Попробуйте ещё раз.'])
    expect(wrapper.emitted('saved')).toBeUndefined()
  })

  it('offers «Повторить» when the account could not be read', async () => {
    const { api } = fakeAccount({}, { load: false })
    wrapper = mount(FeedSettingsForm, { props: { api } })
    await flushPromises()

    expect(wrapper.get('[role="alert"]').text()).toBe('Проверьте соединение и попробуйте ещё раз.')
    vi.mocked(api.load).mockResolvedValueOnce({
      general: { theme: 'system' },
      feeds: { ...defaults, homeFeed: 'fresh' },
    })
    await wrapper.get('button').trigger('click')
    await flushPromises()

    expect(checkedIn('home')).toBe('fresh')
  })
})

describe('homeFeedPath', () => {
  const of = (homeFeed: string): Preferences => ({
    general: { theme: 'system' },
    feeds: { ...defaults, homeFeed },
  })

  it('sends «Свежее» and «Моя лента» to their feeds and keeps «Популярное» on the home page', () => {
    expect(homeFeedPath(of('fresh'))).toBe('/new')
    expect(homeFeedPath(of('my'))).toBe('/my')
    expect(homeFeedPath(of('popular'))).toBeNull()
    expect(homeFeedPath(of('unknown'))).toBeNull()
    expect(homeFeedPath(null)).toBeNull()
  })
})
