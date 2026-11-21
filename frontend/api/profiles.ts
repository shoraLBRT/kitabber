// A blog's profile for the blog page (E04-02, FR-PROFILE, ADR-0025) as a plain outcome the page understands. The page
// address names the blog by its nickname, or by its UUID while it has none (a new blog's nickname is empty until the
// owner sets it); a short numeric `id123` address does not exist yet (open question of ADR-0025).
import type { ApiClient, components } from './client'

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
  return profile.name?.trim() || (profile.nickname ? `@${profile.nickname}` : 'Блог без названия')
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
