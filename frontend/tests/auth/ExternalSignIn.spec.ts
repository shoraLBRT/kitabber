import { flushPromises, mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import ExternalSignIn from '../../components/auth/ExternalSignIn.vue'
import SignInForm from '../../components/auth/SignInForm.vue'
import {
  externalErrorMessage,
  externalSignInApi,
  telegramCallbackQuery,
  type ExternalSignInApi,
} from '../../api/external-sign-in'
import type { ApiClient } from '../../api/client'
import type { SignInApi } from '../../api/sign-in'

const returnUrl = 'http://site.test/auth/sign-in?returnTo=/'

function fakeApi(providers: () => Promise<string[] | undefined>): ExternalSignInApi {
  return {
    providers: vi.fn(providers),
    startUrl: (provider, back, link) =>
      `/start/${provider}?returnUrl=${encodeURIComponent(back)}${link ? '&link' : ''}`,
  }
}

describe('ExternalSignIn', () => {
  it('says the providers are loading, then shows a link to each in the API order', async () => {
    let finish: (list: string[]) => void = () => {}
    const wrapper = mount(ExternalSignIn, {
      props: { api: fakeApi(() => new Promise((resolve) => (finish = resolve))), returnUrl },
    })
    expect(wrapper.get('[role="status"]').text()).toBe('Загружаем способы входа…')
    expect(wrapper.get('[data-testid="external-sign-in"]').attributes('aria-busy')).toBe('true')

    finish(['yandex', 'vk', 'google', 'telegram'])
    await flushPromises()

    const links = wrapper.findAll('a')
    expect(links.map((a) => a.text())).toEqual(['Яндекс', 'VK', 'Google', 'Telegram'])
    expect(links[0]!.attributes('href')).toBe(`/start/yandex?returnUrl=${encodeURIComponent(returnUrl)}`)
    expect(links[2]!.attributes('aria-label')).toBe('Войти через Google')
  })

  it('is not shown at all when no provider is turned on', async () => {
    const wrapper = mount(ExternalSignIn, { props: { api: fakeApi(async () => []), returnUrl } })
    await flushPromises()
    expect(wrapper.find('[data-testid="external-sign-in"]').exists()).toBe(false)
  })

  it('says so when the list cannot be loaded, and shows the error of the last attempt', async () => {
    const wrapper = mount(ExternalSignIn, {
      props: { api: fakeApi(async () => undefined), returnUrl, error: externalErrorMessage('denied') },
    })
    await flushPromises()
    expect(wrapper.find('a').exists()).toBe(false)
    expect(wrapper.get('[role="status"]').text()).toContain('Не удалось загрузить другие способы входа')
    expect(wrapper.get('[role="alert"]').text()).toBe('Вход отменён у провайдера.')
  })

  it('is offered by the sign-in form only when given the providers', async () => {
    const signIn = {} as SignInApi
    const wrapper = mount(SignInForm, {
      props: { api: signIn, external: fakeApi(async () => ['google']), externalReturnUrl: returnUrl },
      global: { stubs: { NuxtLink: { template: '<a><slot /></a>' } } },
    })
    await flushPromises()
    expect(wrapper.find('[data-provider="google"]').exists()).toBe(true)
    expect(
      mount(SignInForm, { props: { api: signIn }, global: { stubs: { NuxtLink: true } } })
        .find('[data-testid="external-sign-in"]')
        .exists(),
    ).toBe(false)
  })
})

describe('external sign-in calls', () => {
  it('starts at the API origin, with the return address and the link flag', () => {
    const api = externalSignInApi({} as ApiClient, 'http://api.test')
    expect(api.startUrl('vk', 'http://site.test/settings?x=1')).toBe(
      'http://api.test/api/v1/sign-in/external/vk?returnUrl=http%3A%2F%2Fsite.test%2Fsettings%3Fx%3D1',
    )
    expect(api.startUrl('google', '/settings', true)).toBe(
      'http://api.test/api/v1/sign-in/external/google?returnUrl=%2Fsettings&link=true',
    )
  })

  it('loads the providers, and undefined when the call fails', async () => {
    const ok = { GET: vi.fn(async () => ({ data: [{ key: 'yandex' }] })) } as unknown as ApiClient
    expect(await externalSignInApi(ok, '').providers()).toEqual(['yandex'])
    const failing = { GET: vi.fn(async () => Promise.reject(new Error('offline'))) } as unknown as ApiClient
    expect(await externalSignInApi(failing, '').providers()).toBeUndefined()
  })

  it('names every error code, an unknown one as a failure, and none without a code', () => {
    expect(externalErrorMessage('email_required')).toContain('подтверждённую почту')
    expect(externalErrorMessage('something-new')).toBe(externalErrorMessage('failed'))
    expect(externalErrorMessage(undefined)).toBeUndefined()
    expect(externalErrorMessage('')).toBeUndefined()
  })

  it('hands the fragment of Telegram Login to the callback as its query', () => {
    const data = { id: 100500, first_name: 'Иван', auth_date: 1700000000, hash: 'ab'.repeat(32) }
    const bytes = new TextEncoder().encode(JSON.stringify(data))
    const encoded = btoa(String.fromCharCode(...bytes))
    const query = new URLSearchParams(telegramCallbackQuery(`#tgAuthResult=${encoded}`, '')!)
    expect(Object.fromEntries(query)).toEqual({
      id: '100500',
      first_name: 'Иван',
      auth_date: '1700000000',
      hash: 'ab'.repeat(32),
    })
    expect(telegramCallbackQuery('', '?id=1&hash=x')).toBe('id=1&hash=x')
    expect(telegramCallbackQuery('#tgAuthResult=not-json', '')).toBeUndefined()
    expect(telegramCallbackQuery('', '')).toBeUndefined()
  })
})
