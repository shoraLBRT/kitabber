import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  parseReactionsChanged,
  reactionsAfter,
  rowOf,
  sortReactions,
  type ReactionCount,
  type ReactionKind,
} from '../../api/reactions'
import type { ReactionSetState } from '../../components/reactions/ReactionSet.vue'
import ReactionRow from '../../components/reactions/ReactionRow.vue'
import { formatNumber } from '../../i18n'

// The row of reactions with their counts (E10-03, ADR-0072) without a page: the order of the chips, the own reaction
// marked, the counts of the realtime event `reactions.changed`, and the row itself. The page with live counts against
// the fake API is in e2e/reactions.spec.ts.

// The order is also checked with kinds this build does not know (a newer server).
const fire = 'fire' as ReactionKind
const laugh = 'laugh' as ReactionKind

const likes = (count: number, mine = false) =>
  rowOf({ counts: count > 0 ? { like: count } : {}, mine: mine ? 'like' : null })

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

  it('adds the kinds to press that nobody put yet', () => {
    expect(sortReactions([{ kind: 'heart', count: 2, mine: true }], ['like'])).toEqual([
      { kind: 'heart', count: 2, mine: true },
      { kind: 'like', count: 0, mine: false },
    ])
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

  it('takes the counts from the event and keeps the own reaction', () => {
    const change = { target: 'post' as const, targetId: id, counts: { like: 9, clap: 2 } }
    expect(reactionsAfter({ counts: { like: 3 }, mine: 'clap' }, change)).toEqual({
      counts: { like: 9, clap: 2 },
      mine: 'clap',
    })
    expect(reactionsAfter({ counts: { like: 1 }, mine: null }, { ...change, counts: {} })).toEqual({
      counts: {},
      mine: null,
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
    // A kind the page has no picture for is left out; the rest keep the order of sortReactions.
    expect(row.findAll('button[data-kind]').map((c) => c.attributes('data-kind'))).toEqual(['like'])
    const like = row.get('[data-testid="post-like"]')
    expect(like.classes()).toContain('reaction--mine')
    expect(like.attributes('aria-pressed')).toBe('true')
    expect(like.text()).toContain('Нравится')
    expect(row.get('[data-testid="post-like-count"]').text()).toBe(formatNumber(1234))
    expect(formatNumber(1234).replace(/\s/g, ' ')).toBe('1 234')
    expect(row.get('[role="group"]').attributes('aria-label')).toBe('Реакции')

    await like.trigger('click')
    expect(row.emitted('choose')).toEqual([['like']])
  })

  it('keeps the like to press with no count, and only shows the counts when it is not pressable', async () => {
    const empty = mount(ReactionRow, { props: { reactions: likes(0) } })
    expect(empty.findAll('button[data-kind]')).toHaveLength(1)
    expect(empty.find('[data-testid="like-count"]').exists()).toBe(false)

    const own = mount(ReactionRow, {
      props: { reactions: likes(2), pressable: false, hint: 'Свой пост' },
    })
    // Nothing to choose from either.
    expect(own.find('[data-testid="like-choose"]').exists()).toBe(false)
    const like = own.get('button')
    expect(like.attributes('disabled')).toBeDefined()
    expect(like.attributes('aria-pressed')).toBeUndefined()
    expect(like.attributes('title')).toBe('Свой пост')
  })

  it('marks the row busy while the own change is not answered', () => {
    const row = mount(ReactionRow, {
      props: { reactions: likes(1, true), busy: true },
    })
    expect(row.get('button').attributes('aria-busy')).toBe('true')
  })
})

describe('choosing a reaction in ReactionRow', () => {
  const set: ReactionSetState = {
    status: 'found',
    items: [
      { code: 'like', title: 'Нравится' },
      { code: 'heart', title: 'Тепло' },
      { code: 'clap', title: 'Браво' },
      { code: 'unknown', title: 'Новая' },
    ],
  }

  afterEach(() => vi.useRealTimers())

  const mountRow = (props: Record<string, unknown> = {}) =>
    mount(ReactionRow, {
      props: {
        reactions: rowOf({ counts: { like: 3, heart: 1 }, mine: 'heart' }),
        set,
        testid: 'post-like',
        ...props,
      },
      attachTo: document.body,
    })

  it('draws every kind put with its picture and its name, the own one marked', () => {
    const row = mountRow()
    const heart = row.get('[data-testid="post-like-heart"]')
    expect(heart.attributes('aria-pressed')).toBe('true')
    expect(heart.get('img').attributes('data-code')).toBe('heart')
    expect(heart.text()).toContain('Тепло')
    expect(row.get('[data-testid="post-like-heart-count"]').text()).toBe('1')
    expect(row.get('[data-testid="post-like"]').attributes('aria-pressed')).toBe('false')
    row.unmount()
  })

  it('opens the set by «Выбрать реакцию», marks the own one and changes the reaction in one choice', async () => {
    const row = mountRow()
    const chooser = row.get('[data-testid="post-like-choose"]')
    expect(chooser.attributes('aria-expanded')).toBe('false')
    await chooser.trigger('click')
    expect(row.emitted('open')).toHaveLength(1)
    expect(chooser.attributes('aria-expanded')).toBe('true')
    // A code without a picture in this build is not offered.
    const options = row.findAll('[data-testid^="reaction-option-"]')
    expect(options.map((o) => o.attributes('aria-label'))).toEqual(['Нравится', 'Тепло', 'Браво'])
    expect(row.get('[data-testid="reaction-option-heart"]').attributes('aria-pressed')).toBe('true')

    await row.get('[data-testid="reaction-option-clap"]').trigger('click')
    expect(row.emitted('choose')).toEqual([['clap']])
    expect(row.find('[data-testid="reaction-picker"]').exists()).toBe(false)
    row.unmount()
  })

  it('opens the set when the pointer rests on «Выбрать реакцию» and closes it when it leaves', async () => {
    vi.useFakeTimers()
    const row = mountRow()
    const chooser = row.get('[data-testid="post-like-choose"]')
    await chooser.trigger('pointerenter', { pointerType: 'mouse' })
    expect(row.find('[data-testid="reaction-picker"]').exists()).toBe(false)
    await vi.advanceTimersByTimeAsync(300)
    expect(row.find('[data-testid="reaction-picker"]').exists()).toBe(true)
    await chooser.trigger('pointerleave', { pointerType: 'mouse' })
    await vi.advanceTimersByTimeAsync(300)
    expect(row.find('[data-testid="reaction-picker"]').exists()).toBe(false)
    row.unmount()
  })

  it('opens the set on a long press of a chip, and that press is not a choice', async () => {
    vi.useFakeTimers()
    const row = mountRow()
    const like = row.get('[data-testid="post-like"]')

    // A short tap is a press on the chip.
    await like.trigger('pointerdown', { pointerType: 'touch', clientX: 10, clientY: 10 })
    await vi.advanceTimersByTimeAsync(200)
    await like.trigger('pointerup', { pointerType: 'touch' })
    await like.trigger('click')
    expect(row.emitted('choose')).toEqual([['like']])
    expect(row.find('[data-testid="reaction-picker"]').exists()).toBe(false)

    // A finger held still opens the set; the click that ends it chooses nothing.
    await like.trigger('pointerdown', { pointerType: 'touch', clientX: 10, clientY: 10 })
    await vi.advanceTimersByTimeAsync(500)
    expect(row.find('[data-testid="reaction-picker"]').exists()).toBe(true)
    await like.trigger('pointerup', { pointerType: 'touch' })
    await like.trigger('click')
    expect(row.emitted('choose')).toEqual([['like']])

    // A finger that moves is a scroll.
    await row.get('[data-testid="reaction-option-like"]').trigger('click')
    await like.trigger('pointerdown', { pointerType: 'touch', clientX: 10, clientY: 10 })
    await like.trigger('pointermove', { pointerType: 'touch', clientX: 10, clientY: 60 })
    await vi.advanceTimersByTimeAsync(600)
    expect(row.find('[data-testid="reaction-picker"]').exists()).toBe(false)
    row.unmount()
  })

  it('shows the set loading, failed with «Повторить», and empty', async () => {
    const loading = mountRow({ set: null })
    await loading.get('[data-testid="post-like-choose"]').trigger('click')
    expect(loading.get('[data-testid="reaction-picker"]').attributes('aria-busy')).toBe('true')
    expect(loading.get('[data-testid="reaction-picker-loading"]').text()).toBe('Загружаем набор реакций…')
    loading.unmount()

    const failed = mountRow({ set: { status: 'error' } })
    await failed.get('[data-testid="post-like-choose"]').trigger('click')
    expect(failed.get('[data-testid="reaction-picker-error"]').text()).toContain(
      'Не удалось загрузить набор реакций.',
    )
    await failed.get('[data-testid="reaction-picker-error"] button').trigger('click')
    expect(failed.emitted('open')).toHaveLength(2)
    failed.unmount()

    const empty = mountRow({ set: { status: 'found', items: [] } })
    await empty.get('[data-testid="post-like-choose"]').trigger('click')
    expect(empty.get('[data-testid="reaction-picker-empty"]').text()).toBe('Реакций пока нет.')
    empty.unmount()
  })

  it('closes the set by Escape, giving the focus back to «Выбрать реакцию»', async () => {
    const row = mountRow()
    const chooser = row.get('[data-testid="post-like-choose"]')
    await chooser.trigger('click')
    await row.get('[data-testid="reaction-option-like"]').trigger('keydown', { key: 'Escape' })
    expect(row.find('[data-testid="reaction-picker"]').exists()).toBe(false)
    expect(document.activeElement).toBe(chooser.element)
    row.unmount()
  })
})
