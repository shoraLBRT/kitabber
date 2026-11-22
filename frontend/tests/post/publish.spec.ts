import { describe, expect, it, vi } from 'vitest'
import { createApiClient } from '../../api/client'
import { publishPost, type PublishInput } from '../../api/publish'
import { listTopics } from '../../api/topics'

// Publishing (E05-09, ADR-0046): the draft is saved, then published, and the outcome carries the post's canonical
// address; a draft kept by a failed attempt is reused; every refusal of the API has its own outcome.
const postId = '0195f3a0-0000-7000-8000-0000000000c9'
const blogId = '0195f3a0-0000-7000-8000-0000000000b4'
const post = { id: postId, blogId, slug: 'knigi-oseni', title: 'Книги осени', status: 'published' }
const topic = { id: blogId, type: 'topic', name: 'Книги', nickname: 'books' }
const input: PublishInput = {
  draftId: null,
  title: 'Книги осени',
  blogId,
  body: { version: 1, blocks: [{ id: 'b1', type: 'paragraph', data: { text: 'Текст' } }] },
}

type Answer = [status: number, body: unknown]

/** A client whose API answers `answers` in turn; `fetch.mock.calls` keeps the requests. */
function clientAnswering(...answers: Answer[]) {
  const fetch = vi.fn(async (request: Request) => {
    void request
    const [status, body] = answers.shift() ?? [500, {}]
    return new Response(JSON.stringify(body), { status, headers: { 'content-type': 'application/json' } })
  })
  return { client: createApiClient({ baseUrl: 'http://api.test', fetch }), fetch }
}

const requests = (fetch: ReturnType<typeof clientAnswering>['fetch']) =>
  fetch.mock.calls.map(([request]) => `${request.method} ${new URL(request.url).pathname}`)

describe('publishPost', () => {
  it('saves a draft, publishes it and gives the address of the post in its blog', async () => {
    const { client, fetch } = clientAnswering([201, { ...post, status: 'draft' }], [200, post], [200, topic])

    const outcome = await publishPost(client, input)

    expect(outcome).toMatchObject({ status: 'published', path: `/books/${postId}-knigi-oseni` })
    expect(requests(fetch)).toEqual([
      'POST /api/v1/posts',
      `POST /api/v1/posts/${postId}/publish`,
      `GET /api/v1/profiles/${blogId}`,
    ])
    expect(await fetch.mock.calls[0]![0].json()).toEqual({ title: input.title, blogId, body: input.body })
  })

  it('saves over the draft of an earlier attempt', async () => {
    const { client, fetch } = clientAnswering([200, post], [200, post], [200, topic])

    await publishPost(client, { ...input, draftId: postId })

    expect(requests(fetch)[0]).toBe(`PUT /api/v1/posts/${postId}`)
  })

  it('returns the field errors of the checks with the draft to try again', async () => {
    const { client } = clientAnswering(
      [201, { ...post, status: 'draft' }],
      [400, { code: 'validation_failed', errors: { body: ['Пустой пост опубликовать нельзя.'] } }],
    )

    expect(await publishPost(client, input)).toEqual({
      status: 'invalid',
      errors: { body: 'Пустой пост опубликовать нельзя.' },
      draftId: postId,
    })
  })

  it('tells an account without a confirmed email, a lost session and a failure apart', async () => {
    const draft: Answer = [201, { ...post, status: 'draft' }]

    expect(
      await publishPost(clientAnswering(draft, [403, { code: 'email_not_confirmed' }]).client, input),
    ).toEqual({ status: 'email-not-confirmed', draftId: postId })
    expect(await publishPost(clientAnswering([401, {}]).client, input)).toEqual({ status: 'unauthorized' })
    expect(await publishPost(clientAnswering(draft, [500, {}]).client, input)).toEqual({
      status: 'error',
      draftId: postId,
    })
    expect(
      await publishPost(clientAnswering([400, { errors: { blogId: ['Такого блога нет.'] } }]).client, input),
    ).toEqual({ status: 'invalid', errors: { blogId: 'Такого блога нет.' }, draftId: null })
  })

  it('leads to the post by its id when the blog cannot be read', async () => {
    const { client } = clientAnswering([201, post], [200, post], [500, {}])

    expect(await publishPost(client, input)).toMatchObject({
      status: 'published',
      path: `/u/${blogId}/${postId}`,
    })
  })
})

describe('listTopics', () => {
  it('lists the topics, or none when the API does not answer', async () => {
    expect(await listTopics(clientAnswering([200, [topic]]).client)).toEqual([topic])
    expect(await listTopics(clientAnswering([500, {}]).client)).toEqual([])
  })
})
