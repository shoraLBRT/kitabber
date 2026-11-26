// Topics the editor offers to publish in (E05-09, ADR-0046): GET /api/v1/topics, every topic by name. The topic page,
// subscriptions and the catalogue come with E11 (#76, #78). A person creates a topic with POST /api/v1/topics (E11-11,
// ADR-0084): a confirmed email, a limit of topics per account, a free and allowed name and address.
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
