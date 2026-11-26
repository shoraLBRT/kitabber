import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import SessionList from '../../components/settings/SessionList.vue'
import type { AccountSession, SessionsApi, SessionsOutcome } from '../../api/sessions'

const here: AccountSession = {
  id: '01930000-0000-7000-8000-000000000001',
  device: 'Chrome, Windows',
  createdAt: '2026-11-20T10:00:00Z',
  lastActiveAt: '2026-11-26T09:30:00Z',
  current: true,
}
const phone: AccountSession = {
  id: '01930000-0000-7000-8000-000000000002',
  device: 'Safari, iOS',
  createdAt: '2026-11-18T08:00:00Z',
  lastActiveAt: '2026-11-25T21:15:00Z',
  current: false,
}
const unknown: AccountSession = { ...phone, id: '01930000-0000-7000-8000-000000000003', device: null }

function fakeApi(overrides: Partial<SessionsApi> = {}): SessionsApi {
  return {
    load: vi.fn(async () => ({ status: 200, body: [here, phone, unknown] })),
    end: vi.fn(async () => ({ status: 204 })),
    endOthers: vi.fn(async () => ({ status: 200, body: { ended: 2 } })),
    ...overrides,
  }
}

let wrapper: VueWrapper | undefined

afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

async function mounted(api: SessionsApi) {
  wrapper = mount(SessionList, {
    props: { api },
    attachTo: document.body,
    global: { stubs: { teleport: true } },
  })
  await flushPromises()
  return wrapper
}

const rows = (w: VueWrapper) => w.findAll('[data-testid="session"]')

describe('SessionList', () => {
  it('shows loading until the devices are read, then each with its last activity and this one marked', async () => {
    let finish: (outcome: SessionsOutcome) => void = () => {}
    const load = vi.fn(() => new Promise<SessionsOutcome>((resolve) => (finish = resolve)))
    const w = await mounted(fakeApi({ load }))
    expect(w.find('[data-testid="sessions-loading"]').exists()).toBe(true)
    expect(w.get('[data-testid="sessions"]').attributes('aria-busy')).toBe('true')

    finish({ status: 200, body: [here, phone, unknown] })
    await flushPromises()
    expect(rows(w)).toHaveLength(3)
    const [current, other, nameless] = rows(w)
    expect(current!.text()).toContain('Chrome, Windows')
    expect(current!.text()).toContain('Это устройство')
    expect(current!.text()).toContain('Последняя активность: 26 ноября 2026')
    expect(current!.find('button').exists()).toBe(false)
    expect(other!.text()).toContain('Safari, iOS')
    expect(other!.text()).not.toContain('Это устройство')
    expect(other!.get('button').attributes('aria-label')).toBe('Выйти на устройстве «Safari, iOS»')
    expect(nameless!.text()).toContain('Неизвестное устройство')
  })

  it('offers «Повторить» when the devices could not be read', async () => {
    const load = vi.fn<SessionsApi['load']>().mockResolvedValueOnce({ status: 0 })
    load.mockResolvedValueOnce({ status: 200, body: [here] })
    const w = await mounted(fakeApi({ load }))
    expect(w.get('[role="alert"]').text()).toContain('Не удалось загрузить устройства')

    await w.get('button').trigger('click')
    await flushPromises()
    expect(load).toHaveBeenCalledTimes(2)
    expect(rows(w)).toHaveLength(1)
  })

  it('says when this is the only device and offers nothing to sign out', async () => {
    const w = await mounted(fakeApi({ load: vi.fn(async () => ({ status: 200, body: [here] })) }))
    expect(w.get('[data-testid="sessions-only-this"]').text()).toContain('Других устройств нет')
    expect(w.text()).not.toContain('Выйти со всех устройств')
  })

  it('signs one device out and takes it off the list', async () => {
    const api = fakeApi()
    const w = await mounted(api)
    await rows(w)[1]!.get('button').trigger('click')
    await flushPromises()
    expect(api.end).toHaveBeenCalledWith(phone.id)
    expect(
      rows(w)
        .map((r) => r.text())
        .join(),
    ).not.toContain('Safari, iOS')
    expect(w.get('[role="status"]').text()).toBe('Устройство отключено.')
  })

  it('keeps the device and says so when signing it out fails', async () => {
    const w = await mounted(fakeApi({ end: vi.fn(async () => ({ status: 0 })) }))
    await rows(w)[1]!.get('button').trigger('click')
    await flushPromises()
    expect(rows(w)).toHaveLength(3)
    expect(w.get('[role="alert"]').text()).toContain('Не удалось выйти на устройстве')
  })

  it('signs out every other device only after the confirmation', async () => {
    const api = fakeApi()
    const w = await mounted(api)
    const button = w.findAll('button').find((b) => b.text() === 'Выйти со всех устройств')!
    await button.trigger('click')
    await flushPromises()
    expect(document.body.textContent).toContain('Выйти со всех устройств?')
    expect(api.endOthers).not.toHaveBeenCalled()

    await w.get('[data-testid="sign-out-others-confirm"]').trigger('click')
    await flushPromises()
    expect(api.endOthers).toHaveBeenCalledOnce()
    expect(rows(w)).toHaveLength(1)
    expect(rows(w)[0]!.text()).toContain('Это устройство')
    expect(w.get('[role="status"]').text()).toBe('Вы вышли на всех устройствах, кроме этого.')
  })

  it('says the session ended when the API no longer knows the person', async () => {
    const w = await mounted(fakeApi({ endOthers: vi.fn(async () => ({ status: 401 })) }))
    await w
      .findAll('button')
      .find((b) => b.text() === 'Выйти со всех устройств')!
      .trigger('click')
    await w.get('[data-testid="sign-out-others-confirm"]').trigger('click')
    await flushPromises()
    expect(rows(w)).toHaveLength(3)
    expect(w.get('[role="alert"]').text()).toBe('Сессия закончилась. Войдите снова.')
  })
})
