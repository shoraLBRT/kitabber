import { describe, expect, it, vi } from 'vitest'
import { createApiClient } from '../../api/client'
import { uploadMedia } from '../../api/media'
import {
  blogKey,
  blogPath,
  blogTitle,
  lookUpProfile,
  ownProfile,
  profileInputErrors,
  profileTextLength,
  saveOwnProfile,
} from '../../api/profiles'

// The blog page's profile calls (E04-02, ADR-0025): what an address names, the outcome of each API answer, and the
// owner known by `registeredAt`, which the API shows to the owner only.
const id = '0195f3a0-0000-7000-8000-0000000000b1'
const profile = {
  id,
  type: 'personal',
  name: 'Анна',
  nickname: 'anna',
  description: null,
  registeredAt: null,
  avatarId: null,
  avatarUrl: null,
  avatarSources: [],
}

function clientAnswering(status: number, body: unknown) {
  const fetch = vi.fn(async (request: Request) => {
    void request
    return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })
  })
  return { client: createApiClient({ baseUrl: 'http://api.test', fetch }), fetch }
}

describe('blog addresses', () => {
  it('reads an id or a nickname from the address, and nothing else', () => {
    expect(blogKey(id.toUpperCase())).toEqual({ id })
    expect(blogKey('Anna')).toEqual({ nickname: 'anna' })
    expect(blogKey('@anna_1')).toEqual({ nickname: 'anna_1' })
    for (const segment of ['id123', '123', 'an', '_anna', 'анна', 'a'.repeat(33), ''])
      expect(blogKey(segment), segment).toBeNull()
  })

  it('puts a blog at its nickname, or at its id without one', () => {
    expect(blogPath(profile)).toBe('/u/anna')
    expect(blogPath({ ...profile, nickname: null })).toBe(`/u/${id}`)
  })

  it('names a blog by its title, else its nickname', () => {
    expect(blogTitle(profile)).toBe('Анна')
    expect(blogTitle({ name: '  ', nickname: 'anna' })).toBe('@anna')
    expect(blogTitle({ name: null, nickname: null })).toBe('Блог без названия')
  })
})

describe('lookUpProfile', () => {
  it('asks by nickname or by id', async () => {
    const byNickname = clientAnswering(200, profile)
    await lookUpProfile(byNickname.client, '@Anna')
    expect(byNickname.fetch.mock.calls[0]![0].url).toBe('http://api.test/api/v1/profiles/by-nickname/anna')
    const byId = clientAnswering(200, profile)
    await lookUpProfile(byId.client, id)
    expect(byId.fetch.mock.calls[0]![0].url).toBe(`http://api.test/api/v1/profiles/${id}`)
  })

  it('knows the owner by the registration date in the answer', async () => {
    expect(await lookUpProfile(clientAnswering(200, profile).client, 'anna')).toEqual({
      status: 'found',
      profile,
      owner: false,
    })
    const own = { ...profile, registeredAt: '2026-03-14T10:00:00Z' }
    expect(await lookUpProfile(clientAnswering(200, own).client, 'anna')).toMatchObject({ owner: true })
  })

  it('turns 404 and 400 into «not found» without guessing, other failures into an error', async () => {
    const notFound = { status: 404, code: 'profile_not_found' }
    expect(await lookUpProfile(clientAnswering(404, notFound).client, 'nobody')).toEqual({
      status: 'not-found',
    })
    expect(await lookUpProfile(clientAnswering(400, {}).client, 'nobody')).toEqual({ status: 'not-found' })
    expect(await lookUpProfile(clientAnswering(500, {}).client, 'anna')).toEqual({ status: 'error' })

    const unreachable = createApiClient({
      baseUrl: 'http://api.test',
      fetch: async () => {
        throw new TypeError('fetch failed')
      },
    })
    expect(await lookUpProfile(unreachable, 'anna')).toEqual({ status: 'error' })

    const untouched = clientAnswering(200, profile)
    expect(await lookUpProfile(untouched.client, 'id123')).toEqual({ status: 'not-found' })
    expect(untouched.fetch).not.toHaveBeenCalled()
  })

  it('finds the own blog, or nothing without a session', async () => {
    expect(await ownProfile(clientAnswering(200, profile).client)).toEqual(profile)
    expect(await ownProfile(clientAnswering(401, {}).client)).toBeNull()
  })
})

describe('editing the own profile (E04-03)', () => {
  it('counts characters as people see them, without the surrounding spaces', () => {
    expect(profileTextLength('  Анна  ')).toBe(4)
    expect(profileTextLength('📚'.repeat(25))).toBe(25)
    expect(profileTextLength('й')).toBe(1)
    expect(profileTextLength(null)).toBe(0)
  })

  it('checks the limits the API checks: 25 for the name, 160 for the description', () => {
    expect(profileInputErrors({ name: '📚'.repeat(25), description: 'я'.repeat(160) })).toEqual({})
    const errors = profileInputErrors({ name: 'a'.repeat(26), description: 'я'.repeat(161) })
    expect(errors.name).toContain('25')
    expect(errors.description).toContain('160')
  })

  it('saves through PUT /profiles/me and turns the answers into outcomes', async () => {
    const saved = clientAnswering(200, { ...profile, name: 'Анна К.' })
    expect(
      await saveOwnProfile(saved.client, { name: 'Анна К.', description: null, avatarId: null }),
    ).toEqual({
      status: 'saved',
      profile: { ...profile, name: 'Анна К.' },
    })
    const request = saved.fetch.mock.calls[0]![0]
    expect(request.method).toBe('PUT')
    expect(request.url).toBe('http://api.test/api/v1/profiles/me')
    expect(await request.json()).toEqual({ name: 'Анна К.', description: null, avatarId: null })

    const problem = {
      status: 400,
      code: 'validation_failed',
      errors: { name: ['Слишком длинно.'], avatarId: [] },
    }
    expect(await saveOwnProfile(clientAnswering(400, problem).client, {})).toEqual({
      status: 'invalid',
      errors: { name: 'Слишком длинно.' },
    })
    expect(await saveOwnProfile(clientAnswering(401, {}).client, {})).toEqual({ status: 'unauthorized' })
    expect(await saveOwnProfile(clientAnswering(500, {}).client, {})).toEqual({ status: 'error' })
  })

  it('uploads the avatar as multipart/form-data and names the refusals', async () => {
    const media = {
      id: 'm1',
      kind: 'image',
      contentType: 'image/jpeg',
      size: 3,
      fileName: 'avatar.jpg',
      createdAt: '',
    }
    const uploaded = clientAnswering(201, media)
    expect(
      await uploadMedia(uploaded.client, new Blob(['abc'], { type: 'image/jpeg' }), 'avatar.jpg'),
    ).toEqual({
      status: 'uploaded',
      media,
    })
    const request = uploaded.fetch.mock.calls[0]![0]
    expect(request.url).toBe('http://api.test/api/v1/media')
    expect(request.headers.get('content-type')).toMatch(/^multipart\/form-data; boundary=/)
    expect(await request.text()).toContain('name="file"; filename="avatar.jpg"')

    const blob = new Blob(['x'])
    expect(await uploadMedia(clientAnswering(415, {}).client, blob, 'a.jpg')).toEqual({
      status: 'unsupported',
    })
    expect(await uploadMedia(clientAnswering(413, {}).client, blob, 'a.jpg')).toEqual({ status: 'too-large' })
    expect(await uploadMedia(clientAnswering(401, {}).client, blob, 'a.jpg')).toEqual({
      status: 'unauthorized',
    })
    expect(await uploadMedia(clientAnswering(500, {}).client, blob, 'a.jpg')).toEqual({ status: 'error' })
  })
})
