// The onboarding after sign-up (E03-10, FR-AUTH, ADR-0102): the name, the avatar and the topics, each step skippable,
// saved at once by POST /api/v1/profiles/me/onboarding. The own profile's `onboardingPending` says whether the person
// still has it ahead; after the answer it is never offered again.
import type { ApiClient, components } from './client'
import { nameMaxLength, profileTextLength, type ProfileSave } from './profiles'
import { t } from '../i18n'

export type OnboardingInput = components['schemas']['OnboardingInput']

/** The shortest name the onboarding takes (SubsiteRules.OnboardingNameMinLength); the longest is the blog name's. */
export const onboardingNameMinLength = 2

/** The error of the name step as the API would give it; null when it fits or is empty (the step is skipped). */
export function onboardingNameError(name: string): string | null {
  const length = profileTextLength(name)
  if (length === 0) return null
  return length < onboardingNameMinLength || length > nameMaxLength
    ? t('onboarding.nameLength', { min: onboardingNameMinLength, max: nameMaxLength })
    : null
}

/** What the onboarding's answer means for the page: `done` — it was finished already, on another device or tab. */
export type OnboardingFinish = ProfileSave | { status: 'done' }

/** The first message of each field of a validation problem (ADR-0006: `errors` keyed by field). */
function fieldErrors(problem: unknown): Record<string, string> {
  const errors = (problem as { errors?: Record<string, string[]> } | undefined)?.errors ?? {}
  return Object.fromEntries(
    Object.entries(errors)
      .filter(([, messages]) => messages.length > 0)
      .map(([field, messages]) => [field, messages[0]!]),
  )
}

/** Saves the choices and marks the onboarding done; an empty input only marks it done. */
export async function finishOnboarding(client: ApiClient, input: OnboardingInput): Promise<OnboardingFinish> {
  try {
    const { data, error, response } = await client.POST('/api/v1/profiles/me/onboarding', { body: input })
    if (data) return { status: 'saved', profile: data }
    if (response.status === 400) return { status: 'invalid', errors: fieldErrors(error) }
    if (response.status === 401) return { status: 'unauthorized' }
    if (response.status === 409) return { status: 'done' }
    return { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/** Where the onboarding leads after sign-in: the page asked for, kept as a local path only. */
export function onboardingPath(returnTo: string): string {
  return `/onboarding?returnTo=${encodeURIComponent(safeReturn(returnTo))}`
}

/** A local path to go on to; anything else (another site, `//host`) is the home page. */
export function safeReturn(value: unknown): string {
  return typeof value === 'string' &&
    value.startsWith('/') &&
    !value.startsWith('//') &&
    !value.startsWith('/\\')
    ? value
    : '/'
}
