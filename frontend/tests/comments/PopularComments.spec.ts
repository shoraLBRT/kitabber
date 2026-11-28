// E08-12 (#101, ADR-0105): the widget «Популярные комментарии» of the right column — the list of comments, the link to
// each on its post's page, the state of the widget from the read (loading, error, empty, ready) and the widget's
// states in the column. Against the fake API in both profiles: e2e/popular-comments.spec.ts.
import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import PopularCommentList from '../../components/comments/PopularCommentList.vue'
import ShellAside from '../../components/shell/ShellAside.vue'
import { popularCommentPath, type PopularComment } from '../../api/popular-comments'
import type { Profile } from '../../api/profiles'
import { popularCommentsState } from '../../composables/usePopularComments'

const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

const annaAccount = '0195f3a0-0000-7000-8000-000000000001'
const borisAccount = '0195f3a0-0000-7000-8000-000000000002'

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
  notifyNewPosts: false,
}

function comment(extra: Partial<PopularComment> = {}): PopularComment {
  return {
    id: '0195f3a0-0000-7000-8000-0000000000d1',
    postId: '0195f3a0-0000-7000-8000-0000000000c1',
    postBlogId: anna.id,
    postTitle: 'Новости дня',
    postSlug: 'novosti-dnya',
    authorAccountId: annaAccount,
    text: 'Отличный разбор',
    rating: 3,
    createdAt: '2026-11-27T10:00:00Z',
    ...extra,
  }
}

describe('popularCommentPath', () => {
  it('leads to the comment on its post by the blog id', () => {
    expect(popularCommentPath(comment())).toBe(
      `/u/${anna.id}/0195f3a0-0000-7000-8000-0000000000c1-novosti-dnya#comment-0195f3a0-0000-7000-8000-0000000000d1`,
    )
  })

  it('leads to a post without a slug by its id alone', () => {
    expect(popularCommentPath(comment({ postSlug: null }))).toBe(
      `/u/${anna.id}/0195f3a0-0000-7000-8000-0000000000c1#comment-0195f3a0-0000-7000-8000-0000000000d1`,
    )
  })
})

describe('popularCommentsState', () => {
  it('is loading until the read answers', () => {
    expect(popularCommentsState('idle', null)).toBe('loading')
    expect(popularCommentsState('pending', null)).toBe('loading')
  })

  it('is an error when the read fails', () => {
    expect(popularCommentsState('error', null)).toBe('error')
  })

  it('is empty without comments and ready with them', () => {
    expect(popularCommentsState('success', { comments: [] })).toBe('empty')
    expect(popularCommentsState('success', { comments: [comment()] })).toBe('ready')
  })
})

describe('PopularCommentList', () => {
  it('shows each comment as one link with its author, text and post', () => {
    const w = mount(PopularCommentList, {
      props: { comments: [comment()], authors: new Map([[annaAccount, anna]]) },
      global: { stubs },
    })
    const link = w.get('[data-testid="popular-comment"]')
    expect(link.attributes('href')).toBe(popularCommentPath(comment()))
    expect(w.get('[data-testid="popular-comment-author"]').text()).toBe('Анна')
    expect(w.get('[data-testid="popular-comment-text"]').text()).toBe('Отличный разбор')
    expect(w.get('[data-testid="popular-comment-post"]').text()).toBe('Новости дня')
  })

  it('keeps the order of the server and names an unknown author «Автор»', () => {
    const second = comment({ id: '0195f3a0-0000-7000-8000-0000000000d2', authorAccountId: borisAccount })
    const w = mount(PopularCommentList, {
      props: {
        comments: [comment(), second],
        authors: new Map([
          [annaAccount, anna],
          [borisAccount, null],
        ]),
      },
      global: { stubs },
    })
    expect(w.findAll('[data-testid="popular-comment-author"]').map((a) => a.text())).toEqual([
      'Анна',
      'Автор',
    ])
  })

  it('shows no post line for a post without a title', () => {
    const w = mount(PopularCommentList, {
      props: { comments: [comment({ postTitle: null })], authors: new Map() },
      global: { stubs },
    })
    expect(w.find('[data-testid="popular-comment-post"]').exists()).toBe(false)
  })
})

describe('ShellAside with the popular comments', () => {
  const widget = (state: 'loading' | 'error' | 'empty' | 'ready') =>
    mount(ShellAside, {
      props: { commentsState: state },
      slots: { comments: '<p data-testid="slotted">Комментарии</p>' },
    }).findAll('section')[0]!

  it('shows a skeleton while loading', () => {
    expect(widget('loading').find('[data-testid="widget-skeleton"]').exists()).toBe(true)
  })

  it('shows the error with «Повторить»', () => {
    expect(widget('error').get('[role="alert"]').text()).toContain('Не удалось загрузить')
  })

  it('says there are no popular comments of the day when empty', () => {
    expect(widget('empty').text()).toContain('За сутки популярных комментариев пока нет')
  })

  it('shows the comments when ready', () => {
    expect(widget('ready').find('[data-testid="slotted"]').exists()).toBe(true)
  })

  it('asks to retry the comments widget', async () => {
    const w = mount(ShellAside, { props: { commentsState: 'error' } })
    await w.get('button').trigger('click')
    expect(w.emitted('retry')).toEqual([['comments']])
  })
})
