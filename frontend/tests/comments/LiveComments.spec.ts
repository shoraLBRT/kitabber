import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it } from 'vitest'
import CommentTree from '../../components/comments/CommentTree.vue'
import {
  commentPostedType,
  loadCommentArrival,
  parseCommentPosted,
  withArrived,
  type Comment,
  type CommentsState,
} from '../../api/comments'
import type { ApiClient } from '../../api/client'
import { comment } from './factory'

// The comments others write while the post is open (E09-09, docs/ui/comments.md, ADR-0112): the event
// `comments.posted`, «N новых» with the heading counting them before they are shown, and where each goes in the tree.
// The page reading them from the channel and the API, and the tree not moving, are in e2e/comment-live.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' }, teleport: true }

let wrapper: ReturnType<typeof mount>

function mountTree(state: CommentsState, arrived: number, branch = false) {
  wrapper = mount(CommentTree, {
    props: { postId: 'p1', signedIn: true, state, authors: new Map(), branches: {}, arrived, branch },
    global: { stubs },
  })
  return wrapper
}

const found = (comments: Comment[]): CommentsState => ({ status: 'found', comments, maxDepth: 8 })

afterEach(() => wrapper?.unmount())

describe('CommentTree: comments that came meanwhile', () => {
  it('counts them in the heading and offers «N новых», which asks to show them', async () => {
    mountTree(found([comment('1', null, 1, 1), comment('1.1', '1', 2)]), 3)
    await flushPromises()

    expect(wrapper.get('[data-testid="comments-count"]').text()).toBe('5')
    const button = wrapper.get('[data-testid="comments-arrived"]')
    expect(button.text()).toBe('3 новых')
    expect(button.attributes('aria-label')).toBe('Показать 3 новых комментария')
    await button.trigger('click')
    expect(wrapper.emitted('show-arrived')).toHaveLength(1)
  })

  it('names one in the singular and many in the genitive', async () => {
    mountTree(found([comment('1', null, 1)]), 1)
    await flushPromises()
    expect(wrapper.get('[data-testid="comments-arrived"]').text()).toBe('1 новый')
    expect(wrapper.get('[data-testid="comments-arrived"]').attributes('aria-label')).toBe(
      'Показать 1 новый комментарий',
    )
    wrapper.unmount()

    mountTree(found([comment('1', null, 1)]), 5)
    await flushPromises()
    expect(wrapper.get('[data-testid="comments-arrived"]').attributes('aria-label')).toBe(
      'Показать 5 новых комментариев',
    )
  })

  it('keeps the bar of no height with no button when none came, so the tree does not move when one does', async () => {
    mountTree(found([comment('1', null, 1)]), 0)
    await flushPromises()
    expect(wrapper.find('[data-testid="comments-arrived-bar"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="comments-arrived-bar"]').attributes('aria-live')).toBe('polite')
    expect(wrapper.find('[data-testid="comments-arrived"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="comments-count"]').text()).toBe('1')
  })

  it('counts them under a post with none yet', async () => {
    mountTree(found([]), 2)
    await flushPromises()
    expect(wrapper.get('[data-testid="comments-count"]').text()).toBe('2')
    expect(wrapper.find('[data-testid="comments-arrived"]').exists()).toBe(true)
  })

  it('has no «N новых» while loading, on a failure and on a branch screen', async () => {
    for (const [state, branch] of [
      [{ status: 'loading' }, false],
      [{ status: 'error' }, false],
      [found([comment('1', null, 1)]), true],
    ] as const) {
      mountTree(state, 2, branch)
      await flushPromises()
      expect(wrapper.find('[data-testid="comments-arrived"]').exists()).toBe(false)
      wrapper.unmount()
    }
  })
})

describe('parseCommentPosted', () => {
  it('reads the event of a new comment', () => {
    expect(commentPostedType).toBe('comments.posted')
    expect(parseCommentPosted({ commentId: 'c1', postId: 'p1', parentId: null })).toEqual({
      commentId: 'c1',
      postId: 'p1',
      parentId: null,
    })
    expect(parseCommentPosted({ commentId: 'c2', postId: 'p1', parentId: 'c1' })?.parentId).toBe('c1')
    expect(parseCommentPosted({ commentId: 'c2', postId: 'p1' })?.parentId).toBeNull()
  })

  it('refuses anything else', () => {
    for (const data of [
      null,
      'c1',
      {},
      { commentId: 'c1' },
      { commentId: 1, postId: 'p1' },
      {
        commentId: 'c1',
        postId: 'p1',
        parentId: 5,
      },
    ])
      expect(parseCommentPosted(data)).toBeNull()
  })
})

describe('withArrived', () => {
  const tree = [
    comment('1', null, 1, 2),
    comment('1.1', '1', 2, 1),
    comment('1.1.1', '1.1', 3),
    comment('2', null, 1),
  ]

  it('puts a comment under the post where a sent one goes for the order', () => {
    expect(withArrived(tree, comment('3', null, 1), [], 'popular').map((c) => c.id)).toEqual([
      '1',
      '1.1',
      '1.1.1',
      '2',
      '3',
    ])
    expect(withArrived(tree, comment('3', null, 1), [], 'date')[0]!.id).toBe('3')
  })

  it('puts a reply under its shown parent and counts it above', () => {
    const added = withArrived(tree, comment('1.2', '1', 2), ['1'])
    expect(added.map((c) => c.id)).toEqual(['1', '1.1', '1.1.1', '1.2', '2'])
    expect(added.map((c) => c.replies)).toEqual([3, 1, 0, 0, 0])
  })

  it('only counts a reply whose parent is not shown under the ancestors that are', () => {
    const added = withArrived(tree, comment('1.1.1.1.1', '1.1.1.1', 5), ['1', '1.1', '1.1.1', '1.1.1.1'])
    expect(added.map((c) => c.id)).toEqual(['1', '1.1', '1.1.1', '2'])
    expect(added.map((c) => c.replies)).toEqual([3, 2, 1, 0])
  })

  it('changes nothing for a comment already in the tree', () => {
    expect(withArrived(tree, comment('1.1', '1', 2), ['1'])).toEqual(tree)
  })
})

describe('loadCommentArrival', () => {
  // The API as the site's server reads it: the branch of one comment and the reader's own comments under the post.
  function client(branch: Comment[] | null, own: string[], postId = 'p1') {
    const paths: string[] = []
    const api = {
      GET: async (path: string) => {
        paths.push(path)
        if (path === '/api/v1/posts/{postId}/comments/mine') return { data: { postId, ids: own } }
        return branch === null
          ? { data: undefined, response: { status: 404 } }
          : { data: { postId, maxDepth: 8, comments: branch }, response: { status: 200 } }
      },
    } as unknown as ApiClient
    return { api, paths }
  }

  it('gives the comment of someone else with the own ids', async () => {
    const { api, paths } = client([comment('c1', null, 1, 0, 'a2')], ['mine'])
    expect(await loadCommentArrival(api, 'p1', 'c1')).toEqual({
      comment: comment('c1', null, 1, 0, 'a2'),
      own: ['mine'],
    })
    expect(paths.sort()).toEqual(['/api/v1/comments/{id}/branch', '/api/v1/posts/{postId}/comments/mine'])
  })

  it('gives none for one of a blocked author, the own one, a deleted one, a gone one or another post', async () => {
    const cases: [Comment[] | null, string[], string][] = [
      [[{ ...comment('c1', null, 1), authorBlocked: true }], [], 'p1'],
      [[comment('c1', null, 1)], ['c1'], 'p1'],
      [[{ ...comment('c1', null, 1), deleted: true }], [], 'p1'],
      [null, [], 'p1'],
      [[comment('c1', null, 1)], [], 'p2'],
    ]
    for (const [branch, own, postId] of cases) {
      const { api } = client(branch, own, postId)
      expect((await loadCommentArrival(api, 'p1', 'c1')).comment).toBeNull()
    }
  })
})
