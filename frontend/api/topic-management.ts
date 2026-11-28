// The management of a topic (E11-12, FR-TOPIC, ADR-0121): GET /api/v1/topics/{id}/management gives everyone the rules
// and the moderators and the reader's role; the owner edits the description and the rules (PUT …/settings) and the
// cover (PUT and DELETE …/cover), appoints and removes moderators (POST …/moderators, DELETE …/moderators/{accountId})
// and offers the topic to another person (PUT …/transfer), who accepts (POST …/transfer/accept) or declines
// (DELETE …/transfer, also the owner's cancel). The calls need the person's access token, which only the site's server
// holds (ADR-0018), so the browser goes through /bff/topic-management and /bff/topic-cover.
import type { ApiClient, components } from './client'
import { descriptionMaxLength, profileTextLength, type CoverInput, type ProfileSave } from './profiles'
import { t } from '../i18n'

export type TopicManagement = components['schemas']['TopicManagementView']
export type TopicMember = components['schemas']['TopicMember']

/** The rules of a topic: at most 2000 characters (SubsiteRules.RulesMaxLength). */
export const rulesMaxLength = 2000

export type ManagementLookup =
  { status: 'found'; management: TopicManagement } | { status: 'not-found' } | { status: 'error' }

/** What a change answers: the topic's management as it is now, field errors, a refusal or a failure. */
export type ManagementSave =
  | { status: 'saved'; management: TopicManagement }
  | { status: 'invalid'; errors: Record<string, string> }
  | { status: 'refused'; message: string }
  | { status: 'unauthorized' }
  | { status: 'error' }

/** The changes the page asks the site's server for (server/routes/bff/topic-management.post.ts). */
export type ManagementAction =
  | { action: 'settings'; description: string | null; rules: string | null }
  | { action: 'appoint'; nickname: string }
  | { action: 'remove-moderator'; accountId: string }
  | { action: 'offer'; nickname: string }
  | { action: 'withdraw' }
  | { action: 'accept' }

const actions = new Set(['settings', 'appoint', 'remove-moderator', 'offer', 'withdraw', 'accept'])

/** Whether `value` is an action the site's server passes on; it takes nothing else. */
export function managementActionOf(value: unknown): ManagementAction | null {
  if (!value || typeof value !== 'object') return null
  const body = value as Record<string, unknown>
  const text = (key: string) => (typeof body[key] === 'string' ? (body[key] as string) : null)
  if (!actions.has(String(body.action))) return null
  switch (body.action) {
    case 'settings':
      return { action: 'settings', description: text('description'), rules: text('rules') }
    case 'appoint':
    case 'offer':
      return { action: body.action, nickname: text('nickname') ?? '' }
    case 'remove-moderator':
      return text('accountId') ? { action: 'remove-moderator', accountId: text('accountId')! } : null
    default:
      return { action: body.action as 'withdraw' | 'accept' }
  }
}

/** Field errors the form finds itself before sending; the API checks the same. */
export function settingsInputErrors(input: { description: string; rules: string }): Record<string, string> {
  const errors: Record<string, string> = {}
  if (profileTextLength(input.description) > descriptionMaxLength)
    errors.description = t('blog.descriptionTooLong', { max: descriptionMaxLength })
  if (profileTextLength(input.rules) > rulesMaxLength)
    errors.rules = t('topicManage.rulesTooLong', { max: rulesMaxLength })
  return errors
}

/** A person as the page names them: the blog's name, else @nickname, else «Пользователь». */
export function memberName(member: Pick<TopicMember, 'name' | 'nickname'>): string {
  return member.name?.trim() || (member.nickname ? `@${member.nickname}` : t('notifications.someone'))
}

/** The address of a person's blog; `null` while they have none. */
export function memberPath(member: Pick<TopicMember, 'blogId' | 'nickname'>): string | null {
  return member.nickname ? `/u/${member.nickname}` : member.blogId ? `/u/${member.blogId}` : null
}

/** The address of a topic's management page. */
export function managementPath(topicId: string): string {
  return `/topics/${topicId}/manage`
}

/** Reads the management of a topic; the reader's token makes the role theirs. */
export async function loadTopicManagement(client: ApiClient, id: string): Promise<ManagementLookup> {
  try {
    const { data, response } = await client.GET('/api/v1/topics/{id}/management', {
      params: { path: { id } },
    })
    if (data) return { status: 'found', management: data }
    return response.status === 404 ? { status: 'not-found' } : { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

function fieldErrors(problem: unknown): Record<string, string> {
  const errors = (problem as { errors?: Record<string, string[]> } | undefined)?.errors ?? {}
  return Object.fromEntries(
    Object.entries(errors)
      .filter(([, messages]) => messages.length > 0)
      .map(([field, messages]) => [field, messages[0]!]),
  )
}

async function save(
  call: () => Promise<{ data?: TopicManagement; error?: unknown; response: Response }>,
): Promise<ManagementSave> {
  try {
    const { data, error, response } = await call()
    if (data) return { status: 'saved', management: data }
    if (response.status === 400) return { status: 'invalid', errors: fieldErrors(error) }
    if (response.status === 401) return { status: 'unauthorized' }
    if ([403, 404, 409].includes(response.status))
      return {
        status: 'refused',
        message: (error as { detail?: string } | undefined)?.detail ?? t('topicManage.refused'),
      }
    return { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/** Makes one change with the person's token (the site's server calls it). */
export function changeTopic(
  client: ApiClient,
  id: string,
  change: ManagementAction,
): Promise<ManagementSave> {
  const path = { params: { path: { id } } }
  switch (change.action) {
    case 'settings':
      return save(() =>
        client.PUT('/api/v1/topics/{id}/settings', {
          ...path,
          body: { description: change.description, rules: change.rules },
        }),
      )
    case 'appoint':
      return save(() =>
        client.POST('/api/v1/topics/{id}/moderators', { ...path, body: { nickname: change.nickname } }),
      )
    case 'remove-moderator':
      return save(() =>
        client.DELETE('/api/v1/topics/{id}/moderators/{accountId}', {
          params: { path: { id, accountId: change.accountId } },
        }),
      )
    case 'offer':
      return save(() =>
        client.PUT('/api/v1/topics/{id}/transfer', { ...path, body: { nickname: change.nickname } }),
      )
    case 'withdraw':
      return save(() => client.DELETE('/api/v1/topics/{id}/transfer', path))
    case 'accept':
      return save(() => client.POST('/api/v1/topics/{id}/transfer/accept', path))
  }
}

/** Sets or moves the topic's cover; the answer is the topic's profile, as for a blog's cover. */
export async function saveTopicCover(client: ApiClient, id: string, input: CoverInput): Promise<ProfileSave> {
  try {
    const { data, error, response } = await client.PUT('/api/v1/topics/{id}/cover', {
      params: { path: { id } },
      body: input,
    })
    if (data) return { status: 'saved', profile: data }
    if (response.status === 400) return { status: 'invalid', errors: fieldErrors(error) }
    if (response.status === 401) return { status: 'unauthorized' }
    return { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/** Removes the topic's cover. */
export async function removeTopicCover(client: ApiClient, id: string): Promise<ProfileSave> {
  try {
    const { data, response } = await client.DELETE('/api/v1/topics/{id}/cover', { params: { path: { id } } })
    if (data) return { status: 'saved', profile: data }
    if (response.status === 401) return { status: 'unauthorized' }
    return { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/** The calls of the page, as the browser makes them: through the site's own server. */
export interface TopicManagementApi {
  change(change: ManagementAction): Promise<ManagementSave>
}

export function topicManagementApi(id: string): TopicManagementApi {
  return {
    change: (change) =>
      $fetch<ManagementSave>('/bff/topic-management', { method: 'POST', body: { id, ...change } }).catch(
        (): ManagementSave => ({ status: 'error' }),
      ),
  }
}
