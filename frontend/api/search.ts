// The suggestions of the header's search (E14-02, FR-SEARCH, ADR-0097): blogs, topics and posts for a query being
// typed, its last word as the start of a word. Open to guests; a signed-in reader's black list applies, so the browser
// asks through the site's own server (/bff/search-suggestions), which adds the reader's token when there is one
// (ADR-0018).
import type { ApiClient } from './client'
import { outcome, type RegistrationOutcome } from './registration'
import { feedPostPath } from './feeds'
import { subsitePath } from './posts'
import type { components } from './schema'

export type SearchOutcome = RegistrationOutcome
export type SearchSuggestions = components['schemas']['SearchSuggestionsView']
export type SuggestedBlog = components['schemas']['SuggestedBlogView']
export type SuggestedPost = components['schemas']['SuggestedPostView']

/** The longest query the API takes (SearchEndpoints.MaxQueryLength). */
export const maxQueryLength = 100

/** How long typing pauses before the suggestions are asked for, in milliseconds. */
export const suggestDelay = 300

export interface SearchApi {
  suggest(query: string, signal?: AbortSignal): Promise<SearchOutcome>
}

/** The API call; the site's server makes it with the reader's token, or without one for a guest. */
export function searchSuggestions(client: ApiClient, query: string): Promise<SearchOutcome> {
  return outcome(() => client.GET('/api/v1/search/suggestions', { params: { query: { q: query } } }))
}

/** The call as the browser makes it: through the site's own server, which answers with the API's status. */
export function searchApi(): SearchApi {
  return {
    async suggest(query, signal) {
      try {
        const response = await $fetch.raw('/bff/search-suggestions', {
          query: { q: query },
          ignoreResponseError: true,
          signal,
        })
        return { status: response.status, body: response._data }
      } catch {
        return { status: 0 }
      }
    },
  }
}

/** The query as the API searches it: trimmed, its spaces collapsed. */
export function normalizeQuery(query: string): string {
  return query.trim().split(/\s+/u).filter(Boolean).join(' ')
}

/** Where a suggested blog leads: a topic at `/<nickname>`, a personal blog at `/u/<nickname>`. */
export function suggestedBlogPath(blog: Pick<SuggestedBlog, 'id' | 'kind' | 'nickname'>): string {
  return subsitePath({
    id: blog.id,
    nickname: blog.nickname ?? null,
    type: blog.kind === 'topic' ? 'topic' : 'personal',
  })
}

/** Where a suggested post leads: by its blog id, which the post page turns into its canonical address (ADR-0041). */
export function suggestedPostPath(post: Pick<SuggestedPost, 'id' | 'blogId'>): string {
  return feedPostPath({ id: post.id, blogId: post.blogId, slug: null })
}

/** The blog's name as a row shows it: its name, else `@nickname`. */
export function suggestedBlogName(blog: Pick<SuggestedBlog, 'name' | 'nickname'>): string {
  return blog.name?.trim() || (blog.nickname ? `@${blog.nickname}` : '')
}
