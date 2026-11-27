import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import BlockDialog from '../../components/blocks/BlockDialog.vue'
import BlogBlock from '../../components/blog/BlogBlock.vue'
import BlogView from '../../components/blog/BlogView.vue'
import CommentTree from '../../components/comments/CommentTree.vue'
import PostMenu from '../../components/post/PostMenu.vue'
import type { BlocksApi } from '../../api/blocks'
import type { Profile } from '../../api/profiles'
import { useToast } from '../../composables/useToast'
import { postMenuItems, noPostMenuFeatures } from '../../utils/post-menu'
import { comment } from '../comments/factory'

// «Заблокировать» (E11-06, FR-SUB, ADR-0091): the dialog that asks first and blocks, the button on someone else's
// personal blog with «Разблокировать» once blocked, and the item of the «…» menus of someone else's post and comment.
// The pages against the fake API are in e2e/black-list.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' }, teleport: true }
const boris = '0195f3a0-0000-7000-8000-000000000002'

function fakeApi(overrides: Partial<BlocksApi> = {}): BlocksApi {
  return {
    load: vi.fn(async () => ({ status: 200 })),
    block: vi.fn(async () => ({ status: 204 })),
    unblock: vi.fn(async () => ({ status: 204 })),
    ...overrides,
  }
}

const anna: Profile = {
  id: '0195f3a0-0000-7000-8000-0000000000b1',
  type: 'personal',
  name: 'Анна',
  nickname: 'anna',
  description: null,
  registeredAt: null,
  avatarId: null,
  avatarUrl: null,
  avatarSources: [],
  subscriberCount: 0,
  subscribed: false,
  cover: null,
  accountId: boris,
  blocked: false,
}

let wrapper: VueWrapper | undefined
const toasts = () => useToast().toasts.map((toast) => toast.message)
const dialog = () => document.querySelector('[role="dialog"]')

afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
  useToast().clear()
  document.body.innerHTML = ''
})

describe('BlockDialog', () => {
  const mountDialog = (api: BlocksApi) => {
    wrapper = mount(BlockDialog, {
      props: { accountId: boris, name: 'Борис', api, open: true },
      attachTo: document.body,
      global: { stubs },
    })
    return wrapper
  }

  it('names the person, says what blocking does and blocks on «Заблокировать»', async () => {
    const api = fakeApi()
    const view = mountDialog(api)
    await flushPromises()
    expect(dialog()?.textContent).toContain('Заблокировать Борис?')
    expect(view.get('[data-testid="block-dialog-text"]').text()).toContain(
      'комментировать ваши посты он не сможет',
    )
    expect(api.block).not.toHaveBeenCalled()

    await view.get('[data-testid="block-confirm"]').trigger('click')
    await flushPromises()
    expect(api.block).toHaveBeenCalledWith(boris)
    expect(view.emitted('blocked')).toHaveLength(1)
    expect(view.emitted('update:open')).toEqual([[false]])
    expect(toasts()).toContain('Борис в чёрном списке.')
  })

  it('stays open with a toast when blocking fails, and asks to sign in again when the session ended', async () => {
    const api = fakeApi({
      block: vi.fn().mockResolvedValueOnce({ status: 500 }).mockResolvedValueOnce({ status: 401 }),
    })
    const view = mountDialog(api)
    await view.get('[data-testid="block-confirm"]').trigger('click')
    await flushPromises()
    expect(view.emitted('blocked')).toBeUndefined()
    expect(view.emitted('update:open')).toBeUndefined()
    expect(toasts()).toContain('Не удалось заблокировать. Попробуйте ещё раз.')

    await view.get('[data-testid="block-confirm"]').trigger('click')
    await flushPromises()
    expect(toasts()).toContain('Войдите снова, чтобы изменить чёрный список.')
  })

  it('goes through the site’s server by default', async () => {
    const raw = vi.fn(async () => ({ status: 204, _data: undefined }))
    vi.stubGlobal('$fetch', { raw })
    wrapper = mount(BlockDialog, {
      props: { accountId: boris, name: 'Борис', open: true },
      attachTo: document.body,
      global: { stubs },
    })
    await wrapper.get('[data-testid="block-confirm"]').trigger('click')
    await flushPromises()
    expect(raw).toHaveBeenCalledWith(`/bff/blocks/${boris}`, { method: 'PUT', ignoreResponseError: true })
  })
})

describe('BlogBlock', () => {
  const mountButton = (api: BlocksApi, blocked = false) => {
    wrapper = mount(BlogBlock, {
      props: { accountId: boris, name: 'Борис', blocked, api },
      attachTo: document.body,
      global: { stubs },
    })
    return wrapper
  }

  it('asks first, then blocks and offers «Разблокировать»', async () => {
    const api = fakeApi()
    const view = mountButton(api)
    expect(view.get('[data-testid="blog-block-button"]').text()).toBe('Заблокировать')
    expect(view.find('[data-testid="blog-blocked"]').exists()).toBe(false)

    await view.get('[data-testid="blog-block-button"]').trigger('click')
    await flushPromises()
    expect(api.block).not.toHaveBeenCalled()
    await view.get('[data-testid="block-confirm"]').trigger('click')
    await flushPromises()
    expect(api.block).toHaveBeenCalledWith(boris)
    expect(view.emitted('change')).toEqual([[true]])
    expect(view.get('[data-testid="blog-block-button"]').text()).toBe('Разблокировать')
    expect(view.get('[data-testid="blog-blocked"]').text()).toContain('Вы заблокировали этого человека')
  })

  it('unblocks a blocked owner at once, and says so when it fails', async () => {
    const api = fakeApi({
      unblock: vi.fn().mockResolvedValueOnce({ status: 503 }).mockResolvedValueOnce({ status: 204 }),
    })
    const view = mountButton(api, true)
    await view.get('[data-testid="blog-block-button"]').trigger('click')
    await flushPromises()
    expect(toasts()).toContain('Не удалось разблокировать. Попробуйте ещё раз.')
    expect(view.get('[data-testid="blog-block-button"]').text()).toBe('Разблокировать')

    await view.get('[data-testid="blog-block-button"]').trigger('click')
    await flushPromises()
    expect(api.unblock).toHaveBeenCalledWith(boris)
    expect(view.emitted('change')).toEqual([[false]])
    expect(toasts()).toContain('Борис больше не в чёрном списке.')
    expect(view.get('[data-testid="blog-block-button"]').text()).toBe('Заблокировать')
  })
})

describe('BlogView', () => {
  const mountView = (profile: Profile, owner: boolean, signedIn: boolean) =>
    mount(BlogView, {
      props: { state: { status: 'found', profile, owner }, base: '/u/anna', tab: 'posts', signedIn },
      global: { stubs },
    })

  it('offers «Заблокировать» only to a signed-in visitor of someone else’s personal blog', () => {
    const button = (view: VueWrapper) => view.find('[data-testid="blog-block-button"]')
    expect(button(mountView(anna, false, true)).text()).toBe('Заблокировать')
    expect(button(mountView({ ...anna, blocked: true }, false, true)).text()).toBe('Разблокировать')
    expect(button(mountView(anna, false, false)).exists()).toBe(false)
    expect(button(mountView(anna, true, true)).exists()).toBe(false)
    expect(button(mountView({ ...anna, type: 'topic', accountId: null }, false, true)).exists()).toBe(false)
  })
})

describe('PostMenu', () => {
  it('offers a reader who may block «Заблокировать автора», never the author', () => {
    expect(postMenuItems(false, noPostMenuFeatures, true).map((item) => item.id)).toEqual([
      'copy-link',
      'block',
    ])
    expect(postMenuItems(true, noPostMenuFeatures, true).map((item) => item.id)).toEqual(['delete'])
    expect(postMenuItems(false, noPostMenuFeatures).map((item) => item.id)).toEqual(['copy-link'])
  })

  it('asks before blocking the author of the post', async () => {
    vi.spyOn(window, 'matchMedia').mockImplementation(
      (query) =>
        ({
          matches: false,
          media: query,
          addEventListener: () => {},
          removeEventListener: () => {},
        }) as unknown as MediaQueryList,
    )
    const raw = vi.fn(async () => ({ status: 204, _data: undefined }))
    vi.stubGlobal('$fetch', { raw })
    wrapper = mount(PostMenu, {
      props: { own: false, path: '/u/boris/1', author: { accountId: boris, name: 'Борис' } },
      attachTo: document.body,
      global: { stubs },
    })
    await flushPromises()
    await wrapper.get('button[aria-label="Меню поста"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-id="block"]').trigger('click')
    await flushPromises()
    expect(dialog()?.textContent).toContain('Заблокировать Борис?')
    await wrapper.get('[data-testid="block-confirm"]').trigger('click')
    await flushPromises()
    expect(raw).toHaveBeenCalledWith(`/bff/blocks/${boris}`, { method: 'PUT', ignoreResponseError: true })
    expect(toasts()).toContain('Борис в чёрном списке.')
  })
})

describe('CommentTree', () => {
  it('blocks the author of someone else’s comment from its menu and says so to the page', async () => {
    vi.spyOn(window, 'matchMedia').mockImplementation(
      (query) =>
        ({
          matches: false,
          media: query,
          addEventListener: () => {},
          removeEventListener: () => {},
        }) as unknown as MediaQueryList,
    )
    const raw = vi.fn(async () => ({ status: 204, _data: undefined }))
    vi.stubGlobal('$fetch', { raw })
    const own = comment('1', null, 1, 0, 'me')
    const others = comment('2', null, 1, 0, boris)
    const props = {
      postId: 'p1',
      signedIn: true,
      state: { status: 'found' as const, comments: [own, others], maxDepth: 3 },
      authors: new Map(),
      branches: {},
      own: { '1': true as const },
      canBlock: true,
    }
    wrapper = mount(CommentTree, { props, attachTo: document.body, global: { stubs } })
    await flushPromises()
    const menu = (id: string) => wrapper!.find(`[data-comment-id="${id}"] [data-testid="comment-menu"]`)
    expect(menu('2').exists()).toBe(true)

    await menu('2').get('button[aria-label="Меню комментария"]').trigger('click')
    await flushPromises()
    const items = [...document.querySelectorAll('[role="menuitem"]')].map((item) => item.textContent?.trim())
    expect(items).toEqual(['Заблокировать автора'])
    await menu('2').get('[data-id="block"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="block-confirm"]').trigger('click')
    await flushPromises()
    expect(raw).toHaveBeenCalledWith(`/bff/blocks/${boris}`, { method: 'PUT', ignoreResponseError: true })
    expect(wrapper.emitted('blocked')).toEqual([[boris]])
  })

  it('offers no blocking before the own comments are known', async () => {
    const props = {
      postId: 'p1',
      signedIn: true,
      state: { status: 'found' as const, comments: [comment('2', null, 1, 0, boris)], maxDepth: 3 },
      authors: new Map(),
      branches: {},
    }
    wrapper = mount(CommentTree, { props, global: { stubs } })
    expect(wrapper.find('[data-testid="comment-menu"]').exists()).toBe(false)
  })
})
