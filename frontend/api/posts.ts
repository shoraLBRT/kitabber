// The address of a post (E07-02, FR-POST, ADR-0041): `<blog address>/<id>-<slug>`, where the slug is the title in
// Latin letters that the API gives (`slug`), and a post without a title is `<blog address>/<id>`. A personal blog's
// address is `/u/<nickname>` (ADR-0025), a topic's is `/<topic>` (docs/ui/topic.md), so the scheme after it is one for
// both. The id alone finds the post: an address with another slug or another blog leads to the canonical one.
import { blogPath, type Profile } from './profiles'
import type { ApiClient, components } from './client'

export type Post = components['schemas']['PostView']

export type PostLookup =
  { status: 'found'; post: Post; blog: Profile; path: string } | { status: 'not-found' } | { status: 'error' }

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
 * author's access token (ADR-0029), so the client must carry the reader's token (server side only, ADR-0018).
 */
export async function lookUpPost(client: ApiClient, segment: string): Promise<PostLookup> {
  const id = postIdOf(segment)
  if (!id) return { status: 'not-found' }
  try {
    const { data: post, response } = await client.GET('/api/v1/posts/{id}', { params: { path: { id } } })
    if (!post) return response.status === 404 ? { status: 'not-found' } : { status: 'error' }
    const { data: blog, response: blogResponse } = await client.GET('/api/v1/profiles/{id}', {
      params: { path: { id: post.blogId } },
    })
    if (!blog) return blogResponse.status === 404 ? { status: 'not-found' } : { status: 'error' }
    return { status: 'found', post, blog, path: postPath(blog, post) }
  } catch {
    return { status: 'error' }
  }
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
