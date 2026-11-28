// The notifications of the signed-in person (E12-02, FR-NOTIFY, ADR-0093, docs/ui/notifications.md): the feed newest
// first, page by page with a cursor, the number of the unread ones for the header's counter, and marking the shown ones
// read. The calls need the person's access token, which only the site's server holds (ADR-0018), so the browser goes
// through /bff/notifications (server/routes/bff/notifications*.ts).
import type { ApiClient } from './client'
import { outcome, type RegistrationOutcome } from './registration'
import { commentHash } from './comments'
import { blogPath } from './profiles'
import type { components } from './schema'
import { t } from '../i18n'

export type NotificationsOutcome = RegistrationOutcome

/** One notification of the feed. */
export type Notification = components['schemas']['NotificationView']

/** A page of the feed. */
export type NotificationPage = components['schemas']['CursorPageOfNotificationView']

export interface NotificationsApi {
  load(cursor?: string | null): Promise<NotificationsOutcome>
  unread(): Promise<NotificationsOutcome>
  markRead(ids: readonly string[]): Promise<NotificationsOutcome>
}

/** The API calls with the person's token: the site's server makes them (server/routes/bff). */
export function ownNotifications(client: ApiClient) {
  return {
    load: (cursor?: string | null) =>
      outcome(() => client.GET('/api/v1/notifications/me', { params: { query: cursor ? { cursor } : {} } })),
    unread: () => outcome(() => client.GET('/api/v1/notifications/me/unread')),
    markRead: (ids: readonly string[]) =>
      outcome(() => client.POST('/api/v1/notifications/me/read', { body: { ids: [...ids] } })),
  }
}

async function bff(
  path: string,
  method: 'GET' | 'POST',
  options: { query?: Record<string, string>; body?: Record<string, unknown> } = {},
): Promise<NotificationsOutcome> {
  try {
    const response = await $fetch.raw(path, { method, ignoreResponseError: true, ...options })
    return { status: response.status, body: response._data }
  } catch {
    return { status: 0 }
  }
}

/** The calls as the browser makes them: through the site's own server, which answers with the API's status. */
export function notificationsApi(): NotificationsApi {
  return {
    load: (cursor) => bff('/bff/notifications', 'GET', { query: cursor ? { cursor } : {} }),
    unread: () => bff('/bff/notifications/unread', 'GET'),
    markRead: (ids) => bff('/bff/notifications/read', 'POST', { body: { ids: [...ids] } }),
  }
}

/** The unread number of an answer of `unread` or `markRead`; `null` when the call failed. */
export function unreadOf(result: NotificationsOutcome): number | null {
  const unread = (result.body as { unread?: unknown } | undefined)?.unread
  return result.status === 200 && typeof unread === 'number' ? unread : null
}

/** The counter's text: «99+» above 99. */
export function badgeText(count: number): string {
  return count > 99 ? '99+' : String(count)
}

/** The person who acted, as the row names them: the blog's name, else its nickname, else «Пользователь». */
export function actorName(notification: Pick<Notification, 'actor'>): string {
  const { name, nickname } = notification.actor
  return name ?? (nickname ? `@${nickname}` : t('notifications.someone'))
}

/** What happened, after the person's name: «ответил на ваш комментарий». An unknown type is a plain row. */
export function actionText(notification: Pick<Notification, 'type'>): string {
  switch (notification.type) {
    case 'comment_reply':
      return t('notifications.commentReply')
    case 'post_comment':
      return t('notifications.postComment')
    case 'reaction':
      return t('notifications.reaction')
    case 'mention':
      return t('notifications.mention')
    case 'new_subscriber':
      return t('notifications.newSubscriber')
    case 'repost':
      return t('notifications.repost')
    case 'subscription_post':
      return t('notifications.subscriptionPost')
    case 'topic_transfer_offer':
      return t('notifications.topicTransferOffer')
    default:
      return t('notifications.unknownType')
  }
}

/**
 * Where the row leads: the post (to the comment, when there is one), the management page of a topic offered to the
 * reader (E11-12), or, for a subscriber, their blog. The id alone finds
 * a post and the site leads on to its canonical address (ADR-0041), so the row needs no blog of the post.
 */
export function notificationLink(notification: Notification): string | null {
  if (notification.type === 'topic_transfer_offer' && notification.blogId)
    return `/topics/${notification.blogId}/manage`
  if (notification.postId) {
    const path = `/u/-/${notification.postId}`
    return notification.commentId ? `${path}${commentHash(notification.commentId)}` : path
  }
  const { blogId, nickname } = notification.actor
  return blogId || nickname ? blogPath({ id: blogId ?? '', nickname: nickname ?? null }) : null
}

/** The rows shown and those of the next page, one row per id. */
export function appendNotifications(
  shown: readonly Notification[],
  next: readonly Notification[],
): Notification[] {
  const seen = new Set(shown.map((n) => n.id))
  return [...shown, ...next.filter((n) => !seen.has(n.id))]
}
