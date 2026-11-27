import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import CommentTree from '../../components/comments/CommentTree.vue'
import type { CommentsState } from '../../api/comments'
import type { CommentFailure } from '../../components/comments/context'
import { commentDraftKey } from '../../utils/comment-draft'
import { comment } from './factory'

// The comment form and the reply form (E09-03, docs/ui/comments.md, ADR-0056) inside the tree: the guest's press asks
// for the sign-in, the draft is kept and restored, a send empties the form at once, a comment being sent is dimmed and
// a failed send gives the text back with its reason. The page against the fake API is in e2e/comment-form.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

const found = (comments = [comment('1', null, 1), comment('2', null, 1)]): CommentsState => ({
  status: 'found',
  comments,
  maxDepth: 3,
})

function mountTree(
  options: {
    state?: CommentsState
    signedIn?: boolean
    pending?: Record<string, true>
    failures?: Record<string, CommentFailure>
    resume?: string | null
  } = {},
) {
  return mount(CommentTree, {
    props: {
      postId: 'p1',
      signedIn: options.signedIn ?? true,
      state: options.state ?? found(),
      authors: new Map(),
      branches: {},
      pending: options.pending ?? {},
      failures: options.failures ?? {},
      resume: options.resume ?? null,
    },
    global: { stubs },
    attachTo: document.body,
  })
}

afterEach(() => {
  localStorage.clear()
  document.body.innerHTML = ''
})

describe('CommentForm', () => {
  it('is the folded field «Комментарий…» that opens on a press', async () => {
    const view = mountTree()
    const folded = view.get('[data-testid="comment-form-open"]')
    expect(folded.text()).toBe('Комментарий…')
    expect(view.find('textarea').exists()).toBe(false)

    await folded.trigger('click')
    const field = view.get('textarea')
    expect(field.attributes('aria-invalid')).toBeUndefined()
    expect(document.activeElement).toBe(field.element)
    expect(view.get('[data-testid="comment-form-send"]').attributes('disabled')).toBeDefined()
  })

  it('opens at once under a post with no comments', () => {
    const view = mountTree({ state: found([]) })
    expect(view.find('textarea').exists()).toBe(true)
    expect(view.get('[data-testid="comments-empty"]').text()).toBe('Комментариев пока нет — будьте первым')
  })

  it('asks a guest to sign in instead of opening, under the post and under a comment', async () => {
    const view = mountTree({ signedIn: false, state: found([]) })
    expect(view.find('textarea').exists()).toBe(false)
    await view.get('[data-testid="comment-form-open"]').trigger('click')
    expect(view.emitted('sign-in')).toEqual([['post']])

    const tree = mountTree({ signedIn: false })
    await tree.get('[data-comment-id="1"] [data-testid="comment-reply"]').trigger('click')
    expect(tree.emitted('sign-in')).toEqual([['1']])
    expect(tree.find('textarea').exists()).toBe(false)
  })

  it('opens the form the person came back to after the sign-in', () => {
    expect(mountTree({ resume: 'post' }).find('textarea').exists()).toBe(true)
    const reply = mountTree({ resume: '2' })
    expect(reply.find('[data-comment-id="2"] textarea').exists()).toBe(true)
  })

  it('keeps the text in a draft and opens with it again', async () => {
    const view = mountTree()
    await view.get('[data-testid="comment-form-open"]').trigger('click')
    await view.get('textarea').setValue('Черновик')
    expect(localStorage.getItem(commentDraftKey('p1', null))).toBe('Черновик')
    view.unmount()

    const again = mountTree()
    expect((again.get('textarea').element as HTMLTextAreaElement).value).toBe('Черновик')
    await again.get('textarea').setValue('   ')
    expect(localStorage.getItem(commentDraftKey('p1', null))).toBeNull()
  })

  it('sends the text, empties and folds the form, the draft kept until the server keeps it', async () => {
    const view = mountTree()
    await view.get('[data-testid="comment-form-open"]').trigger('click')
    await view.get('textarea').setValue('Новый комментарий')
    await view.get('form').trigger('submit')
    expect(view.emitted('send')).toEqual([['Новый комментарий', null]])
    expect(view.find('textarea').exists()).toBe(false)
    expect(localStorage.getItem(commentDraftKey('p1', null))).toBe('Новый комментарий')
  })

  it('sends on Ctrl+Enter and not a blank text', async () => {
    const view = mountTree()
    await view.get('[data-testid="comment-form-open"]').trigger('click')
    await view.get('textarea').setValue('  ')
    await view.get('textarea').trigger('keydown', { key: 'Enter', ctrlKey: true })
    expect(view.emitted('send')).toBeUndefined()
    await view.get('textarea').setValue('Быстро')
    await view.get('textarea').trigger('keydown', { key: 'Enter', ctrlKey: true })
    expect(view.emitted('send')).toEqual([['Быстро', null]])
  })

  it('replies under a comment and closes the reply form', async () => {
    const view = mountTree()
    await view.get('[data-comment-id="1"] [data-testid="comment-reply"]').trigger('click')
    const form = view.get('[data-comment-id="1"] [data-testid="comment-form"]')
    expect(document.activeElement).toBe(form.get('textarea').element)
    await form.get('textarea').setValue('Ответ')
    await form.get('form').trigger('submit')
    expect(view.emitted('send')).toEqual([['Ответ', '1']])
    expect(view.find('[data-comment-id="1"] [data-testid="comment-form"]').exists()).toBe(false)
  })

  it('closes the reply form on «Отмена», keeping the draft', async () => {
    const view = mountTree()
    await view.get('[data-comment-id="2"] [data-testid="comment-reply"]').trigger('click')
    await view.get('[data-comment-id="2"] textarea').setValue('Потом допишу')
    await view.get('[data-testid="comment-form-cancel"]').trigger('click')
    expect(view.find('[data-comment-id="2"] textarea').exists()).toBe(false)
    expect(localStorage.getItem(commentDraftKey('p1', '2'))).toBe('Потом допишу')
  })

  it('offers no reply to a guest comment, at the deepest level or to a comment being sent', () => {
    const guest = {
      ...comment('g', null, 1),
      kind: 'guest',
      authorAccountId: null,
      guest: { name: 'Гость', url: null },
    }
    const view = mountTree({
      state: found([
        guest,
        comment('d', null, 1, 2),
        comment('d.1', 'd', 2, 1),
        comment('d.2', 'd.1', 3),
        comment('local-1', null, 1),
      ]),
      pending: { 'local-1': true },
    })
    const replies = view.findAll('[data-testid="comment-reply"]')
    expect(replies.map((button) => button.element.closest('li')?.dataset.commentId)).toEqual(['d', 'd.1'])
  })

  it('dims a comment being sent and names its author «Вы»', () => {
    const sending = { ...comment('local-1', null, 1), authorAccountId: null }
    const view = mountTree({ state: found([sending]), pending: { 'local-1': true } })
    const item = view.get('[data-comment-id="local-1"]')
    expect(item.classes()).toContain('comment--sending')
    expect(item.attributes('aria-busy')).toBe('true')
    expect(item.get('[data-testid="comment-author"]').text()).toBe('Вы')
    expect(item.get('[data-testid="comment-sending"]').text()).toBe('Отправляется…')
  })

  it('gives a failed text back with the reason and forgets the reason once edited', async () => {
    const failures = { post: { text: 'Не дошло', message: 'Не удалось отправить комментарий.' } }
    const view = mountTree({ failures })
    const field = view.get('textarea')
    expect((field.element as HTMLTextAreaElement).value).toBe('Не дошло')
    expect(field.attributes('aria-invalid')).toBe('true')
    expect(view.get('[data-testid="comment-form-error"]').text()).toBe('Не удалось отправить комментарий.')
    await field.setValue('Не дошло!')
    expect(view.emitted('dismiss')).toEqual([['post']])
  })

  it('opens the reply form again when its send fails after it closed', async () => {
    const view = mountTree()
    expect(view.find('[data-comment-id="1"] textarea').exists()).toBe(false)
    await view.setProps({ failures: { '1': { text: 'Ответ', message: 'Ветка не глубже 3 уровней.' } } })
    const field = view.get('[data-comment-id="1"] textarea')
    expect((field.element as HTMLTextAreaElement).value).toBe('Ответ')
    expect(view.get('[data-comment-id="1"] [role="alert"]').text()).toBe('Ветка не глубже 3 уровней.')
  })

  // E11-06 (ADR-0091): the author blocked the person, so the form says so, keeps the text and sends no more.
  it('says the author blocked the person and does not send again', async () => {
    const failures = {
      '1': {
        text: 'Ответ',
        message: 'Автор заблокировал вас: отвечать ему и комментировать его посты нельзя.',
        blocked: true,
      },
    }
    const view = mountTree()
    await view.setProps({ failures })
    const form = view.get('[data-comment-id="1"] [data-testid="comment-form"]')
    expect(form.get('[data-testid="comment-form-blocked"]').text()).toBe(
      'Автор заблокировал вас: отвечать ему и комментировать его посты нельзя.',
    )
    expect(form.find('[data-testid="comment-form-error"]').exists()).toBe(false)
    expect((form.get('textarea').element as HTMLTextAreaElement).value).toBe('Ответ')
    expect(form.get('[data-testid="comment-form-send"]').attributes('disabled')).toBeDefined()
    await form.get('textarea').setValue('Ответ ещё раз')
    await form.get('form').trigger('submit')
    expect(view.emitted('dismiss')).toBeUndefined()
    expect(view.emitted('send')).toBeUndefined()
  })

  it('gives the text back to the folded form under the post when its send fails', async () => {
    const view = mountTree()
    await view.setProps({ failures: { post: { text: 'Сбой', message: 'Не удалось.' } } })
    expect((view.get('textarea').element as HTMLTextAreaElement).value).toBe('Сбой')
    expect(localStorage.getItem(commentDraftKey('p1', null))).toBe('Сбой')
  })
})
