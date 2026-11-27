import { describe, expect, it } from 'vitest'
import {
  canEdit,
  canReply,
  deleteComment,
  editComment,
  loadOwnComments,
  withAuthorBlocked,
  withDeleted,
  withEdited,
} from '../../api/comments'
import type { ApiClient } from '../../api/client'
import { commentMenuItems } from '../../utils/comment-menu'
import { comment } from './factory'

// Changing and deleting an own comment (E09-05, ADR-0059) without a page: until when the text may change, the items of
// the menu, the tree after a change and a deletion (a stub over replies, nothing over none), and the answers of the
// API. The comment's look is in tests/comments/CommentEdit.spec.ts, the page in e2e/comment-edit.spec.ts.

const created = Date.parse('2026-11-20T12:00:00Z')
const inTime = created + 5 * 60_000
const late = created + 15 * 60_000

describe('canEdit', () => {
  it('lets the text change before the end of the time the server gave', () => {
    expect(canEdit(comment('1', null, 1), inTime)).toBe(true)
    expect(canEdit(comment('1', null, 1), late)).toBe(false)
  })

  it('never lets a guest comment or a deleted one change', () => {
    expect(canEdit({ ...comment('1', null, 1), editableUntil: null }, inTime)).toBe(false)
    expect(canEdit({ ...comment('1', null, 1), deleted: true }, inTime)).toBe(false)
  })
})

describe('commentMenuItems', () => {
  it('gives the author «Редактировать» while the time lasts and «Удалить» always', () => {
    expect(commentMenuItems(comment('1', null, 1), true, inTime).map((item) => item.label)).toEqual([
      'Редактировать',
      'Удалить',
    ])
    expect(commentMenuItems(comment('1', null, 1), true, late).map((item) => item.label)).toEqual(['Удалить'])
  })

  it('has nothing for someone else’s comment or a deleted one', () => {
    expect(commentMenuItems(comment('1', null, 1), false, inTime)).toEqual([])
    expect(commentMenuItems({ ...comment('1', null, 1), deleted: true }, true, inTime)).toEqual([])
  })

  // E11-06 (ADR-0091): a reader who may block gets «Заблокировать автора» on someone else's native comment only.
  it('offers a reader who may block «Заблокировать автора» on someone else’s comment', () => {
    const ids = (items: ReturnType<typeof commentMenuItems>) => items.map((item) => item.id)
    expect(commentMenuItems(comment('1', null, 1), false, inTime, true).map((item) => item.label)).toEqual([
      'Заблокировать автора',
    ])
    expect(ids(commentMenuItems(comment('1', null, 1), true, inTime, true))).toEqual(['edit', 'delete'])
    expect(commentMenuItems({ ...comment('1', null, 1), authorBlocked: true }, false, inTime, true)).toEqual(
      [],
    )
    expect(commentMenuItems({ ...comment('1', null, 1), deleted: true }, false, inTime, true)).toEqual([])
    const guest = { ...comment('1', null, 1), kind: 'guest' as const, authorAccountId: null }
    expect(commentMenuItems(guest, false, inTime, true)).toEqual([])
  })
})

describe('withAuthorBlocked', () => {
  it('folds every comment of the blocked author and leaves the others', () => {
    const tree = [
      comment('1', null, 1, 1, 'a1'),
      comment('1.1', '1', 2, 0, 'a2'),
      comment('2', null, 1, 0, 'A1'),
    ]
    const after = withAuthorBlocked(tree, 'a1')
    expect(after.map((c) => c.authorBlocked)).toEqual([true, false, true])
  })
})

describe('withEdited', () => {
  it('puts the new text in place and keeps the replies read', () => {
    const tree = [comment('1', null, 1, 1), comment('1.1', '1', 2)]
    const kept = { ...comment('1', null, 1), text: 'Стало', editedAt: '2026-11-20T12:05:00Z', replies: 0 }
    const edited = withEdited(tree, kept)
    expect(edited[0]).toMatchObject({ text: 'Стало', editedAt: '2026-11-20T12:05:00Z', replies: 1 })
    expect(edited[1]).toEqual(tree[1])
  })
})

describe('withDeleted', () => {
  const tree = [
    comment('1', null, 1, 3),
    comment('1.1', '1', 2, 1),
    comment('1.1.1', '1.1', 3),
    comment('1.2', '1', 2),
    comment('2', null, 1),
  ]

  it('leaves a comment with replies as their stub, with no author and no text', () => {
    const after = withDeleted(tree, '1.1')
    expect(after.map((c) => c.id)).toEqual(['1', '1.1', '1.1.1', '1.2', '2'])
    expect(after[1]).toMatchObject({
      deleted: true,
      text: '',
      authorAccountId: null,
      editableUntil: null,
      replies: 1,
    })
    expect(canReply(after[1]!, 8)).toBe(false)
  })

  it('takes a comment without replies out and counts one reply less above it', () => {
    const after = withDeleted(tree, '1.2')
    expect(after.map((c) => c.id)).toEqual(['1', '1.1', '1.1.1', '2'])
    expect(after[0]!.replies).toBe(2)
  })

  it('takes out the stubs left with no replies above the comment deleted', () => {
    const stubbed = withDeleted(withDeleted(tree, '1'), '1.1')
    const after = withDeleted(withDeleted(stubbed, '1.1.1'), '1.2')
    expect(after.map((c) => c.id)).toEqual(['2'])
    // The root still holds the place of «1.2» while it is there.
    expect(withDeleted(stubbed, '1.1.1').map((c) => c.id)).toEqual(['1', '1.2', '2'])
  })

  it('changes nothing for a comment not shown', () => {
    expect(withDeleted(tree, 'x')).toEqual(tree)
  })
})

describe('the API of the own comments', () => {
  function client(answer: () => Promise<{ data?: unknown; error?: unknown; response: { status: number } }>) {
    const calls: unknown[] = []
    const record = (method: string) => async (path: string, init: unknown) => {
      calls.push([method, path, init])
      return answer()
    }
    const api = {
      GET: record('GET'),
      PATCH: record('PATCH'),
      DELETE: record('DELETE'),
    } as unknown as ApiClient
    return { api, calls }
  }

  it('sends the new text and gives the kept comment', async () => {
    const kept = { ...comment('1', null, 1), text: 'Стало' }
    const { api, calls } = client(async () => ({ data: kept, response: { status: 200 } }))
    expect(await editComment(api, '1', 'Стало')).toEqual({ status: 'edited', comment: kept })
    expect(calls).toEqual([
      ['PATCH', '/api/v1/comments/{id}', { params: { path: { id: '1' } }, body: { text: 'Стало' } }],
    ])
  })

  it('gives the reason of a refusal', async () => {
    const empty = client(async () => ({
      error: { errors: { text: ['Напишите текст комментария.'] } },
      response: { status: 400 },
    }))
    expect(await editComment(empty.api, '1', ' ')).toEqual({
      status: 'refused',
      message: 'Напишите текст комментария.',
    })
    const closed = client(async () => ({
      error: { code: 'comments.edit_closed' },
      response: { status: 403 },
    }))
    expect(await editComment(closed.api, '1', 'Поздно')).toEqual({
      status: 'refused',
      message: 'Время на правку вышло: комментарий можно только удалить.',
    })
    const gone = client(async () => ({ response: { status: 404 } }))
    expect(await editComment(gone.api, '1', 'Текст')).toEqual({
      status: 'refused',
      message: 'Комментарий уже удалён.',
    })
  })

  it('tells a sign-out and a failure apart', async () => {
    expect(await editComment(client(async () => ({ response: { status: 401 } })).api, '1', 'Т')).toEqual({
      status: 'unauthorized',
    })
    expect(await editComment(client(async () => ({ response: { status: 500 } })).api, '1', 'Т')).toEqual({
      status: 'error',
    })
    const thrown = client(async () => {
      throw new Error('offline')
    })
    expect(await editComment(thrown.api, '1', 'Т')).toEqual({ status: 'error' })
    expect(await deleteComment(thrown.api, '1')).toBe(0)
    expect(await loadOwnComments(thrown.api, 'p1')).toEqual([])
  })

  it('deletes and reads the own comments', async () => {
    const deleted = client(async () => ({ response: { status: 204 } }))
    expect(await deleteComment(deleted.api, '1')).toBe(204)
    expect(deleted.calls).toEqual([['DELETE', '/api/v1/comments/{id}', { params: { path: { id: '1' } } }]])
    const mine = client(async () => ({ data: { postId: 'p1', ids: ['1', '2'] }, response: { status: 200 } }))
    expect(await loadOwnComments(mine.api, 'p1')).toEqual(['1', '2'])
  })
})
