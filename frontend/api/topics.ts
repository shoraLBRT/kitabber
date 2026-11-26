// Topics the editor offers to publish in (E05-09, ADR-0046): GET /api/v1/topics, every topic by name. A person creates
// a topic with POST /api/v1/topics (E11-11, ADR-0084): a confirmed email, a limit of topics per account, a free and
// allowed name and address. The catalogue behind «Показать все» and the menu's topics (E11-02, ADR-0088):
// GET /api/v1/topics/catalog, the most followed first, searched, page by page; GET /api/v1/topics/menu, the person's
// subscribed topics or the popular ones.
import type { ApiClient, components } from './client'
import { descriptionMaxLength, nameMaxLength, profileTextLength } from './profiles'
import { t } from '../i18n'
export type Topic = components['schemas']['TopicSummary']

/** Every topic; an empty list when the API does not answer, so the editor still offers «Без темы». */
export async function listTopics(client: ApiClient): Promise<Topic[]> {
  try {
    const { data } = await client.GET('/api/v1/topics')
    return data ?? []
  } catch {
    return []
  }
}

export type TopicInput = components['schemas']['TopicInput']

/** A topic's name: from 2 characters (ADR-0084) to the blog name's 25. */
export const topicNameMinLength = 2

/** The address rule of blogs and topics (SubsiteRules.NormalizeNickname): 3–32 Latin letters, digits and _, a letter first. */
const addressForm = /^[a-z][a-z0-9_]{2,31}$/

/** The address as the API stores it: trimmed, without a leading @, in lower case. */
export function normalizeTopicAddress(address: string): string {
  return address.trim().replace(/^@+/, '').toLowerCase()
}

/** What the form checks itself before sending; the API checks the same and more (taken, reserved, the stop list). */
export function topicInputErrors(input: {
  name: string
  nickname: string
  description: string
}): Record<string, string> {
  const errors: Record<string, string> = {}
  const nameLength = profileTextLength(input.name.trim())
  if (nameLength < topicNameMinLength || nameLength > nameMaxLength)
    errors.name = t('topicCreate.nameLength', { min: topicNameMinLength, max: nameMaxLength })
  const address = normalizeTopicAddress(input.nickname)
  if (!addressForm.test(address) || /^id[0-9]+$/.test(address)) errors.nickname = t('topicCreate.addressForm')
  if (profileTextLength(input.description) > descriptionMaxLength)
    errors.description = t('blog.descriptionTooLong', { max: descriptionMaxLength })
  return errors
}

export type TopicCreate =
  | { status: 'created'; topic: Topic }
  | { status: 'invalid'; errors: Record<string, string> }
  | { status: 'limit'; message: string }
  | { status: 'unconfirmed' }
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

/** Creates a topic owned by the signed-in account. */
export async function createTopic(client: ApiClient, input: TopicInput): Promise<TopicCreate> {
  try {
    const { data, error, response } = await client.POST('/api/v1/topics', { body: input })
    if (data) return { status: 'created', topic: data }
    if (response.status === 400) return { status: 'invalid', errors: fieldErrors(error) }
    if (response.status === 401) return { status: 'unauthorized' }
    if (response.status === 403) return { status: 'unconfirmed' }
    if (response.status === 409) {
      const detail = (error as { detail?: string } | undefined)?.detail
      return { status: 'limit', message: detail ?? t('topicCreate.limit') }
    }
    return { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

export type TopicCard = components['schemas']['TopicCard']
export type TopicCatalogPage = components['schemas']['CursorPageOfTopicCard']
export type TopicMenu = components['schemas']['TopicMenu']

/** The longest search the API takes (TopicCatalogRules.QueryMaxLength). */
export const catalogQueryMaxLength = 50

export type CatalogLookup = { status: 'found'; page: TopicCatalogPage } | { status: 'error' }

/** One page of the catalogue: the first without a cursor, the next with the previous page's `nextCursor`. */
export async function loadTopicCatalog(
  client: ApiClient,
  search: string,
  cursor?: string | null,
): Promise<CatalogLookup> {
  const q = search.trim()
  try {
    const { data } = await client.GET('/api/v1/topics/catalog', {
      params: { query: { ...(q ? { q } : {}), ...(cursor ? { cursor } : {}) } },
    })
    return data ? { status: 'found', page: data } : { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/** Adds a later page to the topics already shown, leaving out any topic that is already there. */
export function appendTopics(shown: TopicCard[], page: TopicCatalogPage): TopicCard[] {
  const seen = new Set(shown.map((topic) => topic.id))
  return [...shown, ...page.items.filter((topic) => !seen.has(topic.id))]
}

/** The menu's topics; `null` when the API does not answer. */
export async function loadTopicMenu(client: ApiClient): Promise<TopicMenu | null> {
  try {
    const { data } = await client.GET('/api/v1/topics/menu')
    return data ?? null
  } catch {
    return null
  }
}

/** The menu's topics as menu items: each leads to its page /<topic>. */
export function topicMenuItems(
  menu: TopicMenu | null | undefined,
): { id: string; label: string; to: string }[] {
  return (menu?.topics ?? []).map((topic) => ({ id: topic.id, label: topic.name, to: `/${topic.nickname}` }))
}
