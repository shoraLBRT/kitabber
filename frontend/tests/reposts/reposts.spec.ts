import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import FeedCard from '../../components/feed/FeedCard.vue'
import PostReposts from '../../components/post/PostReposts.vue'
import { createApiClient } from '../../api/client'
import type { FeedBlog, FeedPost } from '../../api/feeds'
import { actionText } from '../../api/notifications'
import type { Post } from '../../api/posts'
import {
  appendReposts,
  createRepost,
  isRepost,
  loadReposts,
  repostTextMaxLength,
  type Repost,
} from '../../api/reposts'
import type { RepostsState } from '../../composables/useReposts'

// Reposts (E10-08, FR-REACT, ADR-0117): the outcomes of reposting and of reading the list, the list's states and the
// form on the post page, a repost's card in a feed with the original inside, and the notification's text. Both
// profiles against the fake API: e2e/reposts.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }
const postId = '0195f3a0-0000-7000-8000-0000000000c1'

const anna: FeedBlog = {
  id: '0195f3a0-0000-7000-8000-0000000000b1',
  type: 'personal',
  name: 'Анна',
  nickname: 'anna',
  avatarSources: [],
}
const boris: FeedBlog = {
  id: '0195f3a0-0000-7000-8000-0000000000b2',
  type: 'personal',
  name: 'Борис',
  nickname: 'boris',
  avatarSources: [],
}

function clientAnswering(status: number, body: unknown) {
  const fetch = vi.fn(
    async () =>
      new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } }),
  )
  return { client: createApiClient({ baseUrl: 'http://api.test', fetch }), fetch }
}

function post(extra: Partial<Post> = {}): Post {
  return {
    id: postId,
    blogId: anna.id,
    authorAccountId: '0195f3a0-0000-7000-8000-000000000001',
    title: 'Привет, мир',
    slug: 'privet-mir',
    body: { version: 1, blocks: [{ id: 'b1', type: 'paragraph', data: { text: 'Первый абзац.' } }] } as never,
    status: 'published',
    origin: 'native',
    source: null,
    createdAt: '2026-11-20T10:00:00Z',
    updatedAt: '2026-11-20T10:00:00Z',
    publishedAt: '2026-11-20T10:00:00Z',
    images: {},
    imagePreviews: {},
    viewCount: 7,
    ...extra,
  }
}

const repost = (id: string, text: string | null): Repost => ({
  id,
  authorAccountId: '0195f3a0-0000-7000-8000-000000000002',
  author: boris,
  text,
  publishedAt: '2026-11-21T10:00:00Z',
})

describe('createRepost', () => {
  it('gives the new repost and sends a blank text as none', async () => {
    const { client, fetch } = clientAnswering(201, post({ id: 'r1', repostOfPostId: postId }))

    expect(await createRepost(client, postId, '   ')).toEqual({ status: 'done', id: 'r1' })
    const request = (fetch.mock.calls[0] as unknown as [Request])[0]
    expect(new URL(request.url).pathname).toBe(`/api/v1/posts/${postId}/reposts`)
    expect(await request.json()).toEqual({ text: null })
  })

  it.each([
    [409, 'posts.already_reposted', 'Вы уже сделали репост этого поста.'],
    [403, 'posts.repost_own_post', 'Свой пост нельзя репостнуть.'],
    [403, 'account.email_not_confirmed', 'Чтобы делать репосты, подтвердите почту.'],
    [404, 'posts.post_not_found', 'Этого поста больше нет.'],
    [400, 'validation_failed', 'Текст к репосту — не длиннее 1000 символов.'],
  ])('tells why the server refused with %i %s', async (status, code, message) => {
    const { client } = clientAnswering(status, { status, code })
    expect(await createRepost(client, postId, 'Текст')).toEqual({ status: 'refused', message })
  })

  it('tells a signed-out person apart from a failure', async () => {
    expect(await createRepost(clientAnswering(401, {}).client, postId, '')).toEqual({
      status: 'unauthorized',
    })
    expect(await createRepost(clientAnswering(500, {}).client, postId, '')).toEqual({ status: 'error' })
  })
})

describe('loadReposts', () => {
  it('reads a page after the cursor and fails as an error', async () => {
    const page = { items: [repost('r1', 'Текст')], nextCursor: null }
    const { client, fetch } = clientAnswering(200, page)

    expect(await loadReposts(client, postId, 'abc')).toEqual({ status: 'found', page })
    const url = new URL((fetch.mock.calls[0] as unknown as [Request])[0].url)
    expect(url.searchParams.get('cursor')).toBe('abc')
    expect(await loadReposts(clientAnswering(404, {}).client, postId)).toEqual({ status: 'error' })
  })

  it('appends the next page, each repost once', () => {
    const first = repost('r1', null)
    expect(appendReposts([first], [first, repost('r2', null)]).map((r) => r.id)).toEqual(['r1', 'r2'])
  })
})

describe('PostReposts', () => {
  const mountList = (state: RepostsState, props: Record<string, unknown> = {}) =>
    mount(PostReposts, { props: { state, ...props }, global: { stubs } })

  it('shows a skeleton while loading, «Повторить» on a failure and a quiet text when empty', async () => {
    expect(mountList({ status: 'loading' }).find('[data-testid="reposts-loading"]').exists()).toBe(true)
    const failed = mountList({ status: 'error' })
    expect(failed.get('[data-testid="reposts-error"]').text()).toContain('Не удалось загрузить репосты.')
    await failed.get('[data-testid="reposts-error"] button').trigger('click')
    expect(failed.emitted('retry')).toHaveLength(1)
    expect(mountList({ status: 'empty' }).get('[data-testid="reposts-empty"]').text()).toBe(
      'Репостов пока нет.',
    )
  })

  it('lists who reposted with their text and offers the next page', async () => {
    const wrapper = mountList({
      status: 'found',
      reposts: [repost('r1', 'Почитайте'), repost('r2', null)],
      hasMore: true,
    })
    const items = wrapper.findAll('[data-testid="repost-item"]')
    expect(items).toHaveLength(2)
    expect(items[0]!.text()).toContain('Борис')
    expect(items[0]!.get('a').attributes('href')).toBe('/u/boris')
    expect(items[0]!.get('[data-testid="repost-item-text"]').text()).toBe('Почитайте')
    expect(items[1]!.find('[data-testid="repost-item-text"]').exists()).toBe(false)
    await wrapper.get('[data-testid="reposts-more"]').trigger('click')
    expect(wrapper.emitted('more')).toHaveLength(1)
  })

  it('offers «Сделать репост» only to who may repost, and sends the text from the form', async () => {
    expect(mountList({ status: 'empty' }).find('[data-testid="repost-open"]').exists()).toBe(false)
    const wrapper = mountList({ status: 'empty' }, { canRepost: true })
    await wrapper.get('[data-testid="repost-open"]').trigger('click')
    const text = wrapper.get('[data-testid="repost-text"]')
    expect(text.attributes('maxlength')).toBe(String(repostTextMaxLength))
    await text.setValue('Обязательно к прочтению')
    await wrapper.get('[data-testid="repost-form"]').trigger('submit')
    expect(wrapper.emitted('repost')).toEqual([['Обязательно к прочтению']])
  })

  it('shows the server’s reason and, once reposted, a confirmation instead of the button', async () => {
    const refused = mountList(
      { status: 'empty' },
      { canRepost: true, sendError: 'Вы уже сделали репост этого поста.' },
    )
    await refused.get('[data-testid="repost-open"]').trigger('click')
    expect(refused.get('[data-testid="repost-error"]').text()).toBe('Вы уже сделали репост этого поста.')

    const done = mountList({ status: 'empty' }, { canRepost: true, reposted: true })
    expect(done.get('[data-testid="repost-done"]').text()).toBe('Репост опубликован в вашем блоге.')
    expect(done.find('[data-testid="repost-open"]').exists()).toBe(false)
  })
})

describe('a repost in a feed', () => {
  const original = post()
  const card: FeedPost = {
    post: post({
      id: 'r1',
      blogId: boris.id,
      authorAccountId: '0195f3a0-0000-7000-8000-000000000002',
      title: null,
      slug: null,
      body: { version: 1, blocks: [] } as never,
      publishedAt: '2026-11-21T10:00:00Z',
      viewCount: 0,
      repostOfPostId: postId,
      repostText: 'Хороший пост',
    }),
    author: boris,
    topic: null,
    bestComment: null,
    original: { post: original, author: anna, topic: null },
  }

  it('is a repost only with its original', () => {
    expect(isRepost(card)).toBe(true)
    expect(isRepost({ ...card, original: null })).toBe(false)
    expect(isRepost({ ...card, post: original, original: null })).toBe(false)
  })

  it('shows the reposter and their text over the original, which the card leads to', () => {
    const wrapper = mount(FeedCard, { props: { item: card }, global: { stubs } })
    const header = wrapper.get('[data-testid="feed-card-repost"]')
    expect(header.text()).toContain('Борис')
    expect(header.text()).toContain('сделал репост')
    expect(header.get('a').attributes('href')).toBe('/u/boris')
    expect(wrapper.get('[data-testid="feed-card-repost-text"]').text()).toBe('Хороший пост')
    expect(wrapper.get('[data-testid="feed-card-author"]').text()).toContain('Анна')
    expect(wrapper.get('[data-testid="feed-card-title"]').attributes('href')).toBe(
      `/u/anna/${postId}-privet-mir`,
    )
    expect(wrapper.text()).toContain('Первый абзац.')
    expect(wrapper.get('[data-testid="feed-card-views"]').text()).toContain('7')
  })

  it('names a repost in the notifications', () => {
    expect(actionText({ type: 'repost' })).toBe('сделал репост вашего поста')
  })
})
