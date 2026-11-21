// E03-04 (#30): the action a guest started before the sign-in window runs once, on the same page, after the sign-in.
import { beforeEach, describe, expect, it } from 'vitest'
import {
  pendingActionKey,
  pendingActionLifetimeMs,
  rememberPendingAction,
  takePendingAction,
} from '../../utils/pending-action'

const start = Date.UTC(2026, 10, 21, 12)

beforeEach(() => sessionStorage.clear())

describe('pending action', () => {
  it('runs once on the page it was started on', () => {
    rememberPendingAction(sessionStorage, 'like:post:42', '/ui?tab=1', start)
    expect(takePendingAction(sessionStorage, 'like:post:42', '/ui?tab=1', start + 5_000)).toBe(true)
    expect(takePendingAction(sessionStorage, 'like:post:42', '/ui?tab=1', start + 6_000)).toBe(false)
  })

  it('leaves other actions on the page alone', () => {
    rememberPendingAction(sessionStorage, 'like:post:42', '/ui', start)
    expect(takePendingAction(sessionStorage, 'like:post:7', '/ui', start)).toBe(false)
    expect(takePendingAction(sessionStorage, 'like:post:42', '/ui', start)).toBe(true)
  })

  it('is forgotten on another page', () => {
    rememberPendingAction(sessionStorage, 'like:post:42', '/ui', start)
    expect(takePendingAction(sessionStorage, 'like:post:42', '/', start)).toBe(false)
    expect(sessionStorage.getItem(pendingActionKey)).toBeNull()
  })

  it('is forgotten after the life of the sign-in flow', () => {
    rememberPendingAction(sessionStorage, 'like:post:42', '/ui', start)
    expect(
      takePendingAction(sessionStorage, 'like:post:42', '/ui', start + pendingActionLifetimeMs + 1),
    ).toBe(false)
    rememberPendingAction(sessionStorage, 'like:post:42', '/ui', start)
    expect(takePendingAction(sessionStorage, 'like:post:42', '/ui', start + pendingActionLifetimeMs)).toBe(
      true,
    )
  })

  it('survives a broken record and a missing storage', () => {
    sessionStorage.setItem(pendingActionKey, '{not json')
    expect(takePendingAction(sessionStorage, 'like:post:42', '/ui', start)).toBe(false)
    expect(() => rememberPendingAction(undefined, 'like:post:42', '/ui', start)).not.toThrow()
    expect(takePendingAction(undefined, 'like:post:42', '/ui', start)).toBe(false)
  })
})
