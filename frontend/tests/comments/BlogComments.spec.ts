// E09-15 (#107, ADR-0114): the tab «Комментарии» of a blog and a topic — the list with the post of each comment and the
// link to the comment, its states (loading, error with «Повторить», empty, «Показать ещё»), the next page appended
// without repeats, and the topic's tabs. Against the fake API in both profiles: e2e/blog-comments.spec.ts.
import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import BlogCommentList from '../../components/comments/BlogCommentList.vue'
import TopicView from '../../components/topic/TopicView.vue'
import { appendBlogComments, blogCommentPath, type BlogComment } from '../../api/blog-comments'
import type { Profile } from '../../api/profiles'
import type { BlogCommentsState } from '../../composables/useBlogComments'

const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

const annaAccount = '0195f3a0-0000-7000-8000-000000000001'

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
  subscriberCount: 0,
  subscribed: false,
  cover: null,
  accountId: annaAccount,
  blocked: false,
  onboardingPending: false,
}

function comment(extra: Partial<BlogComment> = {}): BlogComment {
  return {
    id: '0195f3a0-0000-7000-8000-0000000000d1',
    postId: '0195f3a0-0000-7000-8000-0000000000c1',
    postBlogId: anna.id,
    postTitle: 'Новости дня',
    postSlug: 'novosti-dnya',
    kind: 'native',
    authorAccountId: annaAccount,
    guest: null,
    text: 'Отличный разбор',
    createdAt: '2026-11-27T10:00:00Z',
    editedAt: null,
    ...extra,
  }
}

function mountList(state: BlogCommentsState, extra: { loadingMore?: boolean; moreFailed?: boolean } = {}) {
  return mount(BlogCommentList, {
    props: { state, authors: new Map([[annaAccount, anna]]), ...extra },
    global: { stubs },
  })
}

describe('blogCommentPath', () => {
  it('leads to the comment on its post by the blog id', () => {
    expect(blogCommentPath(comment())).toBe(
      `/u/${anna.id}/0195f3a0-0000-7000-8000-0000000000c1-novosti-dnya#comment-0195f3a0-0000-7000-8000-0000000000d1`,
    )
  })

  it('leads to a post without a slug by its id alone', () => {
    expect(blogCommentPath(comment({ postSlug: null }))).toBe(
      `/u/${anna.id}/0195f3a0-0000-7000-8000-0000000000c1#comment-0195f3a0-0000-7000-8000-0000000000d1`,
    )
  })
})

describe('appendBlogComments', () => {
  it('adds the next page after the shown comments, each once', () => {
    const first = comment()
    const second = comment({ id: '0195f3a0-0000-7000-8000-0000000000d2' })
    expect(appendBlogComments([first], [first, second]).map((c) => c.id)).toEqual([first.id, second.id])
  })
})

describe('BlogCommentList', () => {
  it('shows each comment with its post, which leads to the comment, its author and its text', () => {
    const wrapper = mountList({ status: 'found', comments: [comment()], hasMore: false })

    const post = wrapper.get('[data-testid="blog-comment-post"]')
    expect(post.text()).toBe('Новости дня')
    expect(post.attributes('href')).toBe(blogCommentPath(comment()))
    expect(post.attributes('aria-label')).toBe('К комментарию под постом «Новости дня»')
    expect(wrapper.get('[data-testid="blog-comment-author"]').text()).toBe('Анна')
    expect(wrapper.get('[data-testid="blog-comment-author"]').attributes('href')).toBe('/u/anna')
    expect(wrapper.get('[data-testid="blog-comment-text"]').text()).toBe('Отличный разбор')
    expect(wrapper.find('[data-testid="blog-comments-more"]').exists()).toBe(false)
  })

  it('names a guest comment by its guest and a post without a title by a placeholder', () => {
    const wrapper = mountList({
      status: 'found',
      comments: [
        comment({
          kind: 'guest',
          authorAccountId: null,
          guest: { name: 'Иван из Telegram', url: null },
          postTitle: null,
        }),
      ],
      hasMore: false,
    })

    expect(wrapper.get('[data-testid="blog-comment-author"]').text()).toBe('Иван из Telegram')
    expect(wrapper.get('[data-testid="blog-comment-author"]').element.tagName).toBe('SPAN')
    expect(wrapper.get('[data-testid="blog-comment-post"]').text()).toBe('Пост без заголовка')
  })

  it('shows a skeleton while loading', () => {
    const wrapper = mountList({ status: 'loading' })
    expect(wrapper.get('[data-testid="blog-comments-loading"]').attributes('aria-busy')).toBe('true')
    expect(wrapper.get('[role="status"]').text()).toBe('Загрузка комментариев')
  })

  it('shows the error with «Повторить»', async () => {
    const wrapper = mountList({ status: 'error' })
    expect(wrapper.get('[role="alert"]').text()).toContain('Не удалось загрузить комментарии')
    await wrapper.get('button').trigger('click')
    expect(wrapper.emitted('retry')).toHaveLength(1)
  })

  it('shows «Пока нет комментариев» when there are none', () => {
    const wrapper = mountList({ status: 'empty' })
    expect(wrapper.get('[data-testid="blog-comments-empty"]').text()).toBe('Пока нет комментариев')
  })

  it('offers «Показать ещё» while more are left, and «Повторить» after a failed page', async () => {
    const more = mountList({ status: 'found', comments: [comment()], hasMore: true })
    const button = more.get('[data-testid="blog-comments-more"]')
    expect(button.text()).toBe('Показать ещё')
    await button.trigger('click')
    expect(more.emitted('more')).toHaveLength(1)

    const failed = mountList({ status: 'found', comments: [comment()], hasMore: true }, { moreFailed: true })
    expect(failed.get('[role="alert"]').text()).toBe('Не удалось загрузить следующие комментарии.')
    expect(failed.get('[data-testid="blog-comments-more"]').text()).toBe('Повторить')

    const loading = mountList(
      { status: 'found', comments: [comment()], hasMore: true },
      { loadingMore: true },
    )
    expect(loading.get('[data-testid="blog-comments-more"]').attributes('disabled')).toBeDefined()
  })
})

describe('TopicView tabs', () => {
  const books: Profile = { ...anna, id: '0195f3a0-0000-7000-8000-0000000000b4', type: 'topic', name: 'Книги' }

  it('shows «Посты» and «Комментарии» at their addresses, the current one marked', () => {
    const wrapper = mount(TopicView, {
      props: { state: { status: 'found', profile: books }, base: '/books', tab: 'comments' },
      global: { stubs },
    })

    const tabs = wrapper.get('nav[aria-label="Разделы темы"]').findAll('a')
    expect(tabs.map((tab) => [tab.text(), tab.attributes('href')])).toEqual([
      ['Посты', '/books'],
      ['Комментарии', '/books/comments'],
    ])
    expect(tabs[1]!.attributes('aria-current')).toBe('page')
    expect(tabs[0]!.attributes('aria-current')).toBeUndefined()
  })
})
