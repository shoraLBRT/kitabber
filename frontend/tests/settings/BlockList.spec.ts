import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import BlockList from '../../components/settings/BlockList.vue'
import type { BlockedAccount, BlocksApi, BlocksOutcome } from '../../api/blocks'
import type { Profile } from '../../api/profiles'

const boris: BlockedAccount = {
  accountId: '0195f3a0-0000-7000-8000-000000000002',
  blockedAt: '2026-11-25T10:00:00Z',
}
const nameless: BlockedAccount = {
  accountId: '0195f3a0-0000-7000-8000-000000000009',
  blockedAt: '2026-11-20T10:00:00Z',
}
const borisBlog = { id: 'b', name: 'Борис', nickname: 'boris' } as Profile

function fakeApi(overrides: Partial<BlocksApi> = {}): BlocksApi {
  return {
    load: vi.fn(async () => ({ status: 200, body: { accounts: [boris, nameless], max: 1000 } })),
    block: vi.fn(async () => ({ status: 204 })),
    unblock: vi.fn(async () => ({ status: 204 })),
    ...overrides,
  }
}

const profiles = vi.fn(async () => new Map<string, Profile | null>([[boris.accountId, borisBlog]]))

let wrapper: VueWrapper | undefined

afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

async function mounted(api: BlocksApi) {
  wrapper = mount(BlockList, {
    props: { api, profiles },
    attachTo: document.body,
    global: { stubs: { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } } },
  })
  await flushPromises()
  return wrapper
}

const rows = (w: VueWrapper) => w.findAll('[data-testid="blocked-account"]')

describe('BlockList', () => {
  it('shows loading until the list is read, then each account with its blog name and the date', async () => {
    let finish: (outcome: BlocksOutcome) => void = () => {}
    const load = vi.fn(() => new Promise<BlocksOutcome>((resolve) => (finish = resolve)))
    const w = await mounted(fakeApi({ load }))
    expect(w.find('[data-testid="block-list-loading"]').exists()).toBe(true)
    expect(w.get('[data-testid="block-list"]').attributes('aria-busy')).toBe('true')

    finish({ status: 200, body: { accounts: [boris, nameless], max: 1000 } })
    await flushPromises()
    expect(rows(w)).toHaveLength(2)
    expect(rows(w)[0]!.text()).toContain('Борис')
    expect(rows(w)[0]!.text()).toContain('25 ноября 2026')
    expect(rows(w)[0]!.get('a').attributes('href')).toBe('/u/boris')
    expect(rows(w)[1]!.text()).toContain('Пользователь без блога')
  })

  it('says the list is empty', async () => {
    const w = await mounted(
      fakeApi({ load: vi.fn(async () => ({ status: 200, body: { accounts: [], max: 1000 } })) }),
    )

    expect(rows(w)).toHaveLength(0)
    expect(w.get('[data-testid="block-list-empty"]').text()).toContain('никого нет')
  })

  it('offers «Повторить» when the list could not be read, and reads it again', async () => {
    const load = vi
      .fn<BlocksApi['load']>()
      .mockResolvedValueOnce({ status: 500 })
      .mockResolvedValueOnce({ status: 200, body: { accounts: [boris], max: 1000 } })
    const w = await mounted(fakeApi({ load }))
    expect(w.get('[role="alert"]').text()).toContain('Не удалось загрузить')

    await w.get('button').trigger('click')
    await flushPromises()
    expect(rows(w)).toHaveLength(1)
  })

  it('unblocks an account and says so, and keeps it when the server refuses', async () => {
    const unblock = vi
      .fn<BlocksApi['unblock']>()
      .mockResolvedValueOnce({ status: 500 })
      .mockResolvedValueOnce({ status: 204 })
    const w = await mounted(fakeApi({ unblock }))
    const button = () => w.get('button[aria-label="Разблокировать Борис"]')

    await button().trigger('click')
    await flushPromises()
    expect(w.get('[role="alert"]').text()).toContain('Не удалось разблокировать')
    expect(rows(w)).toHaveLength(2)

    await button().trigger('click')
    await flushPromises()
    expect(unblock).toHaveBeenLastCalledWith(boris.accountId)
    expect(rows(w)).toHaveLength(1)
    expect(w.get('[role="status"]').text()).toBe('Борис больше не в чёрном списке.')
  })
})
