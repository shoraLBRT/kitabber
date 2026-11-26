import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import CommentTree from '../../components/comments/CommentTree.vue'
import type { Comment, CommentsState } from '../../api/comments'
import { useToast } from '../../composables/useToast'
import { comment } from './factory'

// Changing and deleting an own comment in the tree (E09-05, docs/ui/comments.md, ADR-0059): the «…» menu only on the
// person's own comments, «Редактировать» only while the time lasts, the form of the text with the server's refusal,
// the mark «изменено», and the stub «Комментарий удалён» over replies. The page against the fake API is in
// e2e/comment-edit.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' }, teleport: true }

let wrapper: ReturnType<typeof mount>

function mountTree(
  comments: Comment[],
  extra: {
    own?: Record<string, true>
    edit?: (id: string, text: string) => Promise<string | null>
    remove?: (id: string) => Promise<boolean>
  } = {},
) {
  const state: CommentsState = { status: 'found', comments, maxDepth: 8 }
  wrapper = mount(CommentTree, {
    props: { postId: 'p1', signedIn: true, state, authors: new Map(), branches: {}, ...extra },
    attachTo: document.body,
    global: { stubs },
  })
  return wrapper
}

const menuButton = (id: string) =>
  wrapper.find(`[data-comment-id="${id}"] button[aria-label="Меню комментария"]`)

async function choose(id: string, item: 'edit' | 'delete') {
  await menuButton(id).trigger('click')
  await flushPromises()
  await wrapper.get(`[data-id="${item}"]`).trigger('click')
  await flushPromises()
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(new Date('2026-11-20T12:05:00Z'))
})

afterEach(() => {
  wrapper?.unmount()
  vi.useRealTimers()
  useToast().clear()
})

describe('CommentTree: own comments', () => {
  it('gives the menu to the own comments only', async () => {
    mountTree([comment('1', null, 1), comment('2', null, 1, 0, 'a2')], { own: { '1': true } })
    await flushPromises()
    expect(menuButton('1').exists()).toBe(true)
    expect(menuButton('2').exists()).toBe(false)
  })

  it('offers only «Удалить» once the time to change the text is over', async () => {
    vi.setSystemTime(new Date('2026-11-20T12:20:00Z'))
    mountTree([comment('1', null, 1)], { own: { '1': true } })
    await flushPromises()
    await menuButton('1').trigger('click')
    await flushPromises()
    expect(wrapper.findAll('[role="menuitem"]').map((item) => item.text())).toEqual(['Удалить'])
  })

  it('changes the text in a form and closes it once the server keeps it', async () => {
    const edit = vi.fn(async () => null)
    mountTree([comment('1', null, 1)], { own: { '1': true }, edit })
    await flushPromises()
    await choose('1', 'edit')

    const field = wrapper.get('[data-testid="comment-edit-text"]')
    expect((field.element as HTMLTextAreaElement).value).toBe('Комментарий 1')
    expect(wrapper.find('[data-testid="comment-text"]').exists()).toBe(false)
    await field.setValue('Новый текст')
    await wrapper.get('[data-testid="comment-edit"]').trigger('submit')
    await flushPromises()

    expect(edit).toHaveBeenCalledWith('1', 'Новый текст')
    expect(wrapper.find('[data-testid="comment-edit"]').exists()).toBe(false)
  })

  it('keeps the form with the server’s refusal and the text written', async () => {
    const edit = vi.fn(async () => 'Время на правку вышло: комментарий можно только удалить.')
    mountTree([comment('1', null, 1)], { own: { '1': true }, edit })
    await flushPromises()
    await choose('1', 'edit')
    await wrapper.get('[data-testid="comment-edit-text"]').setValue('Поздно')
    await wrapper.get('[data-testid="comment-edit"]').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-testid="comment-edit-error"]').text()).toBe(
      'Время на правку вышло: комментарий можно только удалить.',
    )
    expect(wrapper.get('[data-testid="comment-edit-text"]').attributes('aria-invalid')).toBe('true')
    expect((wrapper.get('[data-testid="comment-edit-text"]').element as HTMLTextAreaElement).value).toBe(
      'Поздно',
    )
  })

  it('leaves the text as it was on «Отмена»', async () => {
    mountTree([comment('1', null, 1)], { own: { '1': true } })
    await flushPromises()
    await choose('1', 'edit')
    await wrapper.get('[data-testid="comment-edit-cancel"]').trigger('click')
    expect(wrapper.get('[data-testid="comment-text"]').text()).toBe('Комментарий 1')
  })

  it('asks before deleting and deletes on «Удалить»', async () => {
    const remove = vi.fn(async () => true)
    mountTree([comment('1', null, 1)], { own: { '1': true }, remove })
    await flushPromises()
    await choose('1', 'delete')

    expect(document.body.textContent).toContain('Удалить комментарий?')
    expect(remove).not.toHaveBeenCalled()
    await wrapper.get('[data-testid="comment-delete-confirm"]').trigger('click')
    await flushPromises()
    expect(remove).toHaveBeenCalledWith('1')
    expect(useToast().toasts.map((toast) => toast.message)).toContain('Комментарий удалён')
  })

  it('keeps the dialog open with a toast when the deletion fails', async () => {
    mountTree([comment('1', null, 1)], { own: { '1': true }, remove: async () => false })
    await flushPromises()
    await choose('1', 'delete')
    await wrapper.get('[data-testid="comment-delete-confirm"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="comment-delete-confirm"]').exists()).toBe(true)
    expect(useToast().toasts.map((toast) => toast.message)).toContain('Не удалось удалить комментарий')
  })
})

describe('CommentTree: changed and deleted comments', () => {
  it('marks a changed text «изменено»', () => {
    mountTree([{ ...comment('1', null, 1), editedAt: '2026-11-20T12:03:00Z' }, comment('2', null, 1)])
    expect(wrapper.get('[data-comment-id="1"] [data-testid="comment-edited"]').text()).toBe('· изменено')
    expect(wrapper.find('[data-comment-id="2"] [data-testid="comment-edited"]').exists()).toBe(false)
  })

  it('draws a deleted comment with replies as the stub over them, with no author, text or actions', () => {
    const stub = {
      ...comment('1', null, 1, 1),
      deleted: true,
      text: '',
      authorAccountId: null,
      editableUntil: null,
    }
    mountTree([stub, comment('1.1', '1', 2)], { own: { '1': true } })
    const item = wrapper.get('[data-comment-id="1"]')
    expect(item.get('[data-testid="comment-deleted"]').text()).toBe('Комментарий удалён')
    expect(item.find(':scope > .comment__head').exists()).toBe(false)
    expect(item.find(':scope > [data-testid="comment-text"]').exists()).toBe(false)
    expect(item.find(':scope > .comment__actions').exists()).toBe(false)
    expect(menuButton('1').exists()).toBe(false)
    expect(wrapper.get('[data-comment-id="1.1"] [data-testid="comment-text"]').text()).toBe('Комментарий 1.1')
  })
})

describe('CommentTree: comments of blocked authors (E11-06)', () => {
  it('folds a blocked author’s comment with «Показать» and shows its text on demand', async () => {
    mountTree([{ ...comment('1', null, 1), authorBlocked: true }, comment('2', null, 1)])
    const item = wrapper.get('[data-comment-id="1"]')
    expect(item.get('[data-testid="comment-folded"]').text()).toContain('вы заблокировали автора')
    expect(item.find('[data-testid="comment-text"]').exists()).toBe(false)
    expect(item.find(':scope > .comment__actions').exists()).toBe(false)
    expect(wrapper.get('[data-comment-id="2"] [data-testid="comment-text"]').text()).toBe('Комментарий 2')

    await item.get('[data-testid="comment-reveal"]').trigger('click')
    expect(wrapper.get('[data-comment-id="1"] [data-testid="comment-text"]').text()).toBe('Комментарий 1')
    expect(wrapper.find('[data-comment-id="1"] [data-testid="comment-folded"]').exists()).toBe(false)
  })
})
