import { describe, expect, it, vi } from 'vitest'
import { createApiClient } from '../../api/client'
import { blogKey, blogPath, blogTitle, lookUpProfile, ownProfile } from '../../api/profiles'

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
