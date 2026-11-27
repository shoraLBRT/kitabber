import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import CommentTree from '../../components/comments/CommentTree.vue'
import { commentPath, type Comment, type CommentsState } from '../../api/comments'
import { useToast } from '../../composables/useToast'
import { comment } from './factory'

// The permanent link of a comment in the tree (E09-07, docs/ui/comments.md, ADR-0107): its time leads to the link,
// «Скопировать ссылку» copies it, and the comment a link named is highlighted. Reading the branch that holds a deep
// comment and bringing it into view is in e2e/comment-link.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' }, teleport: true }
const postPath = '/u/anna/p1-privet'

let wrapper: ReturnType<typeof mount>

function mountTree(
  comments: Comment[],
  extra: { highlighted?: string | null; linked?: boolean; signedIn?: boolean } = {},
) {
  const state: CommentsState = { status: 'found', comments, maxDepth: 8 }
  wrapper = mount(CommentTree, {
    props: {
      postId: 'p1',
      signedIn: extra.signedIn ?? false,
      state,
      authors: new Map(),
      branches: {},
      highlighted: extra.highlighted ?? null,
      commentLink: extra.linked === false ? null : (id: string) => commentPath(postPath, id),
    },
    attachTo: document.body,
    global: { stubs },
  })
  return wrapper
}

afterEach(() => {
  wrapper?.unmount()
  useToast().clear()
  vi.unstubAllGlobals()
})

describe('CommentTree: comment links', () => {
  it('links the time of a comment to the comment itself', async () => {
    mountTree([comment('1', null, 1), comment('1.1', '1', 2)])
    await flushPromises()
    const permalink = wrapper.get('[data-comment-id="1.1"] [data-testid="comment-permalink"]')
    expect(permalink.attributes('href')).toBe(`${postPath}#comment-1.1`)
    expect(permalink.find('time').exists()).toBe(true)
  })

  it('keeps a plain time when the page knows no link', async () => {
    mountTree([comment('1', null, 1)], { linked: false })
    await flushPromises()
    expect(wrapper.find('[data-testid="comment-permalink"]').exists()).toBe(false)
    expect(wrapper.find('[data-comment-id="1"] time').exists()).toBe(true)
    expect(wrapper.find('[data-testid="comment-menu"]').exists()).toBe(false)
  })

  it('highlights only the comment a link named', async () => {
    mountTree([comment('1', null, 1, 1), comment('1.1', '1', 2), comment('2', null, 1)], {
      highlighted: '1.1',
    })
    await flushPromises()
    const highlighted = wrapper.findAll('[data-highlighted="true"]')
    expect(highlighted.map((item) => item.attributes('data-comment-id'))).toEqual(['1.1'])
    expect(highlighted[0]!.classes()).toContain('comment--highlighted')
  })

  it('copies the full link of the comment for a guest reader and says so', async () => {
    const writeText = vi.fn(async () => {})
    vi.stubGlobal('navigator', { ...navigator, clipboard: { writeText } })
    mountTree([comment('1', null, 1, 0, 'a1')])
    await flushPromises()

    await wrapper.get('[data-comment-id="1"] button[aria-label="Меню комментария"]').trigger('click')
    await flushPromises()
    expect(
      [...document.querySelectorAll('[role="menuitem"]')].map((item) => item.textContent?.trim()),
    ).toEqual(['Скопировать ссылку'])
    await wrapper.get('[data-id="copy-link"]').trigger('click')
    await flushPromises()

    expect(writeText).toHaveBeenCalledWith(`${window.location.origin}${postPath}#comment-1`)
    expect(useToast().toasts.map((toast) => toast.message)).toEqual(['Ссылка скопирована'])
  })

  it('says so when the link could not be copied', async () => {
    vi.stubGlobal('navigator', {
      ...navigator,
      clipboard: { writeText: vi.fn(async () => Promise.reject(new Error('denied'))) },
    })
    mountTree([comment('1', null, 1, 0, 'a1')])
    await flushPromises()

    await wrapper.get('[data-comment-id="1"] button[aria-label="Меню комментария"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-id="copy-link"]').trigger('click')
    await flushPromises()

    expect(useToast().toasts.map((toast) => toast.message)).toEqual(['Не удалось скопировать ссылку'])
  })
})
