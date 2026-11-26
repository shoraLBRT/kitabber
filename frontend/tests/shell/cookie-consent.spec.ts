// E02-13 (#66, ADR-0066): the cookie choice — its cookie value, and the gate that starts optional code only after
// consent and removes its cookies on a withdrawal.
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  hasCookieConsent,
  loadCookieConsent,
  resetCookieConsentForTests,
  setCookieConsent,
  useCookieConsent,
  whenConsented,
} from '../../composables/useCookieConsent'
import {
  acceptAll,
  consentCookie,
  necessaryOnly,
  parseConsent,
  readCookie,
  serializeConsent,
} from '../../utils/cookie-consent'

function clearCookies() {
  for (const pair of document.cookie.split(';')) {
    const name = pair.split('=')[0]!.trim()
    if (name) document.cookie = `${name}=; path=/; max-age=0`
  }
}

beforeEach(() => {
  resetCookieConsentForTests()
  clearCookies()
})
afterEach(clearCookies)

describe('cookie consent value', () => {
  it('round-trips a choice', () => {
    expect(serializeConsent(acceptAll)).toBe('v1.analytics-1')
    expect(serializeConsent(necessaryOnly)).toBe('v1.analytics-0')
    expect(parseConsent('v1.analytics-1')).toEqual(acceptAll)
    expect(parseConsent('v1.analytics-0')).toEqual(necessaryOnly)
  })

  it('reads no choice from a missing, foreign or older value, so the banner asks again', () => {
    for (const value of [
      null,
      undefined,
      '',
      'yes',
      'v0.analytics-1',
      'v1.analytics-2',
      'v1.analytics-1.ads-1',
    ])
      expect(parseConsent(value)).toBeNull()
  })

  it('keeps the choice a year for the whole site', () => {
    expect(consentCookie(acceptAll, false)).toBe(
      'cookie_consent=v1.analytics-1; path=/; max-age=31536000; samesite=lax',
    )
    expect(consentCookie(acceptAll, true)).toMatch(/; secure$/)
  })

  it('finds one cookie among others', () => {
    expect(readCookie('a=1; cookie_consent=v1.analytics-0; b=2', 'cookie_consent')).toBe('v1.analytics-0')
    expect(readCookie('xcookie_consent=1', 'cookie_consent')).toBeNull()
  })
})

describe('useCookieConsent', () => {
  it('has no choice until one is made, and allows nothing optional', () => {
    loadCookieConsent()
    const { loaded, decided } = useCookieConsent()
    expect(loaded.value).toBe(true)
    expect(decided.value).toBe(false)
    expect(hasCookieConsent('analytics')).toBe(false)
  })

  it('reads the saved choice', () => {
    document.cookie = 'cookie_consent=v1.analytics-1; path=/'
    loadCookieConsent()
    expect(useCookieConsent().choice.value).toEqual(acceptAll)
    expect(hasCookieConsent('analytics')).toBe(true)
  })

  it('saves a choice in the cookie', () => {
    setCookieConsent(necessaryOnly)
    expect(readCookie(document.cookie, 'cookie_consent')).toBe('v1.analytics-0')
    expect(useCookieConsent().decided.value).toBe(true)
  })

  it('starts optional code only after consent, once', () => {
    const start = vi.fn()
    loadCookieConsent()
    whenConsented('analytics', start)
    expect(start).not.toHaveBeenCalled()
    setCookieConsent(necessaryOnly)
    expect(start).not.toHaveBeenCalled()
    setCookieConsent(acceptAll)
    setCookieConsent(acceptAll)
    expect(start).toHaveBeenCalledTimes(1)
  })

  it('waits for the saved choice to be read before starting', () => {
    document.cookie = 'cookie_consent=v1.analytics-1; path=/'
    const start = vi.fn()
    whenConsented('analytics', start)
    expect(start).not.toHaveBeenCalled()
    loadCookieConsent()
    expect(start).toHaveBeenCalledTimes(1)
  })

  it('removes the cookies of a withdrawn category and starts again on a new consent', () => {
    const start = vi.fn(() => {
      document.cookie = '_stats=1; path=/'
    })
    setCookieConsent(acceptAll)
    whenConsented('analytics', start, ['_stats'])
    expect(readCookie(document.cookie, '_stats')).toBe('1')

    setCookieConsent(necessaryOnly)
    expect(readCookie(document.cookie, '_stats')).toBeNull()
    setCookieConsent(acceptAll)
    expect(start).toHaveBeenCalledTimes(2)
  })

  it('stops waiting when asked', () => {
    const start = vi.fn()
    const stop = whenConsented('analytics', start)
    stop()
    setCookieConsent(acceptAll)
    expect(start).not.toHaveBeenCalled()
  })
})
