import { mount } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'
import { createApiClient } from '../../api/client'
import type { Profile } from '../../api/profiles'
import {
  deletePost,
  lookUpPost,
  postIdOf,
  postPath,
  postText,
  type Post,
  type PostLookup,
} from '../../api/posts'
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
  avatarSources: [],
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
  images: {},
  imagePreviews: {},
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
      author: anna,
      path: `/u/anna/${id}-privet-mir`,
      own: false,
    })
    expect(fetch.mock.calls[0]![0].url).toBe(`http://api.test/api/v1/posts/${id}`)
    // In a personal blog the blog is the author's: no third request.
    expect(fetch).toHaveBeenCalledTimes(2)
  })

  // E07-05 (ADR-0053): the post is the reader's own when the account of their session wrote it.
  it('says whether the reader wrote the post', async () => {
    const answers = {
      [`/api/v1/posts/${id}`]: { status: 200, body: post },
      [`/api/v1/profiles/${anna.id}`]: { status: 200, body: anna },
    }
    const author = post.authorAccountId.toUpperCase()
    expect(await lookUpPost(clientAnswering(answers).client, id, author)).toMatchObject({ own: true })
    expect(await lookUpPost(clientAnswering(answers).client, id, anna.id)).toMatchObject({ own: false })
    expect(await lookUpPost(clientAnswering(answers).client, id, null)).toMatchObject({ own: false })
  })

  // E07-04 (ADR-0050): in a topic the author is the personal blog of the post's account.
  it('looks the author of a post in a topic up by the account', async () => {
    const inTopic = { ...post, blogId: books.id }
    const { client } = clientAnswering({
      [`/api/v1/posts/${id}`]: { status: 200, body: inTopic },
      [`/api/v1/profiles/${books.id}`]: { status: 200, body: books },
      [`/api/v1/profiles/by-account/${post.authorAccountId}`]: { status: 200, body: anna },
    })
    expect(await lookUpPost(client, id)).toMatchObject({ status: 'found', blog: books, author: anna })

    const noAuthor = clientAnswering({
      [`/api/v1/posts/${id}`]: { status: 200, body: inTopic },
      [`/api/v1/profiles/${books.id}`]: { status: 200, body: books },
    })
    expect(await lookUpPost(noAuthor.client, id)).toMatchObject({
      status: 'found',
      blog: books,
      author: null,
    })
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

// E07-05 (ADR-0053): deleting asks the API with DELETE and gives its status, which the site's server passes on.
describe('deletePost', () => {
  it('deletes the post by its id and gives the status of the answer', async () => {
    for (const status of [204, 403, 404]) {
      const fetch = vi.fn(async (_request: Request) => new Response(status === 204 ? null : '{}', { status }))
      const client = createApiClient({ baseUrl: 'http://api.test', fetch })
      expect(await deletePost(client, id)).toBe(status)
      const request = fetch.mock.calls[0]![0]
      expect(request.method).toBe('DELETE')
      expect(request.url).toBe(`http://api.test/api/v1/posts/${id}`)
    }
  })
})

describe('PostView', () => {
  // The comments read the API themselves; tests/comments covers them.
  const stubs = {
    NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' },
    PostComments: {
      props: ['postId'],
      template: '<div data-testid="post-comments-stub" :data-post="postId" />',
    },
  }
  const mountView = (state: PostLookup | { status: 'loading' }) =>
    mount(PostView, { props: { state }, global: { stubs } })

  it('shows the author, the date, the title and the text of the post', () => {
    const wrapper = mountView({
      status: 'found',
      post,
      blog: anna,
      author: anna,
      path: postPath(anna, post),
      own: false,
    })
    expect(wrapper.get('h1').text()).toBe('Привет, мир')
    const author = wrapper.get('[data-testid="post-author"]')
    expect(author.attributes('href')).toBe('/u/anna')
    expect(author.text()).toContain('Анна')
    // The blog is the author's own: it is not named twice.
    expect(wrapper.find('[data-testid="post-blog"]').exists()).toBe(false)
    const date = wrapper.get('time[data-testid="post-date"]')
    expect(date.attributes('datetime')).toBe('2026-11-20T10:00:00Z')
    expect(date.text()).toContain('20 ноября 2026')
    const body = wrapper.get('[data-testid="post-blocks"]')
    expect(body.get('p').html()).toContain('Первый <b>абзац</b>')
    expect(body.find('hr').exists()).toBe(true)
    expect(body.get('h2#h-c').text()).toBe('Подзаголовок')
    expect(wrapper.find('[data-testid="post-draft"]').exists()).toBe(false)
  })

  it('shows a post without a title without a heading, and a draft as a draft', () => {
    const draft = { ...post, title: null, slug: null, status: 'draft' }
    const wrapper = mountView({
      status: 'found',
      post: draft,
      blog: books,
      author: anna,
      path: postPath(books, draft),
      own: false,
    })
    expect(wrapper.find('h1').exists()).toBe(false)
    expect(wrapper.get('[data-testid="post-author"]').attributes('href')).toBe('/u/anna')
    expect(wrapper.get('[data-testid="post-blog"]').attributes('href')).toBe('/books')
    expect(wrapper.get('[data-testid="post-draft"]').text()).toContain('черновик')
    expect(wrapper.find('[data-testid="post-date"]').exists()).toBe(false)
  })

  it('goes without the author when their blog cannot be read', () => {
    const wrapper = mountView({
      status: 'found',
      post,
      blog: books,
      author: null,
      path: postPath(books, post),
      own: false,
    })
    expect(wrapper.find('[data-testid="post-author"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="post-blog"]').text()).toBe('Книги')
  })

  // E07-04: the action panel is a toolbar after the blocks; the like waits for E10-01, sharing copies the link.
  it('has the action panel with the like, the comments and sharing', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true })
    const wrapper = mountView({
      status: 'found',
      post,
      blog: anna,
      author: anna,
      path: postPath(anna, post),
      own: false,
    })
    const bar = wrapper.get('[role="toolbar"]')
    expect(bar.attributes('aria-label')).toBe('Действия с постом')
    // Without the likes the page reads (E10-01) the like is shown and disabled; tests/likes covers it with them.
    expect(bar.get('[data-testid="post-like"]').attributes('disabled')).toBeDefined()
    expect(bar.get('[data-testid="post-comments"]').attributes('href')).toBe('#comments')
    expect(wrapper.find('#comments').exists()).toBe(true)
    await bar.get('[data-testid="post-share"]').trigger('click')
    expect(writeText).toHaveBeenCalledWith(`${window.location.origin}/u/anna/${id}-privet-mir`)
  })

  // E07-05 (ADR-0053): the «…» menu in the header holds the author's items for their own post.
  it('puts the menu of the author or of a reader into the header', async () => {
    for (const [own, item] of [
      [true, 'Удалить'],
      [false, 'Скопировать ссылку'],
    ] as const) {
      const wrapper = mountView({
        status: 'found',
        post,
        blog: anna,
        author: anna,
        path: postPath(anna, post),
        own,
      })
      await wrapper.get('[data-testid="post-header"] button[aria-label="Меню поста"]').trigger('click')
      expect(wrapper.get('[role="menu"]').text()).toBe(item)
      wrapper.unmount()
    }
  })

  it('moves between the buttons of the panel with the arrow keys', async () => {
    const wrapper = mount(PostView, {
      props: {
        state: { status: 'found', post, blog: anna, author: anna, path: postPath(anna, post), own: false },
      },
      global: { stubs },
      attachTo: document.body,
    })
    const comments = wrapper.get('[data-testid="post-comments"]').element as HTMLElement
    const share = wrapper.get('[data-testid="post-share"]').element as HTMLElement
    comments.focus()
    await wrapper.get('[role="toolbar"]').trigger('keydown', { key: 'ArrowRight' })
    expect(document.activeElement).toBe(share)
    await wrapper.get('[role="toolbar"]').trigger('keydown', { key: 'ArrowRight' })
    expect(document.activeElement).toBe(comments)
    wrapper.unmount()
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
