import { flushPromises, mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import SignInMethods from '../../components/settings/SignInMethods.vue'
import type { ExternalSignInApi } from '../../api/external-sign-in'
import type { IdentitiesApi, IdentitiesOutcome } from '../../api/identities'

const returnUrl = 'http://site.test/settings/account'
const yandex = {
  id: '0195f3a0-0000-7000-8000-0000000000a1',
  provider: 'yandex',
  linkedAt: '2026-11-20T10:00:00Z',
}

function external(providers: string[] | undefined = ['yandex', 'vk']): ExternalSignInApi {
  return {
    providers: vi.fn(async () => providers),
    startUrl: (provider, back, link) => `/start/${provider}?returnUrl=${back}&link=${link}`,
  }
}

function identities(overrides: Partial<IdentitiesApi> = {}): IdentitiesApi {
  return {
    load: vi.fn(async (): Promise<IdentitiesOutcome> => ({ status: 200, body: [yandex] })),
    unlink: vi.fn(async (): Promise<IdentitiesOutcome> => ({ status: 204 })),
    ...overrides,
  }
}

function mountSection(api: IdentitiesApi, providers = external(), error?: string) {
  return mount(SignInMethods, { props: { api, external: providers, returnUrl, error } })
}

describe('SignInMethods', () => {
  it('shows a skeleton while loading, then each provider linked or with a link to link it', async () => {
    let finish: (outcome: IdentitiesOutcome) => void = () => {}
    const wrapper = mountSection(identities({ load: () => new Promise((resolve) => (finish = resolve)) }))
    expect(wrapper.find('[data-testid="sign-in-methods-loading"]').exists()).toBe(true)
    expect(wrapper.get('section').attributes('aria-busy')).toBe('true')

    finish({ status: 200, body: [yandex] })
    await flushPromises()

    const rows = wrapper.findAll('[data-testid="sign-in-method"]')
    expect(rows.map((r) => r.attributes('data-provider'))).toEqual(['yandex', 'vk'])
    expect(rows[0]!.text()).toContain('Привязан 20 ноября 2026')
    expect(rows[0]!.get('button').attributes('aria-label')).toBe('Отвязать Яндекс')
    expect(rows[1]!.text()).toContain('Не привязан')
    expect(rows[1]!.get('a').attributes('href')).toBe(`/start/vk?returnUrl=${returnUrl}&link=true`)
  })

  it('unlinks a provider, and says why the last way to sign in stays', async () => {
    const api = identities()
    const wrapper = mountSection(api)
    await flushPromises()
    await wrapper.get('button[aria-label="Отвязать Яндекс"]').trigger('click')
    await flushPromises()
    expect(api.unlink).toHaveBeenCalledWith(yandex.id)
    expect(wrapper.get('[role="status"]').text()).toBe('Яндекс отвязан.')
    expect(wrapper.find('button[aria-label="Отвязать Яндекс"]').exists()).toBe(false)

    const last = mountSection(identities({ unlink: vi.fn(async () => ({ status: 409 })) }))
    await flushPromises()
    await last.get('button[aria-label="Отвязать Яндекс"]').trigger('click')
    await flushPromises()
    expect(last.get('[role="alert"]').text()).toContain('последний способ входа')
    expect(last.find('button[aria-label="Отвязать Яндекс"]').exists()).toBe(true)
  })

  it('offers to retry when a list cannot be loaded, and says when nothing is connected', async () => {
    const api = identities({ load: vi.fn(async () => ({ status: 0 })) })
    const wrapper = mountSection(api)
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toBe('Не удалось загрузить способы входа.')
    await wrapper.get('button').trigger('click')
    expect(api.load).toHaveBeenCalledTimes(2)

    const empty = mountSection(
      identities({ load: vi.fn(async () => ({ status: 200, body: [] })) }),
      external([]),
    )
    await flushPromises()
    expect(empty.text()).toContain('Вход через другие сервисы на этом сайте пока не подключён.')
  })

  it('shows the error of a linking that failed, and a linked provider no longer turned on', async () => {
    const wrapper = mountSection(
      identities(),
      external(['vk']),
      'Этот аккаунт провайдера уже привязан к другому аккаунту.',
    )
    await flushPromises()
    expect(wrapper.findAll('[role="alert"]')[0]!.text()).toContain('уже привязан к другому аккаунту')
    const rows = wrapper.findAll('[data-testid="sign-in-method"]')
    expect(rows.map((r) => r.attributes('data-provider'))).toEqual(['vk', 'yandex'])
    expect(rows[1]!.find('button').exists()).toBe(true)
  })
})
