import { describe, expect, it } from 'vitest'
import {
  branchPath,
  branchRootOf,
  commentCount,
  commentHash,
  commentOfHash,
  commentPath,
  loadAuthors,
  loadCommentPlace,
  loadBranch,
  loadComments,
  nearestShown,
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

describe('branchPath', () => {
  it('puts a branch screen under its post (E09-06)', () => {
    expect(branchPath('/u/anna/p1-privet', 'c1')).toBe('/u/anna/p1-privet/comments/c1')
    expect(branchPath('/books/p1', 'c1')).toBe('/books/p1/comments/c1')
  })
})

describe('commentHash', () => {
  const id = '0195F3A0-0000-7000-8000-0000000000D1'

  it('names a comment on its post page and reads it back (E08-15)', () => {
    expect(commentHash('c1')).toBe('#comment-c1')
    expect(commentOfHash(commentHash(id))).toBe(id.toLowerCase())
  })

  it('reads nothing from another fragment', () => {
    expect(commentOfHash('')).toBeNull()
    expect(commentOfHash('#comments')).toBeNull()
    expect(commentOfHash('#comment-not-an-id')).toBeNull()
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
    expect(await loadComments(api, 'p1', 6, 'date')).toEqual({ status: 'found', tree })
    expect(await loadBranch(api, '1', 3)).toEqual({ status: 'found', tree })
    expect(calls).toEqual([
      [
        '/api/v1/posts/{postId}/comments',
        { params: { path: { postId: 'p1' }, query: { levels: 6, sort: 'popular' } } },
      ],
      [
        '/api/v1/posts/{postId}/comments',
        { params: { path: { postId: 'p1' }, query: { levels: 6, sort: 'date' } } },
      ],
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

  it('tells a branch that is gone from one that failed (E09-06)', async () => {
    const answering = (status: number) => client(async () => ({ response: { status } }) as { data?: unknown })
    expect(await loadBranch(answering(404), '1', 3)).toEqual({ status: 'not-found' })
    expect(await loadBranch(answering(503), '1', 3)).toEqual({ status: 'error' })
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

describe('comment links (E09-07)', () => {
  const place = (depth: number) => ({
    id: `c${depth}`,
    postId: 'p1',
    depth,
    ancestors: Array.from({ length: depth - 1 }, (_, n) => `c${n + 1}`),
  })

  it('puts the comment on its post page', () => {
    expect(commentPath('/u/anna/p1-privet', 'c1')).toBe('/u/anna/p1-privet#comment-c1')
  })

  it('reads the rest of the branch from the deepest ancestor shown', () => {
    const shown = [comment('c1', null, 1, 6), comment('c2', 'c1', 2, 5), comment('c3', 'c2', 3, 4)]
    expect(nearestShown(shown, place(7))).toBe('c3')
    expect(nearestShown([comment('other', null, 1)], place(7))).toBeNull()
    expect(nearestShown(shown, place(1))).toBeNull()
  })

  it('opens on a phone the branch screen that reaches the comment in one read', () => {
    expect(branchRootOf(place(7), 3)).toBe('c4')
    expect(branchRootOf(place(5), 3)).toBe('c2')
    expect(branchRootOf(place(4), 3)).toBe('c1')
    expect(branchRootOf(place(2), 3)).toBe('c1')
    expect(branchRootOf(place(1), 3)).toBe('c1')
  })

  it('reads the place of a comment, or nothing when it is gone or the read failed', async () => {
    const found = place(3)
    const client = (result: unknown) => ({ GET: async () => result }) as unknown as ApiClient
    expect(await loadCommentPlace(client({ data: found }), 'c3')).toEqual(found)
    expect(await loadCommentPlace(client({ data: undefined, response: { status: 404 } }), 'c3')).toBeNull()
    const failing = {
      GET: async () => {
        throw new Error('offline')
      },
    } as unknown as ApiClient
    expect(await loadCommentPlace(failing, 'c3')).toBeNull()
  })
})
