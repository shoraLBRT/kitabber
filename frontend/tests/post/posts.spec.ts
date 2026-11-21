import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { createApiClient } from '../../api/client'
import type { Profile } from '../../api/profiles'
import { lookUpPost, postIdOf, postPath, postText, type Post, type PostLookup } from '../../api/posts'
import PostView from '../../components/post/PostView.vue'

// The address of a post (E07-02, ADR-0041) and the post page's states; the redirects against the fake API are in
// e2e/post.spec.ts, the slug itself in the backend's PostSlugTests.
const id = '0195f3a0-0000-7000-8000-0000000000c1'

const anna: Profile = {
  id: '0195f3a0-0000-7000-8000-0000000000b1',
  type: 'personal',
  name: 'Анна',
  nickname: 'anna',
  description: null,
  registeredAt: null,
  avatarId: null,
  avatarUrl: null,
}
const books: Profile = {
  ...anna,
  id: '0195f3a0-0000-7000-8000-0000000000b4',
  type: 'topic',
  name: 'Книги',
  nickname: 'books',
}

const post: Post = {
  id,
  blogId: anna.id,
  authorAccountId: '0195f3a0-0000-7000-8000-000000000001',
  title: 'Привет, мир',
  slug: 'privet-mir',
  body: {
    version: 1,
    blocks: [
      { id: 'a', type: 'paragraph', data: { text: 'Первый <b>абзац</b>&nbsp;и&amp;' } },
      { id: 'b', type: 'delimiter', data: {} },
      { id: 'c', type: 'header', data: { text: 'Подзаголовок', level: 2 } },
    ],
  } as unknown as Post['body'],
  status: 'published',
  origin: 'native',
  source: null,
  createdAt: '2026-11-20T10:00:00Z',
  updatedAt: '2026-11-20T10:00:00Z',
  publishedAt: '2026-11-20T10:00:00Z',
}

describe('post address', () => {
  it('takes the id from <id>-<slug> and from the id alone', () => {
    expect(postIdOf(`${id}-privet-mir`)).toBe(id)
    expect(postIdOf(id.toUpperCase())).toBe(id)
    expect(postIdOf(`${id}-любой-слаг`)).toBe(id)
  })

  it('finds no id in segments that are not a post', () => {
    for (const segment of ['comments', '', `${id}x`, id.slice(0, 35), 'privet-mir'])
      expect(postIdOf(segment), segment).toBeNull()
  })

  it('has one scheme for a blog and a topic', () => {
    expect(postPath(anna, post)).toBe(`/u/anna/${id}-privet-mir`)
    expect(postPath(books, post)).toBe(`/books/${id}-privet-mir`)
    expect(postPath({ ...anna, nickname: null }, post)).toBe(`/u/${anna.id}/${id}-privet-mir`)
  })

  it('ends with the id alone for a post without a title', () => {
    expect(postPath(anna, { id, slug: null })).toBe(`/u/anna/${id}`)
  })
})

function clientAnswering(answers: Record<string, { status: number; body: unknown }>) {
  const fetch = vi.fn(async (request: Request) => {
    const answer = answers[new URL(request.url).pathname] ?? { status: 404, body: {} }
    return new Response(JSON.stringify(answer.body), {
      status: answer.status,
      headers: { 'content-type': 'application/json' },
    })
  })
  return { client: createApiClient({ baseUrl: 'http://api.test', fetch }), fetch }
}

describe('lookUpPost', () => {
  it('finds the post by the id of the segment and gives its canonical address', async () => {
    const { client, fetch } = clientAnswering({
      [`/api/v1/posts/${id}`]: { status: 200, body: post },
      [`/api/v1/profiles/${anna.id}`]: { status: 200, body: anna },
    })
    expect(await lookUpPost(client, `${id}-staryy-slag`)).toEqual({
      status: 'found',
      post,
      blog: anna,
      path: `/u/anna/${id}-privet-mir`,
    })
    expect(fetch.mock.calls[0]![0].url).toBe(`http://api.test/api/v1/posts/${id}`)
  })

  it('does not ask the API about a segment that is not a post', async () => {
    const { client, fetch } = clientAnswering({})
    expect(await lookUpPost(client, 'comments')).toEqual({ status: 'not-found' })
    expect(fetch).not.toHaveBeenCalled()
  })

  it('tells a missing post from a failure', async () => {
    expect(await lookUpPost(clientAnswering({}).client, id)).toEqual({ status: 'not-found' })
    const failing = clientAnswering({ [`/api/v1/posts/${id}`]: { status: 500, body: {} } })
    expect(await lookUpPost(failing.client, id)).toEqual({ status: 'error' })
  })
})

describe('PostView', () => {
  const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }
  const mountView = (state: PostLookup | { status: 'loading' }) =>
    mount(PostView, { props: { state }, global: { stubs } })

  it('shows the blog, the title and the text of the post', () => {
    const wrapper = mountView({ status: 'found', post, blog: anna, path: postPath(anna, post) })
    expect(wrapper.get('h1').text()).toBe('Привет, мир')
    expect(wrapper.get('[data-testid="post-blog"]').attributes('href')).toBe('/u/anna')
    expect(wrapper.findAll('.post__text').map((p) => p.text())).toEqual(['Первый абзац и&', 'Подзаголовок'])
    expect(wrapper.find('[data-testid="post-draft"]').exists()).toBe(false)
  })

  it('shows a post without a title without a heading, and a draft as a draft', () => {
    const draft = { ...post, title: null, slug: null, status: 'draft' }
    const wrapper = mountView({ status: 'found', post: draft, blog: books, path: postPath(books, draft) })
    expect(wrapper.find('h1').exists()).toBe(false)
    expect(wrapper.get('[data-testid="post-blog"]').attributes('href')).toBe('/books')
    expect(wrapper.get('[data-testid="post-draft"]').text()).toContain('черновик')
  })

  it('shows loading, not found and a failure with a retry', async () => {
    expect(mountView({ status: 'loading' }).get('[data-testid="post-loading"]').attributes('aria-busy')).toBe(
      'true',
    )
    expect(mountView({ status: 'not-found' }).text()).toContain('Пост не найден')
    const failed = mountView({ status: 'error' })
    expect(failed.get('[role="alert"]').text()).toContain('попробуйте ещё раз')
    await failed.get('button').trigger('click')
    expect(failed.emitted('retry')).toHaveLength(1)
  })

  it('reads the text of the text blocks only', () => {
    expect(postText({ body: { version: 1, blocks: [] } as unknown as Post['body'] })).toEqual([])
  })
})
