import { createHash } from 'node:crypto'
import { describe, expect, it } from 'vitest'
import { fakeCaptchaSolution, needsCaptcha, solveCaptcha, type CaptchaChallenge } from '../../api/captcha'

const sha256 = (text: string) => createHash('sha256').update(text).digest('hex')

function challenge(number: number, maxNumber = 2000): CaptchaChallenge {
  const salt = 'abc123?expires=1900000000'
  return {
    provider: 'proof-of-work',
    algorithm: 'SHA-256',
    challenge: sha256(salt + number),
    salt,
    signature: 'signed',
    maxNumber,
  }
}

describe('solveCaptcha', () => {
  it('finds the number of a proof of work and answers in the ALTCHA format', async () => {
    const solution = await solveCaptcha(challenge(1234))
    expect(JSON.parse(atob(solution!))).toEqual({
      algorithm: 'SHA-256',
      challenge: sha256('abc123?expires=1900000000' + 1234),
      number: 1234,
      salt: 'abc123?expires=1900000000',
      signature: 'signed',
    })
  })

  it('gives up on a challenge without a solution or of an unknown provider, and solves the fake', async () => {
    expect(await solveCaptcha(challenge(3000, 1000))).toBeUndefined()
    expect(await solveCaptcha({ ...challenge(1), provider: 'other' })).toBeUndefined()
    expect(await solveCaptcha({ ...challenge(1), algorithm: 'MD5' })).toBeUndefined()
    expect(
      await solveCaptcha({
        provider: 'fake',
        algorithm: null,
        challenge: null,
        salt: null,
        signature: null,
        maxNumber: null,
      }),
    ).toBe(fakeCaptchaSolution)
  })

  it('recognises the captcha_required answer', () => {
    expect(needsCaptcha({ code: 'captcha_required' })).toBe(true)
    expect(needsCaptcha({ code: 'invalid_credentials' })).toBe(false)
    expect(needsCaptcha(undefined)).toBe(false)
  })
})
