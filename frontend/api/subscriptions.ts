// Subscribing to a blog or a topic (E11-03, FR-SUB, ADR-0085): PUT and DELETE /api/v1/blogs/{id}/subscription, both
// idempotent. The numbers and the reader's own mark come with the profile; a change answers with the new ones.
import type { ApiClient, components } from './client'
import type { Profile } from './profiles'
import { locale, t } from '../i18n'

export type SubscriptionView = components['schemas']['SubscriptionView']

/** The outcome of a change: the new numbers, the server's reason, or the person is signed out. */
export type SubscriptionChange =
  | { status: 'done'; state: SubscriptionView }
  | { status: 'refused'; message: string }
  | { status: 'unauthorized' }
  | { status: 'error' }

const refusedMessages: Record<number, string> = {
  403: t('subscription.ownBlog'),
  404: t('subscription.notFound'),
}

/** Subscribes (`subscribed`) to the blog `blogId` or unsubscribes from it. */
export async function changeSubscription(
  client: ApiClient,
  blogId: string,
  subscribed: boolean,
): Promise<SubscriptionChange> {
  try {
    const options = { params: { path: { id: blogId } } }
    const { data, error, response } = subscribed
      ? await client.PUT('/api/v1/blogs/{id}/subscription', options)
      : await client.DELETE('/api/v1/blogs/{id}/subscription', options)
    if (data) return { status: 'done', state: data }
    if (response.status === 401) return { status: 'unauthorized' }
    const detail = (error as { detail?: string } | undefined)?.detail
    const refused = refusedMessages[response.status]
    return refused ? { status: 'refused', message: detail ?? refused } : { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/** The subscription of a profile as its header shows it. */
export function subscriptionOf(profile: Pick<Profile, 'subscriberCount' | 'subscribed'>): SubscriptionView {
  return { subscriberCount: profile.subscriberCount, subscribed: profile.subscribed }
}

/** The state shown at once, before the server answers: subscribed or not, the count moved by one. */
export function toggledSubscription(state: SubscriptionView): SubscriptionView {
  return state.subscribed
    ? { subscriberCount: Math.max(0, state.subscriberCount - 1), subscribed: false }
    : { subscriberCount: state.subscriberCount + 1, subscribed: true }
}

/** The action a guest's press remembers for the sign-in window (ADR-0032). */
export function subscribeAction(blogId: string): string {
  return `subscribe:${blogId}`
}

const compact = new Intl.NumberFormat(locale, { notation: 'compact', maximumFractionDigits: 1 })

/** A count as the header shows it: «1,2 тыс.» from a thousand on (docs/ui/blog.md). */
export function compactCount(count: number): string {
  return count < 1000 ? String(count) : compact.format(count)
}

// The lists behind the header's numbers (E04-06, FR-PROFILE, ADR-0099): GET /api/v1/blogs/{id}/subscribers and
// …/subscriptions, the newest subscriptions first, page by page with a cursor; each row carries the reader's own mark.
export type SubscriptionCard = components['schemas']['SubscriptionCard']
export type SubscriptionListPage = components['schemas']['CursorPageOfSubscriptionCard']

/** Which list: who follows the blog, or whom the owner of a personal blog follows. */
export type SubscriptionListKind = 'subscribers' | 'subscriptions'

export type SubscriptionListLookup = { status: 'found'; page: SubscriptionListPage } | { status: 'error' }

/** One page of a list: the first without a cursor, the next with the previous page's `nextCursor`. */
export async function loadSubscriptionList(
  client: ApiClient,
  blogId: string,
  kind: SubscriptionListKind,
  cursor?: string | null,
): Promise<SubscriptionListLookup> {
  try {
    const options = { params: { path: { id: blogId }, query: cursor ? { cursor } : {} } }
    const { data } =
      kind === 'subscribers'
        ? await client.GET('/api/v1/blogs/{id}/subscribers', options)
        : await client.GET('/api/v1/blogs/{id}/subscriptions', options)
    return data ? { status: 'found', page: data } : { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}

/** Adds a later page to the rows already shown, leaving out any row that is already there. */
export function appendSubscriptionCards(
  shown: SubscriptionCard[],
  page: SubscriptionListPage,
): SubscriptionCard[] {
  const seen = new Set(shown.map((card) => card.id))
  return [...shown, ...page.items.filter((card) => !seen.has(card.id))]
}
