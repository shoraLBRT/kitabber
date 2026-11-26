import { mount } from '@vue/test-utils'
import { readdirSync, readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it, vi } from 'vitest'
import { loadReactionSet, type ReactionSetItem } from '../../api/reactions'
import type { ApiClient } from '../../api/client'
import ReactionIcon from '../../components/reactions/ReactionIcon.vue'
import ReactionSet from '../../components/reactions/ReactionSet.vue'
import { reactionGraphic, reactionGraphics, reactionGraphicsSource } from '../../utils/reaction-graphics'

// The set of reactions (E02-12, ADR-0095) without a page: the bundled pictures and their licence, the picture of one
// reaction, the set in its loading, error, empty and found states, and the call that reads it. The page against the
// fake API is in e2e/reaction-set.spec.ts.

const here = dirname(fileURLToPath(import.meta.url))
const assets = join(here, '../../../brand/reactions')
// The codes of ReactionGraphics in the Reactions module: the server offers only these.
const serverCodes = readFileSync(join(here, '../../../backend/src/Modules/Reactions/ReactionSet.cs'), 'utf8')
  .match(/Codes \{ get; \} = \[([^\]]*)\]/)![1]!
  .match(/"([a-z]+)"/g)!
  .map((quoted) => quoted.slice(1, -1))

const set: ReactionSetItem[] = [
  { code: 'like', title: 'Нравится' },
  { code: 'heart', title: 'Тепло' },
]

describe('the pictures of reactions', () => {
  it('has one SVG for every code the server offers, and nothing else', () => {
    const files = readdirSync(assets)
      .filter((file) => file.endsWith('.svg'))
      .map((file) => file.slice(0, -'.svg'.length))
    expect(files.sort()).toEqual([...serverCodes].sort())
    expect(Object.keys(reactionGraphics).sort()).toEqual([...serverCodes].sort())
  })

  it('keeps the licence of the pictures next to them, as MIT asks', () => {
    const license = readFileSync(join(assets, 'LICENSE'), 'utf8')
    expect(license).toContain('MIT License')
    expect(license).toContain('Copyright (c) Microsoft Corporation.')
    expect(reactionGraphicsSource.license).toBe('MIT')
  })

  it('ships plain vector pictures with no scripts or outside links', () => {
    for (const code of serverCodes) {
      const svg = readFileSync(join(assets, `${code}.svg`), 'utf8')
      expect(svg.startsWith('<svg')).toBe(true)
      expect(svg).not.toMatch(/<script|<foreignObject|href=|<image/i)
    }
  })

  it('has no picture for a code it does not know', () => {
    expect(reactionGraphic('like')).toBeTruthy()
    expect(reactionGraphic('dislike')).toBeNull()
    expect(reactionGraphic('toString')).toBeNull()
  })
})

describe('the picture of one reaction', () => {
  it('draws the bundled picture at a fixed size, named for screen readers when it has a title', () => {
    const icon = mount(ReactionIcon, { props: { code: 'heart', title: 'Тепло', size: 'lg' } })
    const img = icon.get('img')
    expect(img.attributes('src')).toBe(reactionGraphic('heart'))
    expect(img.attributes('alt')).toBe('Тепло')
    expect(img.attributes('aria-hidden')).toBeUndefined()
    expect(img.attributes('width')).toBe('40')
    expect(img.attributes('height')).toBe('40')
  })

  it('is decoration without a title, and draws nothing for an unknown code', () => {
    const decorative = mount(ReactionIcon, { props: { code: 'like' } })
    expect(decorative.get('img').attributes('alt')).toBe('')
    expect(decorative.get('img').attributes('aria-hidden')).toBe('true')
    expect(
      mount(ReactionIcon, { props: { code: 'dislike' } })
        .find('img')
        .exists(),
    ).toBe(false)
  })
})

describe('the set of reactions', () => {
  it('shows placeholders while loading, told to screen readers', () => {
    const view = mount(ReactionSet, { props: { state: { status: 'loading' } } })
    expect(view.get('[data-testid="reaction-set-loading"]').attributes('aria-busy')).toBe('true')
    expect(view.get('[role="status"]').text()).toBe('Загружаем набор реакций…')
  })

  it('says it failed and offers to try again', async () => {
    const view = mount(ReactionSet, { props: { state: { status: 'error' } } })
    expect(view.get('[role="alert"]').text()).toContain('Не удалось загрузить набор реакций.')
    await view.get('button').trigger('click')
    expect(view.emitted('retry')).toHaveLength(1)
  })

  it('says there are no reactions when the directory offers none it can draw', () => {
    for (const items of [[], [{ code: 'dislike', title: 'Нет' }]]) {
      const view = mount(ReactionSet, { props: { state: { status: 'found', items } } })
      expect(view.get('[data-testid="state"]').text()).toBe('Реакций пока нет.')
    }
  })

  it('shows every reaction with its picture and name, in the directory order', () => {
    const view = mount(ReactionSet, {
      props: { state: { status: 'found', items: [...set, { code: 'dislike', title: 'Нет' }] } },
    })
    const items = view.findAll('[data-testid="reaction-set-item"]')
    expect(items.map((item) => item.text())).toEqual(['Нравится', 'Тепло'])
    expect(items.map((item) => item.get('img').attributes('data-code'))).toEqual(['like', 'heart'])
    expect(view.get('ul').attributes('aria-label')).toBe('Набор реакций')
  })
})

describe('reading the set', () => {
  it('returns the items, or an error when the API fails or does not answer', async () => {
    const answer = (data: unknown) => ({ GET: vi.fn().mockResolvedValue({ data }) }) as unknown as ApiClient
    expect(await loadReactionSet(answer(set))).toEqual({ status: 'found', items: set })
    expect(await loadReactionSet(answer(undefined))).toEqual({ status: 'error' })
    const broken = { GET: vi.fn().mockRejectedValue(new Error('offline')) } as unknown as ApiClient
    expect(await loadReactionSet(broken)).toEqual({ status: 'error' })
  })
})
