import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import { createApiClient } from '../../api/client'
import type { CommentsState } from '../../api/comments'
import {
  changeLike,
  commentLikes,
  loadLikes,
  toggled,
  type LikeState,
  type LikeTarget,
} from '../../api/likes'
import CommentTree from '../../components/comments/CommentTree.vue'
import PostActions from '../../components/post/PostActions.vue'
import { likeAction, type Likes } from '../../composables/useLikes'
import { comment } from '../comments/factory'

// Likes on posts and comments (E10-01, ADR-0061) without a page: the API's answers, the state shown before the server
// answers, and the buttons of the action panel and of the comment tree. The page against the fake API is in
// e2e/likes.spec.ts.

const postId = '0195f3a0-0000-7000-8000-0000000000c1'

function clientAnswering(answer: { status: number; body: unknown }) {
  const fetch = vi.fn(
    async () =>
      new Response(JSON.stringify(answer.body), {
        status: answer.status,
        headers: { 'content-type': 'application/json' },
      }),
  )
  return { client: createApiClient({ baseUrl: 'http://api.test', fetch }), fetch }
}

/** A stand-in for useLikes: the counts given, every press recorded. */
function fakeLikes(post: LikeState, comments: Record<string, LikeState> = {}) {
  const presses: [LikeTarget, string][] = []
  const postRef = ref(post)
  const commentsRef = ref(comments)
  const likes = {
    post: postRef,
    comments: commentsRef,
    of: (target: LikeTarget, id: string) =>
      target === 'post' ? postRef.value : (commentsRef.value[id] ?? { count: 0, liked: false }),
    toggle: (target: LikeTarget, id: string) => presses.push([target, id]),
    isBusy: () => false,
  } as unknown as Likes
  return { likes, presses }
}

describe('likes API', () => {
  it('puts and takes back a like with PUT and DELETE on the target', async () => {
    const { client, fetch } = clientAnswering({ status: 200, body: { count: 3, liked: true } })
    expect(await changeLike(client, 'post', postId, true)).toEqual({
      status: 'done',
      state: { count: 3, liked: true },
    })
    await changeLike(client, 'comment', 'c1', false)
    const [put, del] = fetch.mock.calls.map((call) => (call as unknown as [Request])[0])
    expect([put!.method, new URL(put!.url).pathname]).toEqual(['PUT', `/api/v1/posts/${postId}/like`])
    expect([del!.method, new URL(del!.url).pathname]).toEqual(['DELETE', '/api/v1/comments/c1/like'])
  })

  it('gives the reason of a refused like, signed out and a failure apart', async () => {
    const own = clientAnswering({
      status: 403,
      body: { code: 'reactions.own_content', detail: 'Свой пост или комментарий оценить нельзя.' },
    })
    expect(await changeLike(own.client, 'post', postId, true)).toEqual({
      status: 'refused',
      message: 'Свой пост или комментарий оценить нельзя.',
    })
    expect(await changeLike(clientAnswering({ status: 401, body: {} }).client, 'post', postId, true)).toEqual(
      {
        status: 'unauthorized',
      },
    )
    expect(await changeLike(clientAnswering({ status: 500, body: {} }).client, 'post', postId, true)).toEqual(
      {
        status: 'error',
      },
    )
  })

  it('reads the likes of the post and its comments, nothing on a failure', async () => {
    const body = {
      postId,
      post: { count: 2, liked: false },
      comments: [{ commentId: 'c1', count: 1, liked: true }],
    }
    const likes = await loadLikes(clientAnswering({ status: 200, body }).client, postId)
    expect(likes).toEqual(body)
    expect(commentLikes(likes!)).toEqual({ c1: { count: 1, liked: true } })
    expect(await loadLikes(clientAnswering({ status: 404, body: {} }).client, postId)).toBeNull()
  })

  it('shows the like put or taken back at once, the count moved by one', () => {
    expect(toggled({ count: 2, liked: false })).toEqual({ count: 3, liked: true })
    expect(toggled({ count: 3, liked: true })).toEqual({ count: 2, liked: false })
    expect(toggled({ count: 0, liked: true })).toEqual({ count: 0, liked: false })
  })

  it('names the action a guest repeats after the sign-in', () => {
    expect(likeAction(postId, 'post', postId)).toBe(`like:${postId}:post`)
    expect(likeAction(postId, 'comment', 'c1')).toBe(`like:${postId}:c1`)
  })
})

describe('PostActions', () => {
  it('turns the like of another author’s post over and shows the count', async () => {
    const { likes, presses } = fakeLikes({ count: 4, liked: true })
    const panel = mount(PostActions, { props: { path: '/u/anna/x', postId, own: false, likes } })
    const like = panel.get('[data-testid="post-like"]')
    expect(like.attributes('disabled')).toBeUndefined()
    expect(like.attributes('aria-pressed')).toBe('true')
    expect(panel.get('[data-testid="post-like-count"]').text()).toBe('4')
    await like.trigger('click')
    expect(presses).toEqual([['post', postId]])
  })

  it('only shows the count on one’s own post, and nothing to press on a draft', async () => {
    const { likes, presses } = fakeLikes({ count: 2, liked: false })
    const own = mount(PostActions, { props: { path: '/u/anna/x', postId, own: true, likes } })
    const like = own.get('[data-testid="post-like"]')
    expect(like.attributes('disabled')).toBeDefined()
    expect(like.attributes('title')).toBe('Свой пост оценить нельзя')
    expect(own.get('[data-testid="post-like-count"]').text()).toBe('2')
    await like.trigger('click')
    expect(presses).toEqual([])

    const draft = mount(PostActions, { props: { path: '/u/anna/x', postId: null, likes } })
    expect(draft.get('[data-testid="post-like"]').attributes('disabled')).toBeDefined()
    const none = mount(PostActions, { props: { path: '/u/anna/x' } })
    expect(none.get('[data-testid="post-like"]').attributes('disabled')).toBeDefined()
    expect(none.find('[data-testid="post-like-count"]').exists()).toBe(false)
  })
})

describe('the likes of the comment tree', () => {
  const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }
  const guest = {
    ...comment('g', null, 1),
    kind: 'guest',
    authorAccountId: null,
    guest: { name: 'Иван', url: null },
  }
  const deleted = { ...comment('d', null, 1, 1), deleted: true, authorAccountId: null, text: '' }
  const state: CommentsState = {
    status: 'found',
    maxDepth: 8,
    comments: [comment('1', null, 1), comment('2', null, 1), guest, deleted, comment('d.1', 'd', 2)],
  }

  function mountTree(likes: Likes | null) {
    return mount(CommentTree, {
      props: {
        postId: 'p1',
        signedIn: true,
        state,
        authors: new Map(),
        branches: {},
        own: { '2': true as const },
        likes,
      },
      global: { stubs },
    })
  }

  const likeOf = (view: ReturnType<typeof mountTree>, id: string) =>
    view.find(`[data-comment-id="${id}"] > .comment__actions [data-testid="comment-like"]`)

  it('likes a native comment, only counts the own one, and offers none on a guest or a deleted one', async () => {
    const { likes, presses } = fakeLikes({ count: 0, liked: false }, { '1': { count: 5, liked: false } })
    const view = mountTree(likes)

    const first = likeOf(view, '1')
    expect(first.attributes('aria-pressed')).toBe('false')
    expect(first.text()).toContain('5')
    await first.trigger('click')
    expect(presses).toEqual([['comment', '1']])

    const own = likeOf(view, '2')
    expect(own.attributes('disabled')).toBeDefined()
    expect(own.attributes('title')).toBe('Свой комментарий оценить нельзя')

    expect(likeOf(view, 'g').exists()).toBe(false)
    expect(likeOf(view, 'd').exists()).toBe(false)
    expect(likeOf(view, 'd.1').exists()).toBe(true)
  })

  it('shows no likes without them', () => {
    expect(mountTree(null).find('[data-testid="comment-like"]').exists()).toBe(false)
  })

  it('marks the comment the person likes', () => {
    const { likes } = fakeLikes({ count: 0, liked: false }, { '1': { count: 1, liked: true } })
    const like = likeOf(mountTree(likes), '1')
    expect(like.attributes('aria-pressed')).toBe('true')
    expect(like.classes()).toContain('comment__like--on')
  })
})
