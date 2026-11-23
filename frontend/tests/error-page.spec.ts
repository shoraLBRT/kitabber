import { mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import ErrorPage from '../error.vue'

// The error page (E02-07, FR-UI, ADR-0057): 404 for an address no page takes, «Что-то пошло не так» for the rest. The
// status codes of the server render are checked in e2e/states.spec.ts.
const clearError = vi.fn()
const useHead = vi.fn()

beforeEach(() => {
  vi.stubGlobal('clearError', clearError)
  vi.stubGlobal('useHead', useHead)
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.clearAllMocks()
})

const stubs = {
  NuxtLayout: { template: '<div data-testid="layout"><slot /></div>' },
  UiToaster: true,
  AuthWindowHost: true,
  FeedQuickPostSheet: true,
}

const mountPage = (statusCode: number) =>
  mount(ErrorPage, {
    props: { error: { statusCode, statusMessage: '', message: '' } as never },
    global: { stubs },
  })

const title = () => (useHead.mock.calls[0]![0] as () => { title: string })().title

describe('error.vue', () => {
  it('shows «Страница не найдена» in the shell for 404, with a way to the main page', async () => {
    const page = mountPage(404)
    const found = page.get('[data-testid="layout"] [data-testid="not-found-page"]')
    expect(found.get('h1').text()).toBe('Страница не найдена')
    expect(found.find('[role="alert"]').exists()).toBe(false)
    expect(title()).toBe('Страница не найдена')

    await found.get('a[href="/"]').trigger('click')
    expect(clearError).toHaveBeenCalledWith({ redirect: '/' })
  })

  it('shows a failure with «Повторить» and the main page for 500', async () => {
    const reload = vi.fn()
    vi.stubGlobal('reloadNuxtApp', reload)
    const page = mountPage(500)
    const failure = page.get('[data-testid="error-page"]')
    expect(failure.get('h1').text()).toBe('Что-то пошло не так')
    expect(failure.get('[role="alert"]').text()).toContain('Попробуйте ещё раз')
    expect(title()).toBe('Ошибка')

    await failure.get('button').trigger('click')
    expect(reload).toHaveBeenCalled()
    await failure.get('a[href="/"]').trigger('click')
    expect(clearError).toHaveBeenCalledWith({ redirect: '/' })
  })
})
