import { describe, expect, it } from 'vitest'
import {
  commentCount,
  loadAuthors,
  loadBranch,
  loadComments,
  nestComments,
  repliesText,
  unknownAuthors,
  withBranch,
} from '../../api/comments'
import type { ApiClient } from '../../api/client'
import { comment } from './factory'

// The comment tree of E09-02 (ADR-0054) without a page: nesting the ordered list, the replies a read left out, a
// loaded branch in place of its comment's part, the counts and the reads themselves.

describe('nestComments', () => {
  it('nests the replies under their comment in the order of the tree', () => {
    const roots = nestComments([
      comment('1', null, 1, 2),
      comment('1.1', '1', 2, 1),
      comment('1.1.1', '1.1', 3),
      comment('2', null, 1, 1),
      comment('2.1', '2', 2),
    ])

    expect(roots.map((node) => node.comment.id)).toEqual(['1', '2'])
    expect(roots[0]!.replies.map((node) => node.comment.id)).toEqual(['1.1'])
    expect(roots[0]!.replies[0]!.replies.map((node) => node.comment.id)).toEqual(['1.1.1'])
    expect(roots.map((node) => node.hidden)).toEqual([0, 0])
  })

  it('counts the replies a read left out', () => {
    const roots = nestComments([
      comment('1', null, 1, 5),
      comment('1.1', '1', 2, 3),
      comment('2', null, 1, 4),
    ])

    expect(roots[0]!.hidden).toBe(1)
    expect(roots[0]!.replies[0]!.hidden).toBe(3)
    expect(roots[1]!.hidden).toBe(4)
  })

  it('nests a thousand comments at once', () => {
    const many = Array.from({ length: 250 }, (_, n) => [
      comment(`${n}`, null, 1, 3),
      comment(`${n}.1`, `${n}`, 2, 2),
      comment(`${n}.1.1`, `${n}.1`, 3, 1),
      comment(`${n}.1.1.1`, `${n}.1.1`, 4),
    ]).flat()
    const started = performance.now()

    const roots = nestComments(many)

    expect(performance.now() - started).toBeLessThan(100)
    expect(roots).toHaveLength(250)
    expect(commentCount(many)).toBe(1000)
  })
})

describe('withBranch', () => {
  it('puts a loaded branch in place of its comment and what was under it', () => {
    const shown = [comment('1', null, 1, 3), comment('1.1', '1', 2, 2), comment('2', null, 1)]
    const branch = [comment('1.1', '1', 2, 2), comment('1.1.1', '1.1', 3, 1), comment('1.1.1.1', '1.1.1', 4)]

    const merged = withBranch(shown, branch)

    expect(merged.map((c) => c.id)).toEqual(['1', '1.1', '1.1.1', '1.1.1.1', '2'])
    expect(nestComments(merged).map((node) => node.hidden)).toEqual([0, 0])
  })

  it('changes nothing for a branch of a comment that is not shown', () => {
    const shown = [comment('1', null, 1)]
    expect(withBranch(shown, [comment('9', null, 1)])).toEqual(shown)
    expect(withBranch(shown, [])).toEqual(shown)
  })
})

describe('counts and words', () => {
  it('counts every comment under the post', () => {
    expect(commentCount([comment('1', null, 1, 5), comment('1.1', '1', 2, 4), comment('2', null, 1)])).toBe(7)
    expect(commentCount([])).toBe(0)
  })

  it('names the number of replies', () => {
    expect([1, 3, 5, 21, 12].map(repliesText)).toEqual([
      '1 ответ',
      '3 ответа',
      '5 ответов',
      '21 ответ',
      '12 ответов',
    ])
  })

  it('asks for each unknown author once', () => {
    const guest = { ...comment('3', null, 1), kind: 'guest', authorAccountId: null }
    const list = [
      comment('1', null, 1, 0, 'a1'),
      comment('2', null, 1, 0, 'a2'),
      comment('4', null, 1),
      guest,
    ]
    expect(unknownAuthors(list, new Map([['a2', null]]))).toEqual(['a1'])
  })
})

describe('reads', () => {
  function client(get: (path: string, init: unknown) => Promise<{ data?: unknown }>): ApiClient {
    return { GET: get } as unknown as ApiClient
  }

  it('reads the tree and a branch some levels down', async () => {
    const calls: unknown[] = []
    const tree = { postId: 'p1', maxDepth: 8, comments: [comment('1', null, 1)] }
    const api = client(async (path, init) => {
      calls.push([path, init])
      return { data: tree }
    })

    expect(await loadComments(api, 'p1', 6)).toEqual({ status: 'found', tree })
    expect(await loadBranch(api, '1', 3)).toEqual({ status: 'found', tree })
    expect(calls).toEqual([
      ['/api/v1/posts/{postId}/comments', { params: { path: { postId: 'p1' }, query: { levels: 6 } } }],
      ['/api/v1/comments/{id}/branch', { params: { path: { id: '1' }, query: { levels: 3 } } }],
    ])
  })

  it('reports a failed read', async () => {
    const failing = client(async () => {
      throw new Error('offline')
    })
    expect(
      await loadComments(
        client(async () => ({})),
        'p1',
        6,
      ),
    ).toEqual({ status: 'error' })
    expect(await loadBranch(failing, '1', 6)).toEqual({ status: 'error' })
  })

  it('reads the authors a few at a time and leaves out the ones that fail', async () => {
    let open = 0
    let most = 0
    const api = client(async (_path, init) => {
      open++
      most = Math.max(most, open)
      await new Promise((resolve) => setTimeout(resolve, 1))
      open--
      const { accountId } = (init as { params: { path: { accountId: string } } }).params.path
      if (accountId === 'bad') throw new Error('offline')
      return { data: { id: `blog-${accountId}` } }
    })
    const ids = [...Array.from({ length: 20 }, (_, n) => `a${n}`), 'bad']

    const found = await loadAuthors(api, ids)

    expect(found.size).toBe(21)
    expect(found.get('a3')).toEqual({ id: 'blog-a3' })
    expect(found.get('bad')).toBeNull()
    expect(most).toBeLessThanOrEqual(6)
  })
})
