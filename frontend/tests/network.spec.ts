import { describe, expect, it, vi } from 'vitest'
import { failedKeys, watchNetwork } from '../utils/network'
import { responseStatus } from '../utils/response-status'

// «Нет интернета» and the status of a server render (E02-07, ADR-0057). The bar and the reading again against a real
// browser are in e2e/states.spec.ts.
describe('failedKeys', () => {
  it('finds only the data whose answer is a failure', () => {
    expect(
      failedKeys({
        'post:abc': { status: 'error' },
        'feed:fresh': { status: 'found', page: { items: [] } },
        'blog:anna': { status: 'not-found' },
        'comments:abc': null,
        count: 3,
        list: [{ status: 'error' }],
      }),
    ).toEqual(['post:abc'])
  })

  it('finds nothing when everything loaded', () => {
    expect(failedKeys({})).toEqual([])
  })
})

describe('watchNetwork', () => {
  it('tells the online and offline events and stops on demand', () => {
    const target = new EventTarget()
    const onChange = vi.fn()
    const stop = watchNetwork(target, onChange)

    target.dispatchEvent(new Event('offline'))
    target.dispatchEvent(new Event('online'))
    expect(onChange.mock.calls).toEqual([[false], [true]])

    stop()
    target.dispatchEvent(new Event('offline'))
    expect(onChange).toHaveBeenCalledTimes(2)
  })
})

describe('responseStatus', () => {
  it('answers 404 for missing content, 500 for a failure and nothing for the content', () => {
    expect(responseStatus({ status: 'not-found' })).toBe(404)
    expect(responseStatus({ status: 'error' })).toBe(500)
    expect(responseStatus({ status: 'found' })).toBeUndefined()
    expect(responseStatus(null)).toBeUndefined()
    expect(responseStatus(undefined)).toBeUndefined()
  })
})
