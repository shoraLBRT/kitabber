import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import PostMenu from '../../components/post/PostMenu.vue'
import { useToast } from '../../composables/useToast'
import { noPostMenuFeatures, postMenuItems, type PostMenuFeatures } from '../../utils/post-menu'

// The «…» menu of a post (E07-05, ADR-0053): its items by role and by flag, the confirmation before deleting, a failed
// deletion, copying the link, and the bottom sheet on a phone. The page against the fake API is in e2e/post-menu.spec.ts.
const allOn: PostMenuFeatures = { postEdit: true, postReport: true }
const postId = '0195f3a0-0000-7000-8000-0000000000c1'
const path = '/u/anna/0195f3a0-0000-7000-8000-0000000000c1-privet-mir'

const phone = (matches: boolean) =>
  vi.spyOn(window, 'matchMedia').mockImplementation(
    (query) =>
      ({
        matches,
        media: query,
        addEventListener: () => {},
        removeEventListener: () => {},
      }) as unknown as MediaQueryList,
  )

let wrapper: ReturnType<typeof mount>
const mountMenu = (props: InstanceType<typeof PostMenu>['$props']) => {
  wrapper = mount(PostMenu, { props, attachTo: document.body, global: { stubs: { teleport: true } } })
  return wrapper
}
const open = async () => {
  await flushPromises()
  await wrapper.get('button[aria-label="Меню поста"]').trigger('click')
  await flushPromises()
}
const labels = () => wrapper.findAll('[role="menuitem"]').map((item) => item.text())
const choose = async (id: string) => {
  await wrapper.get(`[data-id="${id}"]`).trigger('click')
  await flushPromises()
}
const dialog = () => wrapper.find('[role="dialog"]')

afterEach(() => {
  wrapper?.unmount()
  vi.restoreAllMocks()
  useToast().clear()
})

describe('postMenuItems', () => {
  it('gives the author edit and delete, a reader report, hide and the link', () => {
    expect(postMenuItems(true, allOn, true, true).map((item) => item.id)).toEqual(['edit', 'delete'])
    expect(postMenuItems(false, allOn, false, true).map((item) => item.id)).toEqual([
      'report',
      'hide',
      'copy-link',
    ])
  })

  it('hides the items of features that are not built behind their flags', () => {
    expect(postMenuItems(true, noPostMenuFeatures).map((item) => item.id)).toEqual(['delete'])
    expect(postMenuItems(false, noPostMenuFeatures).map((item) => item.id)).toEqual(['copy-link'])
  })

  it('offers «Скрыть» only to a reader who may hide the post (E11-07)', () => {
    expect(postMenuItems(false, noPostMenuFeatures, false, true).map((item) => item.id)).toEqual([
      'hide',
      'copy-link',
    ])
    expect(postMenuItems(false, noPostMenuFeatures, false, false).map((item) => item.id)).toEqual([
      'copy-link',
    ])
  })

  it('never offers promotion, donations or gifts', () => {
    const labels = [...postMenuItems(true, allOn), ...postMenuItems(false, allOn)].map((item) => item.label)
    expect(labels.join(' ')).not.toMatch(/продвин|донат|подар/i)
  })
})

describe('PostMenu', () => {
  it('shows the author their items and a reader theirs', async () => {
    phone(false)
    mountMenu({ own: true, path, features: allOn })
    await open()
    expect(labels()).toEqual(['Редактировать', 'Удалить'])
    wrapper.unmount()

    mountMenu({ own: false, path, features: allOn, hidePostId: postId })
    await open()
    expect(labels()).toEqual(['Пожаловаться', 'Скрыть', 'Скопировать ссылку'])
  })

  it('keeps the flagged items hidden by default', async () => {
    phone(false)
    mountMenu({ own: false, path })
    await open()
    expect(labels()).toEqual(['Скопировать ссылку'])
  })

  it('asks before deleting, and cancelling keeps the post', async () => {
    phone(false)
    const remove = vi.fn().mockResolvedValue(true)
    mountMenu({ own: true, path, remove })
    await open()
    await choose('delete')
    expect(dialog().text()).toContain('Удалить пост?')
    expect(remove).not.toHaveBeenCalled()

    await wrapper.get('[role="dialog"] footer button:not([data-testid])').trigger('click')
    await flushPromises()
    expect(dialog().exists()).toBe(false)
    expect(remove).not.toHaveBeenCalled()

    await open()
    await choose('delete')
    await wrapper.get('[data-testid="post-delete-confirm"]').trigger('click')
    await flushPromises()
    expect(remove).toHaveBeenCalledTimes(1)
    expect(dialog().exists()).toBe(false)
  })

  it('keeps the dialog and says so when deleting fails', async () => {
    phone(false)
    const remove = vi.fn().mockResolvedValue(false)
    mountMenu({ own: true, path, remove })
    await open()
    await choose('delete')
    await wrapper.get('[data-testid="post-delete-confirm"]').trigger('click')
    await flushPromises()
    expect(dialog().exists()).toBe(true)
    expect(useToast().toasts.map((toast) => toast.message)).toContain('Не удалось удалить пост')
  })

  it('copies the link of the post', async () => {
    phone(false)
    const writeText = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true })
    mountMenu({ own: false, path })
    await open()
    await choose('copy-link')
    expect(writeText).toHaveBeenCalledWith(`${window.location.origin}${path}`)
    expect(useToast().toasts.map((toast) => toast.message)).toContain('Ссылка скопирована')
  })

  it('passes a flagged item on to its task', async () => {
    phone(false)
    mountMenu({ own: false, path, features: allOn })
    await open()
    await choose('report')
    expect(wrapper.emitted('action')).toEqual([['report']])
  })

  it('hides the post from the reader’s feeds and says so, or says that it failed', async () => {
    phone(false)
    const hide = vi.fn().mockResolvedValueOnce({ status: 204 }).mockResolvedValueOnce({ status: 500 })
    const hidden = { load: vi.fn(), topicState: vi.fn(), hide, show: vi.fn() }
    mountMenu({ own: false, path, hidePostId: postId, hidden })
    await open()
    await choose('hide')
    expect(hide).toHaveBeenCalledWith('posts', postId)
    expect(useToast().toasts.map((toast) => toast.message)).toContain(
      'Пост скрыт из ваших лент. Вернуть его можно в настройках.',
    )
    expect(wrapper.emitted('action')).toBeUndefined()

    await open()
    await choose('hide')
    expect(useToast().toasts.map((toast) => toast.message)).toContain(
      'Не удалось скрыть. Попробуйте ещё раз.',
    )
  })

  it('opens a bottom sheet on a phone', async () => {
    phone(true)
    mountMenu({ own: true, path })
    await open()
    const sheet = wrapper.get('[role="dialog"]')
    expect(sheet.text()).toContain('Меню поста')
    expect(wrapper.get('[data-testid="post-menu-sheet"]').text()).toContain('Удалить')
    await choose('delete')
    expect(wrapper.get('[role="dialog"]').text()).toContain('Удалить пост?')
  })
})
