// A blog's profile for the blog page (E04-02, FR-PROFILE, ADR-0025) as a plain outcome the page understands. The page
// address names the blog by its nickname, or by its UUID while it has none (a new blog's nickname is empty until the
// owner sets it); a short numeric `id123` address does not exist yet (open question of ADR-0025).
import type { ApiClient, components } from './client'
import { locale, t } from '../i18n'

export type Profile = components['schemas']['Profile']

export type ProfileLookup =
  { status: 'found'; profile: Profile; owner: boolean } | { status: 'not-found' } | { status: 'error' }

const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
// The nickname form of ADR-0025 (§7): Latin letters, digits and `_`, 3 to 32, starting with a letter; any case.
const nickname = /^[a-z][a-z0-9_]{2,31}$/i

/** What the address segment names: a blog id, a nickname, or nothing (then the blog cannot exist). */
export function blogKey(segment: string): { id: string } | { nickname: string } | null {
  const value = segment.replace(/^@/, '')
  if (uuid.test(value)) return { id: value.toLowerCase() }
  // `id123` is the form kept for a numeric blog address (ADR-0025, §7), never a nickname.
  if (nickname.test(value) && !/^id\d+$/i.test(value)) return { nickname: value.toLowerCase() }
  return null
}

/** The address of a blog: by its nickname when it has one, otherwise by its id. */
export function blogPath(profile: Pick<Profile, 'id' | 'nickname'>): string {
  return `/u/${profile.nickname ?? profile.id}`
}

/** The blog's name as the page shows it: its title, else its nickname; a new blog has neither yet (ADR-0025, §6). */
export function blogTitle(profile: Pick<Profile, 'name' | 'nickname'>): string {
  return profile.name?.trim() || (profile.nickname ? `@${profile.nickname}` : t('blog.untitled'))
}

/**
 * Looks the blog up. The API shows `registeredAt` to the owner only (ADR-0025, §5), so a date in the answer is what
 * makes the reader the owner; the client must carry the reader's access token for that (server side only, ADR-0018).
 */
export async function lookUpProfile(client: ApiClient, segment: string): Promise<ProfileLookup> {
  const key = blogKey(segment)
  if (!key) return { status: 'not-found' }
  try {
    const { data, response } =
      'id' in key
        ? await client.GET('/api/v1/profiles/{id}', { params: { path: { id: key.id } } })
        : await client.GET('/api/v1/profiles/by-nickname/{nickname}', {
            params: { path: { nickname: key.nickname } },
          })
    if (data) return { status: 'found', profile: data, owner: data.registeredAt !== null }
    if (response.status === 404 || response.status === 400) return { status: 'not-found' }
    return { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/** The person's own blog (GET /profiles/me creates it when it is missing); null without a session or on a failure. */
export async function ownProfile(client: ApiClient): Promise<Profile | null> {
  try {
    const { data } = await client.GET('/api/v1/profiles/me')
    return data ?? null
  } catch {
    return null
  }
}

// Editing the own profile (E04-03, FR-PROFILE, ADR-0034): the name, the description and the avatar. The limits are
// the API's (SubsiteRules): characters as people see them, after the surrounding spaces are cut.
export type ProfileInput = components['schemas']['ProfileInput']

export const nameMaxLength = 25
export const descriptionMaxLength = 160

const graphemes = new Intl.Segmenter(locale, { granularity: 'grapheme' })

/** The length the limits count: grapheme clusters of the trimmed text, so an emoji is one character. */
export function profileTextLength(text: string | null | undefined): number {
  return [...graphemes.segment(text?.trim() ?? '')].length
}

/** Field errors of the input as the API would give them, found before it is sent; empty when it fits. */
export function profileInputErrors(
  input: Pick<ProfileInput, 'name' | 'description'>,
): Record<string, string> {
  const errors: Record<string, string> = {}
  if (profileTextLength(input.name) > nameMaxLength)
    errors.name = t('blog.nameTooLong', { max: nameMaxLength })
  if (profileTextLength(input.description) > descriptionMaxLength)
    errors.description = t('blog.descriptionTooLong', { max: descriptionMaxLength })
  return errors
}

export type ProfileSave =
  | { status: 'saved'; profile: Profile }
  | { status: 'invalid'; errors: Record<string, string> }
  | { status: 'unauthorized' }
  | { status: 'error' }

/** The first message of each field of a validation problem (ADR-0006: `errors` keyed by field). */
function fieldErrors(problem: unknown): Record<string, string> {
  const errors = (problem as { errors?: Record<string, string[]> } | undefined)?.errors ?? {}
  return Object.fromEntries(
    Object.entries(errors)
      .filter(([, messages]) => messages.length > 0)
      .map(([field, messages]) => [field, messages[0]!]),
  )
}

/** Saves the own profile: PUT /profiles/me replaces the name, the description and the avatar. */
export async function saveOwnProfile(client: ApiClient, input: ProfileInput): Promise<ProfileSave> {
  try {
    const { data, error, response } = await client.PUT('/api/v1/profiles/me', { body: input })
    if (data) return { status: 'saved', profile: data }
    if (response.status === 400) return { status: 'invalid', errors: fieldErrors(error) }
    if (response.status === 401) return { status: 'unauthorized' }
    return { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

// The nickname (E04-05, ADR-0090): free among blogs and topics, not a reserved address, changed once in 30 days. The
// API checks all of it; the form only checks the shape as the person types.
export type NicknameSave = ProfileSave | { status: 'too-soon'; message: string }

/** Field error of a nickname before it is sent, as the API would give it; null when the shape fits. */
export function nicknameInputError(value: string): string | null {
  const nick = value.trim().replace(/^@/, '')
  if (!nick) return t('settings.nicknameRequired')
  const key = blogKey(nick)
  return key && 'nickname' in key ? null : t('settings.nicknameForm')
}

/** Changes the own nickname (PUT /profiles/me/nickname); a change sooner than 30 days after the last is `too-soon`. */
export async function saveOwnNickname(client: ApiClient, nickname: string): Promise<NicknameSave> {
  try {
    const { data, error, response } = await client.PUT('/api/v1/profiles/me/nickname', { body: { nickname } })
    if (data) return { status: 'saved', profile: data }
    if (response.status === 400) return { status: 'invalid', errors: fieldErrors(error) }
    if (response.status === 401) return { status: 'unauthorized' }
    if (response.status === 409)
      return { status: 'too-soon', message: (error as { detail?: string } | undefined)?.detail ?? '' }
    return { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

// The cover (E04-04, ADR-0079): an uploaded image and the vertical position, in percent, of the band the header shows.
export type ProfileCover = components['schemas']['ProfileCover']
export type CoverInput = components['schemas']['CoverInput']

/** Sets the cover or moves it (PUT /profiles/me/cover); the answer is the whole profile, as for the other edits. */
export async function saveOwnCover(client: ApiClient, input: CoverInput): Promise<ProfileSave> {
  try {
    const { data, error, response } = await client.PUT('/api/v1/profiles/me/cover', { body: input })
    if (data) return { status: 'saved', profile: data }
    if (response.status === 400) return { status: 'invalid', errors: fieldErrors(error) }
    if (response.status === 401) return { status: 'unauthorized' }
    return { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/** Removes the cover (DELETE /profiles/me/cover). */
export async function removeOwnCover(client: ApiClient): Promise<ProfileSave> {
  try {
    const { data, response } = await client.DELETE('/api/v1/profiles/me/cover')
    if (data) return { status: 'saved', profile: data }
    if (response.status === 401) return { status: 'unauthorized' }
    return { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}
