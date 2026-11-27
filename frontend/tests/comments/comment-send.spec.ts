import { afterEach, describe, expect, it } from 'vitest'
import {
  canReply,
  nestComments,
  replaceComment,
  sendComment,
  withComment,
  withoutComment,
} from '../../api/comments'
import type { ApiClient } from '../../api/client'
import {
  commentDraftKey,
  forgetCommentDraft,
  readCommentDraft,
  saveCommentDraft,
} from '../../utils/comment-draft'
import { comment } from './factory'

// Sending a comment (E09-03, ADR-0056) without a page: where a new comment goes in the ordered tree and how the counts
// of the comments above it follow, undoing it, the server's comment in its place, the answers of the send, and the
// local draft.

const tree = [
  comment('1', null, 1, 2),
  comment('1.1', '1', 2, 1),
  comment('1.1.1', '1.1', 3),
  comment('2', null, 1),
]

describe('withComment', () => {
  it('puts a comment under the post at the end', () => {
    const added = withComment(tree, comment('3', null, 1))
    expect(added.map((c) => c.id)).toEqual(['1', '1.1', '1.1.1', '2', '3'])
  })

  it('puts a reply after the replies of its parent and counts it above', () => {
    const added = withComment(tree, comment('1.2', '1', 2))
    expect(added.map((c) => c.id)).toEqual(['1', '1.1', '1.1.1', '1.2', '2'])
    expect(added.map((c) => c.replies)).toEqual([3, 1, 0, 0, 0])

    const deep = withComment(tree, comment('1.1.2', '1.1', 3))
    expect(deep.map((c) => c.id)).toEqual(['1', '1.1', '1.1.1', '1.1.2', '2'])
    expect(deep.map((c) => c.replies)).toEqual([3, 2, 0, 0, 0])
  })

  it('keeps the replies a read left out where they were', () => {
    // Comment 1 has five replies, two read: three wait behind «Показать ещё», before and after the reply sent.
    const partial = [comment('1', null, 1, 5), comment('1.1', '1', 2, 1), comment('1.1.1', '1.1', 3)]
    const before = nestComments(partial)[0]!.hidden
    const after = nestComments(withComment(partial, comment('1.2', '1', 2)))[0]!
    expect(after.hidden).toBe(before)
    expect(after.replies.map((node) => node.comment.id)).toEqual(['1.1', '1.2'])
  })

  it('changes nothing for a reply to a comment not shown', () => {
    expect(withComment(tree, comment('x.1', 'x', 2))).toEqual(tree)
  })
})

describe('withoutComment', () => {
  it('takes a sent comment out and the counts above back', () => {
    const added = withComment(tree, comment('1.1.2', '1.1', 3))
    expect(withoutComment(added, '1.1.2')).toEqual(tree)
    expect(withoutComment(withComment(tree, comment('3', null, 1)), '3')).toEqual(tree)
    expect(withoutComment(tree, 'none')).toEqual(tree)
  })
})

describe('replaceComment', () => {
  it('puts the server comment in the place of the one sent', () => {
    const sent = withComment(tree, comment('local-1', '1', 2))
    const kept = replaceComment(sent, 'local-1', comment('srv', '1', 2))
    expect(kept.map((c) => c.id)).toEqual(['1', '1.1', '1.1.1', 'srv', '2'])
  })
})

describe('canReply', () => {
  it('refuses a guest comment and the deepest level', () => {
    expect(canReply(comment('1', null, 1), 3)).toBe(true)
    expect(canReply(comment('1', null, 3), 3)).toBe(false)
    expect(canReply({ ...comment('g', null, 1), kind: 'guest' }, 3)).toBe(false)
  })
})

describe('sendComment', () => {
  function client(answer: () => Promise<{ data?: unknown; error?: unknown; response: { status: number } }>) {
    const calls: unknown[] = []
    const api = {
      POST: async (path: string, init: unknown) => {
        calls.push([path, init])
        return answer()
      },
    } as unknown as ApiClient
    return { api, calls }
  }

  it('posts the text and the parent and gives the kept comment', async () => {
    const kept = comment('srv', '1', 2)
    const { api, calls } = client(async () => ({ data: kept, response: { status: 201 } }))
    expect(await sendComment(api, 'p1', 'Ответ', '1')).toEqual({ status: 'sent', comment: kept })
    expect(calls).toEqual([
      [
        '/api/v1/posts/{postId}/comments',
        { params: { path: { postId: 'p1' } }, body: { text: 'Ответ', parentId: '1' } },
      ],
    ])
  })

  it('gives the reason of a refusal, a lost sign-in and a failure', async () => {
    const refused = client(async () => ({
      error: { errors: { parentId: ['Ветка не глубже 3 уровней: ответьте выше по ветке.'] } },
      response: { status: 400 },
    }))
    expect(await sendComment(refused.api, 'p1', 'Ответ', '1')).toEqual({
      status: 'refused',
      message: 'Ветка не глубже 3 уровней: ответьте выше по ветке.',
    })
    const signedOut = client(async () => ({ response: { status: 401 } }))
    expect(await sendComment(signedOut.api, 'p1', 'Текст', null)).toEqual({ status: 'unauthorized' })
    const failed = client(async () => ({ response: { status: 503 } }))
    expect(await sendComment(failed.api, 'p1', 'Текст', null)).toEqual({ status: 'error' })
    const thrown = client(async () => {
      throw new Error('offline')
    })
    expect(await sendComment(thrown.api, 'p1', 'Текст', null)).toEqual({ status: 'error' })
  })

  // E11-06 (ADR-0091): the author blocked the person; another 403 stays a plain failure.
  it('tells a black list apart from other refusals', async () => {
    const blocked = client(async () => ({
      error: { code: 'comments.blocked', detail: 'Автор заблокировал вас: отвечать ему нельзя.' },
      response: { status: 403 },
    }))
    expect(await sendComment(blocked.api, 'p1', 'Ответ', '1')).toEqual({ status: 'blocked' })
    const forbidden = client(async () => ({ error: { code: 'forbidden' }, response: { status: 403 } }))
    expect(await sendComment(forbidden.api, 'p1', 'Ответ', '1')).toEqual({ status: 'error' })
  })
})

describe('comment drafts', () => {
  afterEach(() => localStorage.clear())

  it('keeps one draft per post and per comment replied to', () => {
    saveCommentDraft(localStorage, commentDraftKey('p1', null), 'Под постом')
    saveCommentDraft(localStorage, commentDraftKey('p1', 'c1'), 'Ответ')
    expect(readCommentDraft(localStorage, commentDraftKey('p1', null))).toBe('Под постом')
    expect(readCommentDraft(localStorage, commentDraftKey('p1', 'c1'))).toBe('Ответ')
    expect(readCommentDraft(localStorage, commentDraftKey('p2', null))).toBe('')
  })

  it('forgets a sent draft but not a new text written since', () => {
    const key = commentDraftKey('p1', null)
    saveCommentDraft(localStorage, key, 'Новый текст')
    forgetCommentDraft(localStorage, key, 'Старый текст')
    expect(readCommentDraft(localStorage, key)).toBe('Новый текст')
    forgetCommentDraft(localStorage, key, 'Новый текст ')
    expect(readCommentDraft(localStorage, key)).toBe('')
  })

  it('works without storage', () => {
    const blocked = {
      getItem: () => {
        throw new Error('blocked')
      },
      setItem: () => {
        throw new Error('blocked')
      },
      removeItem: () => {
        throw new Error('blocked')
      },
    } as unknown as Storage
    expect(() => saveCommentDraft(blocked, 'k', 'text')).not.toThrow()
    expect(readCommentDraft(blocked, 'k')).toBe('')
    expect(() => forgetCommentDraft(blocked, 'k', 'text')).not.toThrow()
    expect(readCommentDraft(undefined, 'k')).toBe('')
  })
})
