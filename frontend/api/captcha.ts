// The captcha of the sign-in (E03-12, ADR-0038): after a series of failed attempts the API answers
// `captcha_required`, the form fetches a challenge from GET /api/v1/sign-in/captcha and sends back its solution.
// `proof-of-work` is the platform's own provider (the ALTCHA format): find the number whose SHA-256 after the salt is
// the challenge. `fake` exists only in Development and Test and is solved by a fixed string.
import type { components } from './schema'

export type CaptchaChallenge = components['schemas']['CaptchaChallenge']

/** The solution the fake provider accepts (the API refuses that provider outside Development and Test). */
export const fakeCaptchaSolution = 'fake-captcha-solved'

/** The problem code of an answer that needs a solved captcha. */
export const captchaRequiredCode = 'captcha_required'

async function sha256Hex(text: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text))
  return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, '0')).join('')
}

/**
 * The solution of `challenge` as the API expects it, or `undefined` for a challenge it cannot solve. Hashes go in
 * batches, so the browser stays responsive while it tries up to `maxNumber` numbers.
 */
export async function solveCaptcha(
  challenge: CaptchaChallenge,
  hash: (text: string) => Promise<string> = sha256Hex,
): Promise<string | undefined> {
  if (challenge.provider === 'fake') return fakeCaptchaSolution
  const { algorithm, salt, signature, maxNumber } = challenge
  if (
    challenge.provider !== 'proof-of-work' ||
    algorithm !== 'SHA-256' ||
    !salt ||
    !signature ||
    maxNumber == null
  )
    return undefined
  const batch = 500
  for (let start = 0; start <= maxNumber; start += batch) {
    const numbers = Array.from({ length: Math.min(batch, maxNumber - start + 1) }, (_, i) => start + i)
    const hashes = await Promise.all(numbers.map((n) => hash(salt + n)))
    const found = hashes.indexOf(challenge.challenge ?? '')
    if (found >= 0) {
      const payload = { algorithm, challenge: challenge.challenge, number: numbers[found], salt, signature }
      return btoa(JSON.stringify(payload))
    }
  }
  return undefined
}

/** Whether a problem body is the `captcha_required` answer. */
export function needsCaptcha(body: unknown): boolean {
  return (body as { code?: unknown } | undefined)?.code === captchaRequiredCode
}
