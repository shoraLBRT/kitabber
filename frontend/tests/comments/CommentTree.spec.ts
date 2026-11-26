import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import CommentTree from '../../components/comments/CommentTree.vue'
import type { CommentsState } from '../../api/comments'
import type { BranchStatus } from '../../components/comments/context'
import type { Profile } from '../../api/profiles'
import { comment } from './factory'

// The states of the comment tree (E09-02, docs/ui/comments.md); the page against the fake API is in
// e2e/comments.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

const anna = {
  id: 'b1',
  nickname: 'anna',
  name: 'Анна',
  avatarUrl: null,
  avatarSources: [],
} as unknown as Profile

function mountTree(
  state: CommentsState,
  extra: { branches?: Record<string, BranchStatus>; authors?: Map<string, Profile | null> } = {},
) {
  return mount(CommentTree, {
    props: {
      postId: 'p1',
      signedIn: false,
      state,
      authors: extra.authors ?? new Map(),
      branches: extra.branches ?? {},
    },
    global: { stubs },
  })
}

const tree: CommentsState = {
  status: 'found',
  maxDepth: 8,
  comments: [
    comment('1', null, 1, 3),
    comment('1.1', '1', 2, 2),
    comment('1.1.1', '1.1', 3, 1),
    comment('2', null, 1),
  ],
}

describe('CommentTree', () => {
  it('shows a skeleton of three comments while the tree loads', () => {
    const view = mountTree({ status: 'loading' })
    expect(view.get('[data-testid="comments-loading"]').attributes('aria-busy')).toBe('true')
    expect(view.findAll('.comments__skeleton')).toHaveLength(3)
    expect(view.get('[role="status"]').text()).toBe('Загрузка комментариев')
  })

  it('offers «Повторить» when the tree fails to load', async () => {
    const view = mountTree({ status: 'error' })
    expect(view.get('[role="alert"]').text()).toBe('Не удалось загрузить комментарии.')
    await view.get('[data-testid="comments-error"] button').trigger('click')
    expect(view.emitted('retry')).toHaveLength(1)
  })

  it('says there are no comments yet', () => {
    const view = mountTree({ status: 'found', comments: [], maxDepth: 8 })
    expect(view.get('[data-testid="comments-empty"]').text()).toBe('Комментариев пока нет — будьте первым')
    expect(view.find('[data-testid="comments-count"]').exists()).toBe(false)
  })

  it('draws nested lists with the level of each comment and the count of all', () => {
    const view = mountTree(tree, { authors: new Map([['a1', anna]]) })
    expect(view.get('[data-testid="comments-count"]').text()).toBe('5')
    const items = view.findAll('[data-testid="comment"]')
    expect(items.map((item) => item.attributes('aria-level'))).toEqual(['1', '2', '3', '1'])
    expect(
      view.get('[data-comment-id="1.1.1"]').element.parentElement?.closest('li')?.dataset.commentId,
    ).toBe('1.1')
    expect(view.get('[data-testid="comment-author"]').text()).toBe('Анна')
    expect(view.get('[data-testid="comment-author"]').attributes('href')).toBe('/u/anna')
  })

  it('names a guest by the outside author and a native one without a profile as «Автор»', () => {
    const guest = {
      ...comment('g', null, 1),
      kind: 'guest',
      authorAccountId: null,
      guest: { name: 'Гость', url: null },
    }
    const view = mountTree({ status: 'found', comments: [guest, comment('n', null, 1)], maxDepth: 8 })
    expect(view.findAll('[data-testid="comment-author"]').map((author) => author.text())).toEqual([
      'Гость',
      'Автор',
    ])
  })

  it('folds a branch by its line and unfolds it again', async () => {
    const view = mountTree(tree)
    const line = view.get('[data-comment-id="1"] > .comment__branch > [data-testid="comment-collapse"]')
    expect(line.attributes('aria-expanded')).toBe('true')
    expect(line.attributes('aria-label')).toBe('Свернуть ветку: 3 ответа')

    await line.trigger('click')
    expect(view.find('[data-comment-id="1.1"]').exists()).toBe(false)
    const expand = view.get('[data-testid="comment-expand"]')
    expect(expand.attributes('aria-expanded')).toBe('false')
    expect(expand.text()).toBe('Развернуть · 3 ответа')

    await expand.trigger('click')
    expect(view.find('[data-comment-id="1.1.1"]').exists()).toBe(true)
  })

  it('asks for the replies a read left out', async () => {
    const view = mountTree(tree)
    const more = view.get('[data-comment-id="1.1.1"] [data-testid="comment-more"]')
    expect(more.text()).toBe('Показать ещё 1 ответ')
    await more.trigger('click')
    expect(view.emitted('more')).toEqual([['1.1.1']])
  })

  it('shows a skeleton line while a branch loads and «Повторить» when it fails', () => {
    const loading = mountTree(tree, { branches: { '1.1.1': 'loading' } })
    expect(loading.find('[data-testid="comment-more"]').exists()).toBe(false)
    expect(loading.get('[data-testid="comment-more-loading"]').attributes('aria-busy')).toBe('true')

    const failed = mountTree(tree, { branches: { '1.1.1': 'error' } })
    expect(failed.get('[data-comment-id="1.1.1"] [role="alert"]').text()).toBe('Не удалось загрузить ответы.')
    expect(failed.get('[data-testid="comment-more"]').text()).toBe('Повторить')
  })
  // A thousand comments in a real browser: e2e/comments.spec.ts (happy-dom is too slow to time a render).
})

// The phone's tree (E09-06): a branch deeper than one read opens on its own screen; that screen is one branch.
describe('CommentTree on a phone', () => {
  const onPhone = (state: CommentsState, branch = false) =>
    mount(CommentTree, {
      props: {
        postId: 'p1',
        signedIn: true,
        state,
        authors: new Map(),
        branches: {},
        branchLink: (id: string) => `/u/anna/p1-slug/comments/${id}`,
        branch,
      },
      global: { stubs },
    })

  it('links the replies a read left out to the branch screen instead of reading them in place', () => {
    const view = onPhone(tree)
    expect(view.find('[data-testid="comment-more"]').exists()).toBe(false)
    const open = view.get('[data-comment-id="1.1.1"] [data-testid="comment-open-branch"]')
    expect(open.attributes('href')).toBe('/u/anna/p1-slug/comments/1.1.1')
    expect(open.text()).toBe('Открыть ветку (1) →')
    expect(open.attributes('aria-label')).toBe('Открыть ветку: 1 ответ')
  })

  it('draws a branch screen without the heading, the order and the form under the post', () => {
    const branch: CommentsState = {
      status: 'found',
      maxDepth: 8,
      comments: [comment('1.1', '1', 4, 1), comment('1.1.1', '1.1', 5)],
    }
    const view = onPhone(branch, true)
    expect(view.find('#comments-title').exists()).toBe(false)
    expect(view.find('[data-testid="comments-sort"]').exists()).toBe(false)
    expect(view.find('[data-testid="comment-form"]').exists()).toBe(false)
    expect(view.findAll('[data-testid="comment"]').map((item) => item.attributes('aria-level'))).toEqual([
      '4',
      '5',
    ])
    expect(view.get('[data-comment-id="1.1"]').element.parentElement?.dataset.testid).toBe('comment-tree')
    expect(view.get('[data-testid="comment-reply"]').text()).toBe('Ответить')
  })

  it('says when the branch is gone', () => {
    const view = onPhone({ status: 'not-found' }, true)
    expect(view.get('[data-testid="comments-not-found"]').text()).toBe(
      'Ветка не найдена: комментарий удалён или адрес набран с ошибкой.',
    )
    expect(view.find('[data-testid="comment-tree"]').exists()).toBe(false)
  })
})
