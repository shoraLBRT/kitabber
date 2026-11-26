import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it, vi } from 'vitest'
import {
  appendTopics,
  loadTopicCatalog,
  loadTopicMenu,
  topicMenuItems,
  type TopicCard,
} from '../../api/topics'
import type { ApiClient } from '../../api/client'

// The catalogue's and the menu's requests and how their answers are shown (E11-02, ADR-0088).
const card = (id: string, name: string): TopicCard => ({
  id,
  name,
  nickname: name,
  description: null,
  subscriberCount: 0,
})

function client(answer: unknown) {
  const GET = vi.fn(async () => answer)
  return { GET, api: { GET } as unknown as ApiClient }
}

const frontend = join(dirname(fileURLToPath(import.meta.url)), '..', '..')
const read = (...path: string[]) => readFileSync(join(frontend, ...path), 'utf8')

describe('the topic catalogue', () => {
  it('asks without a search for every topic and with a trimmed one when there is text', async () => {
    const { GET, api } = client({ data: { items: [], nextCursor: null } })
    await loadTopicCatalog(api, '   ')
    await loadTopicCatalog(api, ' кино ', 'next')
    expect(GET.mock.calls).toEqual([
      ['/api/v1/topics/catalog', { params: { query: {} } }],
      ['/api/v1/topics/catalog', { params: { query: { q: 'кино', cursor: 'next' } } }],
    ])
  })

  it('is a failure when the API does not answer', async () => {
    expect(await loadTopicCatalog(client({ data: undefined }).api, '')).toEqual({ status: 'error' })
    const broken = { GET: vi.fn(async () => Promise.reject(new Error('down'))) } as unknown as ApiClient
    expect(await loadTopicCatalog(broken, '')).toEqual({ status: 'error' })
  })

  it('adds a later page without repeating a topic already shown', () => {
    const shown = [card('1', 'a'), card('2', 'b')]
    expect(
      appendTopics(shown, { items: [card('2', 'b'), card('3', 'c')], nextCursor: null }).map((t) => t.id),
    ).toEqual(['1', '2', '3'])
  })
})

describe('the menu topics', () => {
  it('lead to the topic pages', () => {
    expect(
      topicMenuItems({ source: 'popular', topics: [{ id: '1', name: 'Книги', nickname: 'books' }] }),
    ).toEqual([{ id: '1', label: 'Книги', to: '/books' }])
    expect(topicMenuItems(null)).toEqual([])
  })

  it('are null when the API does not answer', async () => {
    expect(await loadTopicMenu(client({ data: undefined }).api)).toBeNull()
  })

  it('reach the left menu and the phone «Меню» screen', () => {
    expect(read('layouts', 'default.vue')).toMatch(/useTopicMenu\(\)[\s\S]*:topics="topics"/)
    expect(read('pages', 'menu.vue')).toMatch(/useTopicMenu\(\)[\s\S]*:topics="topics"/)
  })
})
