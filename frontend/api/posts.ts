// The address of a post (E07-02, FR-POST, ADR-0041): `<blog address>/<id>-<slug>`, where the slug is the title in
// Latin letters that the API gives (`slug`), and a post without a title is `<blog address>/<id>`. A personal blog's
// address is `/u/<nickname>` (ADR-0025), a topic's is `/<topic>` (docs/ui/topic.md), so the scheme after it is one for
// both. The id alone finds the post: an address with another slug or another blog leads to the canonical one.
import { blogPath, type Profile } from './profiles'
import type { ApiClient, components } from './client'

export type Post = components['schemas']['PostView']

/**
 * A found post comes with its blog and its author (E07-04, ADR-0050): the personal blog of the account that wrote it,
 * the blog itself for a post in a personal blog; `null` when the author's blog cannot be read, so the header goes
 * without the author rather than the page failing. `own` says whether the reader wrote it (E07-05, ADR-0053): the «…»
 * menu then holds the author's items. The site's server decides it from its session, so the page never learns the
 * reader's account id.
 */
export type PostLookup =
  | { status: 'found'; post: Post; blog: Profile; author: Profile | null; path: string; own: boolean }
  | { status: 'not-found' }
  | { status: 'error' }

// The id the address begins with, then the end or a hyphen and the slug.
const segmentPattern = /^([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})(?:-[^/]*)?$/i

/** The post id an address segment names; `null` when the segment is not `<id>` or `<id>-<slug>`. */
export function postIdOf(segment: string): string | null {
  return segmentPattern.exec(segment)?.[1]?.toLowerCase() ?? null
}

/** The last segment of a post's address: `<id>-<slug>`, or `<id>` for a post without a title. */
export function postSegment(post: Pick<Post, 'id' | 'slug'>): string {
  return post.slug ? `${post.id}-${post.slug}` : post.id
}

/** The address of a blog or a topic, under which its posts live. */
export function subsitePath(blog: Pick<Profile, 'id' | 'nickname' | 'type'>): string {
  return blog.type === 'topic' ? `/${blog.nickname ?? blog.id}` : blogPath(blog)
}

/** The canonical address of a post in its blog. */
export function postPath(
  blog: Pick<Profile, 'id' | 'nickname' | 'type'>,
  post: Pick<Post, 'id' | 'slug'>,
): string {
  return `${subsitePath(blog)}/${postSegment(post)}`
}

/**
 * Looks the post of an address segment up, and its blog for the canonical address. A draft is found only with its
 * author's access token (ADR-0029), so the client must carry the reader's token (server side only, ADR-0018);
 * `reader` is the account of that token, `null` for a guest.
 */
export async function lookUpPost(
  client: ApiClient,
  segment: string,
  reader: string | null = null,
): Promise<PostLookup> {
  const id = postIdOf(segment)
  if (!id) return { status: 'not-found' }
  try {
    const { data: post, response } = await client.GET('/api/v1/posts/{id}', { params: { path: { id } } })
    if (!post) return response.status === 404 ? { status: 'not-found' } : { status: 'error' }
    const { data: blog, response: blogResponse } = await client.GET('/api/v1/profiles/{id}', {
      params: { path: { id: post.blogId } },
    })
    if (!blog) return blogResponse.status === 404 ? { status: 'not-found' } : { status: 'error' }
    return {
      status: 'found',
      post,
      blog,
      author: await authorOf(client, post, blog),
      path: postPath(blog, post),
      own: reader !== null && reader.toLowerCase() === post.authorAccountId.toLowerCase(),
    }
  } catch {
    return { status: 'error' }
  }
}

// Only its owner posts in a personal blog, so its profile is the author's; in a topic the author is looked up.
async function authorOf(client: ApiClient, post: Post, blog: Profile): Promise<Profile | null> {
  if (blog.type === 'personal') return blog
  try {
    const { data } = await client.GET('/api/v1/profiles/by-account/{accountId}', {
      params: { path: { accountId: post.authorAccountId } },
    })
    return data ?? null
  } catch {
    return null
  }
}

/**
 * Deletes the reader's own post (E07-05, ADR-0053): the API deletes softly and only for the author. The answer's status:
 * 204 once deleted, 401, 403 (`posts.not_author`) or 404 otherwise.
 */
export async function deletePost(client: ApiClient, id: string): Promise<number> {
  const { response } = await client.DELETE('/api/v1/posts/{id}', { params: { path: { id } } })
  return response.status
}

/** The plain text of the text blocks, for the feed card until it draws the blocks with PostBlocks (E08-02, #55). */
export function postText(post: Pick<Post, 'body'>): string[] {
  const blocks = (post.body as { blocks?: { type?: string; data?: { text?: unknown } }[] }).blocks ?? []
  return blocks
    .map((block) => (typeof block.data?.text === 'string' ? plain(block.data.text) : ''))
    .filter((text) => text.length > 0)
}

const entities: Record<string, string> = { amp: '&', lt: '<', gt: '>', quot: '"', '#39': "'", nbsp: ' ' }

function plain(html: string): string {
  return html
    .replace(/<br\s*\/?>/gi, ' ')
    .replace(/<[^>]*>/g, '')
    .replace(/&(amp|lt|gt|quot|#39|nbsp);/g, (_, name: string) => entities[name]!)
    .trim()
}
