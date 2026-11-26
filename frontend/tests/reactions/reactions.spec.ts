import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import {
  likesAfter,
  parseReactionsChanged,
  reactionsOfLikes,
  sortReactions,
  type ReactionCount,
  type ReactionKind,
} from '../../api/reactions'
import ReactionRow from '../../components/reactions/ReactionRow.vue'
import { formatNumber } from '../../i18n'

// The row of reactions with their counts (E10-03, ADR-0072) without a page: the order of the chips, the own reaction
// marked, the counts of the realtime event `reactions.changed`, and the row itself. The page with live counts against
// the fake API is in e2e/reactions.spec.ts.

// Only «Нравится» exists so far; the order is checked with kinds the set of reactions (E10-02) will add.
const fire = 'fire' as ReactionKind
const laugh = 'laugh' as ReactionKind

describe('the order of reactions', () => {
  it('puts the most popular first and leaves out the kinds nobody put', () => {
    const reactions: ReactionCount[] = [
      { kind: 'like', count: 2, mine: false },
      { kind: fire, count: 7, mine: false },
      { kind: laugh, count: 0, mine: false },
    ]
    expect(sortReactions(reactions).map((r) => r.kind)).toEqual([fire, 'like'])
  })

  it('keeps the order of the kinds for equal counts and the pressable kind with no count', () => {
    expect(
      sortReactions([
        { kind: fire, count: 3, mine: false },
        { kind: 'like', count: 3, mine: true },
      ]).map((r) => r.kind),
    ).toEqual(['like', fire])
    expect(sortReactions([{ kind: 'like', count: 0, mine: false }], ['like'])).toEqual([
      { kind: 'like', count: 0, mine: false },
    ])
  })

  it('turns the likes of a target into its reactions', () => {
    expect(reactionsOfLikes({ count: 4, liked: true })).toEqual([{ kind: 'like', count: 4, mine: true }])
  })
})

describe('the realtime event of new counts', () => {
  const id = '0195f3a0-0000-7000-8000-0000000000c1'

  it('reads the counts of the target, skipping kinds the page does not know', () => {
    expect(
      parseReactionsChanged({
        target: 'comment',
        targetId: id,
        reactions: [
          { kind: 'like', count: 12 },
          { kind: 'unknown', count: 3 },
        ],
      }),
    ).toEqual({ target: 'comment', targetId: id, counts: { like: 12 } })
    expect(parseReactionsChanged({ target: 'post', targetId: id, reactions: [] })).toEqual({
      target: 'post',
      targetId: id,
      counts: {},
    })
  })

  it('refuses data that is not such an event', () => {
    expect(parseReactionsChanged(null)).toBeNull()
    expect(parseReactionsChanged({ target: 'blog', targetId: id, reactions: [] })).toBeNull()
    expect(parseReactionsChanged({ target: 'post', targetId: 1, reactions: [] })).toBeNull()
    expect(parseReactionsChanged({ target: 'post', targetId: id })).toBeNull()
    expect(
      parseReactionsChanged({ target: 'post', targetId: id, reactions: [{ kind: 'like', count: -1 }] }),
    ).toEqual({ target: 'post', targetId: id, counts: {} })
  })

  it('takes the count from the event and keeps the own mark', () => {
    const change = { target: 'post' as const, targetId: id, counts: { like: 9 } }
    expect(likesAfter({ count: 3, liked: true }, change)).toEqual({ count: 9, liked: true })
    expect(likesAfter({ count: 1, liked: false }, { ...change, counts: {} })).toEqual({
      count: 0,
      liked: false,
    })
  })
})

describe('ReactionRow', () => {
  it('shows the chips most popular first, the own one marked, the counts by the locale', async () => {
    const row = mount(ReactionRow, {
      props: {
        reactions: [
          { kind: fire, count: 5000, mine: false },
          { kind: 'like', count: 1234, mine: true },
        ],
        testid: 'post-like',
      },
    })
    // A kind the page has no icon for is left out; the rest keep the order of sortReactions.
    expect(row.findAll('button').map((c) => c.attributes('data-kind'))).toEqual(['like'])
    const like = row.get('[data-testid="post-like"]')
    expect(like.classes()).toContain('reaction--mine')
    expect(like.attributes('aria-pressed')).toBe('true')
    expect(like.text()).toContain('Нравится')
    expect(row.get('[data-testid="post-like-count"]').text()).toBe(formatNumber(1234))
    expect(formatNumber(1234).replace(/\s/g, ' ')).toBe('1 234')
    expect(row.get('[role="group"]').attributes('aria-label')).toBe('Реакции')

    await like.trigger('click')
    expect(row.emitted('toggle')).toEqual([['like']])
  })

  it('keeps the like to press with no count, and only shows the counts when it is not pressable', async () => {
    const empty = mount(ReactionRow, { props: { reactions: reactionsOfLikes({ count: 0, liked: false }) } })
    expect(empty.findAll('button')).toHaveLength(1)
    expect(empty.find('[data-testid="like-count"]').exists()).toBe(false)

    const own = mount(ReactionRow, {
      props: { reactions: reactionsOfLikes({ count: 2, liked: false }), pressable: false, hint: 'Свой пост' },
    })
    const like = own.get('button')
    expect(like.attributes('disabled')).toBeDefined()
    expect(like.attributes('aria-pressed')).toBeUndefined()
    expect(like.attributes('title')).toBe('Свой пост')
  })

  it('marks the row busy while the own change is not answered', () => {
    const row = mount(ReactionRow, {
      props: { reactions: reactionsOfLikes({ count: 1, liked: true }), busy: true },
    })
    expect(row.get('button').attributes('aria-busy')).toBe('true')
  })
})
