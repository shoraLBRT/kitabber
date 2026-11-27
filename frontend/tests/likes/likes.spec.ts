import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import { createApiClient } from '../../api/client'
import type { CommentsState } from '../../api/comments'
import {
  changeReaction,
  chosen,
  loadReactions,
  noReactions,
  postReactions,
  rowOf,
  type ReactionKind,
  type ReactionState,
  type ReactionTarget,
} from '../../api/reactions'
import CommentTree from '../../components/comments/CommentTree.vue'
import PostActions from '../../components/post/PostActions.vue'
import { likeAction, type Likes } from '../../composables/useLikes'
import { comment } from '../comments/factory'

// Reactions on posts and comments (E10-01, E10-02, ADR-0061, ADR-0101) without a page: the API's answers, the state
// shown before the server answers, and the buttons of the action panel and of the comment tree. The page against the
// fake API is in e2e/likes.spec.ts and e2e/reaction-choice.spec.ts.

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

const likes_ = (count: number, mine: ReactionKind | null = null): ReactionState => ({
  counts: count > 0 ? { like: count } : {},
  mine,
})

/** A stand-in for useLikes: the counts given, every choice recorded. */
function fakeLikes(post: ReactionState, comments: Record<string, ReactionState> = {}) {
  const presses: [ReactionTarget, string, ReactionKind][] = []
  const postRef = ref(post)
  const commentsRef = ref(comments)
  const of = (target: ReactionTarget, id: string) =>
    target === 'post' ? postRef.value : (commentsRef.value[id] ?? noReactions)
  const likes = {
    post: postRef,
    comments: commentsRef,
    set: ref(null),
    of,
    reactions: (target: ReactionTarget, id: string) => rowOf(of(target, id)),
    choose: (target: ReactionTarget, id: string, kind: ReactionKind) => presses.push([target, id, kind]),
    loadSet: () => {},
    isBusy: () => false,
  } as unknown as Likes
  return { likes, presses }
}

describe('reactions API', () => {
  it('puts a kind with PUT and takes the reaction back with DELETE on the target', async () => {
    const { client, fetch } = clientAnswering({
      status: 200,
      body: { reactions: [{ kind: 'heart', count: 3 }], mine: 'heart' },
    })
    expect(await changeReaction(client, 'post', postId, 'heart')).toEqual({
      status: 'done',
      state: { counts: { heart: 3 }, mine: 'heart' },
    })
    await changeReaction(client, 'comment', 'c1', null)
    const [put, del] = fetch.mock.calls.map((call) => (call as unknown as [Request])[0])
    expect([put!.method, new URL(put!.url).pathname]).toEqual(['PUT', `/api/v1/posts/${postId}/reaction`])
    expect(await put!.json()).toEqual({ kind: 'heart' })
    expect([del!.method, new URL(del!.url).pathname]).toEqual(['DELETE', '/api/v1/comments/c1/reaction'])
  })

  it('gives the reason of a refusal, a kind out of the set, signed out and a failure apart', async () => {
    const own = clientAnswering({
      status: 403,
      body: { code: 'reactions.own_content', detail: 'На свой пост или комментарий реакцию не ставят.' },
    })
    expect(await changeReaction(own.client, 'post', postId, 'like')).toEqual({
      status: 'refused',
      message: 'На свой пост или комментарий реакцию не ставят.',
    })
    const gone = clientAnswering({
      status: 400,
      body: { errors: { kind: ['…'] }, detail: 'One or more errors' },
    })
    expect(await changeReaction(gone.client, 'post', postId, 'sad')).toEqual({
      status: 'refused',
      message: 'Этой реакции больше нет в наборе.',
    })
    expect(
      await changeReaction(clientAnswering({ status: 401, body: {} }).client, 'post', postId, 'like'),
    ).toEqual({
      status: 'unauthorized',
    })
    expect(
      await changeReaction(clientAnswering({ status: 500, body: {} }).client, 'post', postId, 'like'),
    ).toEqual({
      status: 'error',
    })
  })

  it('reads the reactions of the post and its comments, skipping unknown kinds, nothing on a failure', async () => {
    const body = {
      postId,
      post: {
        reactions: [
          { kind: 'like', count: 2 },
          { kind: 'fire', count: 9 },
        ],
        mine: null,
      },
      comments: [{ commentId: 'c1', reactions: [{ kind: 'clap', count: 1 }], mine: 'clap' }],
    }
    const read = await loadReactions(clientAnswering({ status: 200, body }).client, postId)
    expect(read).toEqual(body)
    expect(postReactions(read!)).toEqual({
      post: { counts: { like: 2 }, mine: null },
      comments: { c1: { counts: { clap: 1 }, mine: 'clap' } },
    })
    expect(await loadReactions(clientAnswering({ status: 404, body: {} }).client, postId)).toBeNull()
  })

  it('shows the choice at once: the old kind loses one, the new one gains one', () => {
    expect(chosen(likes_(2), 'like')).toEqual({ counts: { like: 3 }, mine: 'like' })
    expect(chosen(likes_(3, 'like'), null)).toEqual({ counts: { like: 2 }, mine: null })
    expect(chosen(likes_(1, 'like'), 'wow')).toEqual({ counts: { wow: 1 }, mine: 'wow' })
    expect(chosen({ counts: { like: 4, heart: 1 }, mine: 'heart' }, 'like')).toEqual({
      counts: { like: 5 },
      mine: 'like',
    })
    expect(chosen({ counts: {}, mine: 'like' }, null)).toEqual({ counts: {}, mine: null })
  })

  it('gives the row every kind with a count and the own one, in the order of the kinds', () => {
    expect(rowOf({ counts: { sad: 1, like: 2 }, mine: 'sad' })).toEqual([
      { kind: 'like', count: 2, mine: false },
      { kind: 'sad', count: 1, mine: true },
    ])
    expect(rowOf(noReactions)).toEqual([])
  })

  it('names the action a guest repeats after the sign-in, with the kind chosen', () => {
    expect(likeAction(postId, 'post', postId)).toBe(`like:${postId}:post:like`)
    expect(likeAction(postId, 'comment', 'c1', 'idea')).toBe(`like:${postId}:c1:idea`)
  })
})

describe('PostActions', () => {
  it('turns the like of another author’s post over and shows the count', async () => {
    const { likes, presses } = fakeLikes(likes_(4, 'like'))
    const panel = mount(PostActions, { props: { path: '/u/anna/x', postId, own: false, likes } })
    const like = panel.get('[data-testid="post-like"]')
    expect(like.attributes('disabled')).toBeUndefined()
    expect(like.attributes('aria-pressed')).toBe('true')
    expect(panel.get('[data-testid="post-like-count"]').text()).toBe('4')
    await like.trigger('click')
    expect(presses).toEqual([['post', postId, 'like']])
  })

  it('only shows the count on one’s own post, and nothing to press on a draft', async () => {
    const { likes, presses } = fakeLikes(likes_(2))
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

  it('reacts to a native comment, only counts the own one, and offers none on a guest or a deleted one', async () => {
    const { likes, presses } = fakeLikes(noReactions, { '1': likes_(5) })
    const view = mountTree(likes)

    const first = likeOf(view, '1')
    expect(first.attributes('aria-pressed')).toBe('false')
    expect(first.text()).toContain('5')
    await first.trigger('click')
    expect(presses).toEqual([['comment', '1', 'like']])

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
    const { likes } = fakeLikes(noReactions, { '1': likes_(1, 'like') })
    const like = likeOf(mountTree(likes), '1')
    expect(like.attributes('aria-pressed')).toBe('true')
    expect(like.classes()).toContain('reaction--mine')
  })
})
