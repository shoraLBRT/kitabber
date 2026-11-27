// E04-12 (#109, ADR-0116): the section «Закладки» — the tabs «Посты» and «Комментарии» in the order the server gives
// (newest bookmark first), a deleted post or comment as a stub in its place, the states (loading, error with
// «Повторить», empty, «Показать ещё»), the next page appended without repeats. Against the fake API in both profiles,
// with the owner-only reading: e2e/bookmark-section.spec.ts.
import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import BookmarkSectionList from '../../components/bookmarks/BookmarkSectionList.vue'
import {
  appendBookmarks,
  bookmarkedId,
  type BookmarkedComment,
  type BookmarkedPost,
  type BookmarkSectionTab,
} from '../../api/bookmark-section'
import { blogCommentPath } from '../../api/blog-comments'
import type { FeedBlog } from '../../api/feeds'
import type { Post } from '../../api/posts'
import type { BookmarkSectionState } from '../../composables/useBookmarkSection'

const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

const annaAccount = '0195f3a0-0000-7000-8000-000000000001'
const anna: FeedBlog = {
  id: '0195f3a0-0000-7000-8000-0000000000b1',
  type: 'personal',
  name: 'Анна',
  nickname: 'anna',
  avatarSources: [],
}

function bookmarkedPost(id: string, title: string | null, gone = false): BookmarkedPost {
  const post: Post = {
    id,
    blogId: anna.id,
    authorAccountId: annaAccount,
    title,
    slug: null,
    body: { version: 1, blocks: [] } as unknown as Post['body'],
    status: 'published',
    origin: 'native',
    source: null,
    createdAt: '2026-11-20T10:00:00Z',
    updatedAt: '2026-11-20T10:00:00Z',
    publishedAt: '2026-11-20T10:05:00Z',
    images: {},
    imagePreviews: {},
    viewCount: 0,
  }
  return {
    postId: id,
    bookmarkedAt: '2026-11-27T10:00:00Z',
    card: gone ? null : { post, author: anna, topic: null, bestComment: null },
  }
}

function bookmarkedComment(id: string, text: string | null): BookmarkedComment {
  const postId = '0195f3a0-0000-7000-8000-0000000000c1'
  return {
    commentId: id,
    postId,
    bookmarkedAt: '2026-11-27T10:00:00Z',
    comment:
      text === null
        ? null
        : {
            id,
            postId,
            postBlogId: anna.id,
            postTitle: 'Новости дня',
            postSlug: 'novosti-dnya',
            kind: 'native',
            authorAccountId: annaAccount,
            guest: null,
            text,
            createdAt: '2026-11-27T09:00:00Z',
            editedAt: null,
          },
  }
}

function mountList(
  tab: BookmarkSectionTab,
  state: BookmarkSectionState<BookmarkedPost | BookmarkedComment>,
  extra: { loadingMore?: boolean; moreFailed?: boolean } = {},
) {
  return mount(BookmarkSectionList, {
    props: { tab, state, authors: new Map(), ...extra },
    global: { stubs },
  })
}

describe('appendBookmarks', () => {
  it('adds the next page after the shown bookmarks, each once', () => {
    const first = bookmarkedPost('0195f3a0-0000-7000-8000-0000000000c1', 'Первый')
    const second = bookmarkedPost('0195f3a0-0000-7000-8000-0000000000c2', 'Второй')
    expect(appendBookmarks([first], [first, second]).map(bookmarkedId)).toEqual([first.postId, second.postId])
  })
})

describe('BookmarkSectionList', () => {
  it('shows the posts as feed cards in the order given, a deleted one as a stub in its place', () => {
    const wrapper = mountList('posts', {
      status: 'found',
      items: [
        bookmarkedPost('0195f3a0-0000-7000-8000-0000000000c2', 'Новее'),
        bookmarkedPost('0195f3a0-0000-7000-8000-0000000000c3', null, true),
        bookmarkedPost('0195f3a0-0000-7000-8000-0000000000c1', 'Старше'),
      ],
      hasMore: false,
    })

    const items = wrapper.findAll('[data-testid="bookmarked-post"]')
    expect(items).toHaveLength(3)
    expect(items[0]!.get('h2').text()).toBe('Новее')
    expect(items[1]!.get('[data-testid="bookmark-stub"]').text()).toBe('Пост удалён или скрыт автором.')
    expect(items[2]!.get('h2').text()).toBe('Старше')
  })

  it('shows a comment with the link to it under its post, a deleted one as a stub', () => {
    const shown = bookmarkedComment('0195f3a0-0000-7000-8000-0000000000d1', 'Отличный разбор')
    const wrapper = mountList('comments', {
      status: 'found',
      items: [shown, bookmarkedComment('0195f3a0-0000-7000-8000-0000000000d2', null)],
      hasMore: false,
    })

    const items = wrapper.findAll('[data-testid="bookmarked-comment"]')
    const post = items[0]!.get('[data-testid="bookmarked-comment-post"]')
    expect(post.text()).toBe('Новости дня')
    expect(post.attributes('href')).toBe(blogCommentPath(shown.comment!))
    expect(items[0]!.get('[data-testid="bookmarked-comment-text"]').text()).toBe('Отличный разбор')
    expect(items[1]!.get('[data-testid="bookmark-stub"]').text()).toBe('Комментарий удалён.')
  })

  it('shows a skeleton while loading', () => {
    expect(mountList('posts', { status: 'loading' }).find('[data-testid="bookmarks-loading"]').exists()).toBe(
      true,
    )
  })

  it('shows the error with «Повторить»', async () => {
    const wrapper = mountList('posts', { status: 'error' })
    expect(wrapper.get('[data-testid="bookmarks-error"]').text()).toContain('Не удалось загрузить закладки.')
    await wrapper.get('button').trigger('click')
    expect(wrapper.emitted('retry')).toHaveLength(1)
  })

  it('names the empty tab', () => {
    expect(mountList('posts', { status: 'empty' }).get('[data-testid="bookmarks-empty"]').text()).toBe(
      'В закладках пока нет постов.',
    )
    expect(mountList('comments', { status: 'empty' }).get('[data-testid="bookmarks-empty"]').text()).toBe(
      'В закладках пока нет комментариев.',
    )
  })

  it('offers «Показать ещё» while more are left, and «Повторить» after a failed page', async () => {
    const state = {
      status: 'found' as const,
      items: [bookmarkedComment('0195f3a0-0000-7000-8000-0000000000d1', 'Текст')],
      hasMore: true,
    }
    const wrapper = mountList('comments', state)
    await wrapper.get('[data-testid="bookmarks-more"]').trigger('click')
    expect(wrapper.emitted('more')).toHaveLength(1)

    const failed = mountList('comments', state, { moreFailed: true })
    expect(failed.get('[role="alert"]').text()).toBe('Не удалось загрузить следующие закладки.')
    expect(failed.get('[data-testid="bookmarks-more"]').text()).toBe('Повторить')
  })
})
