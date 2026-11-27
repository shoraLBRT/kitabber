// Reposts (E10-08, FR-REACT, ADR-0117): a signed-in person reposts someone else's published post into their blog, with
// a text or without, through POST /api/v1/posts/{id}/reposts; the post page lists the reposts of the post, newest
// first, from GET /api/v1/posts/{id}/reposts. A repost is a post of its own in the reposter's blog: the feeds give it
// with the original inside (`original` of a feed card), and deleting the original hides it everywhere.
import type { ApiClient, components } from './client'
import type { FeedPost } from './feeds'
import { t } from '../i18n'

export type Repost = components['schemas']['RepostView']
export type RepostPage = components['schemas']['CursorPageOfRepostView']

/** A page of the list, or a failure the list shows with «Повторить». */
export type RepostsLookup = { status: 'found'; page: RepostPage } | { status: 'error' }

/** The outcome of reposting. */
export type RepostOutcome =
  | { status: 'done'; id: string }
  | { status: 'refused'; message: string }
  | { status: 'unauthorized' }
  | { status: 'error' }

/** The longest text over a repost (`PostRules.RepostTextMaxLength`). */
export const repostTextMaxLength = 1000

/** How many reposts one page of the list gives. */
export const repostsPageSize = 20

/** Whether a feed card is a repost with its original. */
export function isRepost(item: FeedPost): item is FeedPost & { original: NonNullable<FeedPost['original']> } {
  return Boolean(item.post.repostOfPostId && item.original)
}

/** Reposts `postId` with `text` (blank for none). */
export async function createRepost(client: ApiClient, postId: string, text: string): Promise<RepostOutcome> {
  try {
    const { data, error, response } = await client.POST('/api/v1/posts/{id}/reposts', {
      params: { path: { id: postId } },
      body: { text: text.trim() === '' ? null : text },
    })
    if (data) return { status: 'done', id: data.id }
    if (response.status === 401) return { status: 'unauthorized' }
    const code = (error as { code?: unknown } | undefined)?.code
    if (response.status === 409) return { status: 'refused', message: t('reposts.already') }
    if (response.status === 403 && code === 'posts.repost_own_post')
      return { status: 'refused', message: t('reposts.own') }
    if (response.status === 403) return { status: 'refused', message: t('reposts.emailNeeded') }
    if (response.status === 404) return { status: 'refused', message: t('reposts.notFound') }
    if (response.status === 400) return { status: 'refused', message: t('reposts.tooLong') }
    return { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/** One page of the reposts of `postId` after `cursor`; the first one without it. */
export async function loadReposts(
  client: ApiClient,
  postId: string,
  cursor?: string | null,
): Promise<RepostsLookup> {
  try {
    const { data } = await client.GET('/api/v1/posts/{id}/reposts', {
      params: { path: { id: postId }, query: { limit: repostsPageSize, ...(cursor ? { cursor } : {}) } },
    })
    return data ? { status: 'found', page: data } : { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/** The reposts of `next` after those already shown, each once. */
export function appendReposts(shown: readonly Repost[], next: readonly Repost[]): Repost[] {
  const seen = new Set(shown.map((r) => r.id))
  return [...shown, ...next.filter((r) => !seen.has(r.id))]
}
