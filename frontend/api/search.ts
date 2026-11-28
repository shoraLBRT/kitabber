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

// The results page (E14-03, FR-SEARCH, ADR-0124): the blocks «Блоги», «Темы» and «Посты» of a query, each read page by
// page by an offset through the site's own server (/bff/search-blogs, /bff/search-posts), which adds the reader's
// token, so «Подписаться» is marked and the black list applies. The query and the filters of «Посты» stay in the
// address, so a search can be shared and survives a reload.
export type FoundBlog = components['schemas']['FoundBlogView']
export type FoundPost = components['schemas']['FoundPostView']
export type FoundBlogsPage = components['schemas']['FoundBlogsPage']
export type FoundPostsPage = components['schemas']['FoundPostsPage']

/** The blocks of blogs: personal blogs («Блоги») and topics («Темы»). */
export type SearchBlogKind = 'personal' | 'topic'
export type SearchPeriod = 'all' | 'day' | 'week' | 'month' | 'year'
export type SearchOrder = 'relevance' | 'date'

export const searchPeriods: readonly SearchPeriod[] = ['all', 'day', 'week', 'month', 'year']
export const searchOrders: readonly SearchOrder[] = ['relevance', 'date']

/** The filters of the block «Посты». */
export interface SearchFilters {
  titlesOnly: boolean
  period: SearchPeriod
  order: SearchOrder
}

export const defaultSearchFilters: SearchFilters = { titlesOnly: false, period: 'all', order: 'relevance' }

/** Where the results of `query` are: in the section «Рейтинг», the query in the address. */
export function searchResultsPath(query: string): string {
  return `/rating?q=${encodeURIComponent(normalizeQuery(query))}`
}

/** The filters an address carries; an unknown value is the default. */
export function filtersOf(query: Record<string, unknown>): SearchFilters {
  const { period, order } = query
  return {
    titlesOnly: query.titles === '1',
    period: searchPeriods.find((p) => p === period) ?? 'all',
    order: searchOrders.find((o) => o === order) ?? 'relevance',
  }
}

/** The address query of `q` and `filters`: a default filter is left out. */
export function resultsQuery(q: string, filters: SearchFilters): Record<string, string> {
  return {
    q,
    ...(filters.titlesOnly ? { titles: '1' } : {}),
    ...(filters.period !== 'all' ? { period: filters.period } : {}),
    ...(filters.order !== 'relevance' ? { order: filters.order } : {}),
  }
}

/** Whether any filter differs from the default. */
export function filtered(filters: SearchFilters): boolean {
  return filters.titlesOnly || filters.period !== 'all' || filters.order !== 'relevance'
}

export type BlogsLookup = { status: 'found'; page: FoundBlogsPage } | { status: 'error' }
export type PostsLookup = { status: 'found'; page: FoundPostsPage } | { status: 'error' }

/** A page of a block of blogs; the site's server asks with the reader's token, or without one for a guest. */
export async function loadFoundBlogs(
  client: ApiClient,
  q: string,
  kind: SearchBlogKind,
  offset: number,
): Promise<BlogsLookup> {
  const result = await outcome(() =>
    client.GET('/api/v1/search/blogs', { params: { query: { q, kind, offset } } }),
  )
  return result.status === 200 && result.body
    ? { status: 'found', page: result.body as FoundBlogsPage }
    : { status: 'error' }
}

/** A page of the block «Посты» with its filters. */
export async function loadFoundPosts(
  client: ApiClient,
  q: string,
  filters: SearchFilters,
  offset: number,
): Promise<PostsLookup> {
  const result = await outcome(() =>
    client.GET('/api/v1/search/posts', { params: { query: { q, offset, ...filters } } }),
  )
  return result.status === 200 && result.body
    ? { status: 'found', page: result.body as FoundPostsPage }
    : { status: 'error' }
}

export interface SearchResultsApi {
  blogs(q: string, kind: SearchBlogKind, offset: number): Promise<BlogsLookup>
  posts(q: string, filters: SearchFilters, offset: number): Promise<PostsLookup>
}

/** The calls as the browser makes them: through the site's own server. */
export function searchResultsApi(): SearchResultsApi {
  return {
    blogs: (q, kind, offset) =>
      $fetch<BlogsLookup>('/bff/search-blogs', { query: { q, kind, offset } }).catch((): BlogsLookup => ({
        status: 'error',
      })),
    posts: (q, filters, offset) =>
      $fetch<PostsLookup>('/bff/search-posts', { query: { q, offset, ...filters } }).catch(
        (): PostsLookup => ({ status: 'error' }),
      ),
  }
}

/** A page appended to the rows shown, without the rows already there. */
export function appendFound<T extends { id: string }>(shown: readonly T[], page: readonly T[]): T[] {
  const ids = new Set(shown.map((row) => row.id))
  return [...shown, ...page.filter((row) => !ids.has(row.id))]
}
