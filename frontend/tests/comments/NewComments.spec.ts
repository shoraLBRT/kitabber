import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import CommentTree from '../../components/comments/CommentTree.vue'
import { newCommentsOrder, type Comment, type CommentsState } from '../../api/comments'
import { comment } from './factory'

// The comments new since the previous opening (E09-08, docs/ui/comments.md, ADR-0109): each is marked «новый», unlike
// the comment a link named, and «К следующему новому · N» goes through them. Reading the opening through the site's
// server and going to a deeper new comment is in e2e/comment-new.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' }, teleport: true }

let wrapper: ReturnType<typeof mount>

function mountTree(
  state: CommentsState,
  extra: { fresh?: Record<string, true>; freshLeft?: number; highlighted?: string | null } = {},
) {
  wrapper = mount(CommentTree, {
    props: {
      postId: 'p1',
      signedIn: true,
      state,
      authors: new Map(),
      branches: {},
      fresh: extra.fresh ?? {},
      freshLeft: extra.freshLeft ?? 0,
      highlighted: extra.highlighted ?? null,
    },
    global: { stubs },
  })
  return wrapper
}

const found = (comments: Comment[]): CommentsState => ({ status: 'found', comments, maxDepth: 8 })

afterEach(() => wrapper?.unmount())

describe('CommentTree: new since the previous opening', () => {
  it('marks only the new comments, apart from the one a link named', async () => {
    mountTree(found([comment('1', null, 1, 1), comment('1.1', '1', 2), comment('2', null, 1)]), {
      fresh: { '1.1': true, '2': true },
      freshLeft: 2,
      highlighted: '2',
    })
    await flushPromises()

    const fresh = wrapper.findAll('[data-new="true"]')
    expect(fresh.map((item) => item.attributes('data-comment-id'))).toEqual(['1.1', '2'])
    expect(fresh.every((item) => item.classes().includes('comment--new'))).toBe(true)
    expect(fresh.map((item) => item.find('[data-testid="comment-new"]').text())).toEqual(['новый', 'новый'])
    expect(wrapper.get('[data-comment-id="1"]').classes()).not.toContain('comment--new')
    // The comment a link named keeps its own highlight; a new one has none of it.
    expect(wrapper.get('[data-comment-id="2"]').classes()).toContain('comment--highlighted')
    expect(wrapper.get('[data-comment-id="1.1"]').classes()).not.toContain('comment--highlighted')
  })

  it('offers «К следующему новому» with how many are left and asks for the next one', async () => {
    mountTree(found([comment('1', null, 1), comment('2', null, 1)]), { fresh: { '2': true }, freshLeft: 3 })
    await flushPromises()

    const next = wrapper.get('[data-testid="comments-next-new"]')
    expect(next.text()).toBe('К следующему новому · 3')
    expect(next.attributes('aria-label')).toBe('К следующему новому комментарию, осталось 3')
    await next.trigger('click')
    expect(wrapper.emitted('next-new')).toHaveLength(1)
  })

  it('has no button when nothing new is left, and none while loading, on a failure or with no comments', async () => {
    for (const state of [
      found([comment('1', null, 1)]),
      { status: 'loading' } as const,
      { status: 'error' } as const,
      found([]),
    ]) {
      mountTree(state, { freshLeft: state.status === 'found' && state.comments.length > 0 ? 0 : 2 })
      await flushPromises()
      expect(wrapper.find('[data-testid="comments-next-new"]').exists()).toBe(false)
      expect(wrapper.find('[data-new="true"]').exists()).toBe(false)
      wrapper.unmount()
    }
  })

  it('does not mark a deleted comment', async () => {
    const stub = { ...comment('1', null, 1, 1), deleted: true }
    mountTree(found([stub, comment('1.1', '1', 2)]), { fresh: { '1': true, '1.1': true }, freshLeft: 1 })
    await flushPromises()
    expect(wrapper.findAll('[data-new="true"]').map((item) => item.attributes('data-comment-id'))).toEqual([
      '1.1',
    ])
  })
})

describe('newCommentsOrder', () => {
  const comments = [comment('2', null, 1, 1), comment('2.1', '2', 2), comment('1', null, 1)]

  it('goes through the new comments in the order shown, then the ones not read yet in the order of the tree', () => {
    expect(newCommentsOrder(comments, ['1', '1.9', '2.1', '3'], new Set(), true)).toEqual([
      '2.1',
      '1',
      '1.9',
      '3',
    ])
  })

  it('leaves out the ones gone to, a deleted one and, on a phone, the ones not read', () => {
    const shown = [...comments, { ...comment('4', null, 1), deleted: true }]
    expect(newCommentsOrder(shown, ['1', '2.1', '4', '9'], new Set(['2.1']), false)).toEqual(['1'])
  })
})
