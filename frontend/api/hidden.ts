// What the account hid from its feeds (E11-07, FR-SUB, FR-TOPIC, ADR-0119): posts hidden from the «…» menu of a post
// and topics hidden on the topic's page, read and brought back in the settings. The calls need the person's access
// token, which only the site's server holds (ADR-0018), so the browser goes through /bff/hidden
// (server/routes/bff/hidden*.ts).
import type { ApiClient } from './client'
import { outcome, type RegistrationOutcome } from './registration'
import type { components } from './schema'

export type HiddenOutcome = RegistrationOutcome

/** GET /api/v1/preferences/me/hidden: the hidden posts and topics, the latest hidden first. */
export type HiddenList = components['schemas']['HiddenListView']

/** One hidden post; `gone` when it is no longer published. */
export type HiddenPost = components['schemas']['HiddenPostView']

/** One hidden topic. */
export type HiddenTopic = components['schemas']['HiddenTopicView']

/** What is hidden: a post or a topic. */
export type HiddenKind = 'posts' | 'topics'

export interface HiddenApi {
  load(): Promise<HiddenOutcome>
  /** Whether the topic is hidden: 200 with `{ hidden }`. */
  topicState(blogId: string): Promise<HiddenOutcome>
  hide(kind: HiddenKind, id: string): Promise<HiddenOutcome>
  show(kind: HiddenKind, id: string): Promise<HiddenOutcome>
}

/** The API calls with the person's token: the site's server makes them (server/routes/bff). */
export function ownHidden(client: ApiClient) {
  return {
    load: () => outcome(() => client.GET('/api/v1/preferences/me/hidden')),
    topicState: (blogId: string) =>
      outcome(() =>
        client.GET('/api/v1/preferences/me/hidden/topics/{blogId}', { params: { path: { blogId } } }),
      ),
    hide: (kind: HiddenKind, id: string) =>
      outcome(() =>
        kind === 'posts'
          ? client.PUT('/api/v1/preferences/me/hidden/posts/{postId}', { params: { path: { postId: id } } })
          : client.PUT('/api/v1/preferences/me/hidden/topics/{blogId}', { params: { path: { blogId: id } } }),
      ),
    show: (kind: HiddenKind, id: string) =>
      outcome(() =>
        kind === 'posts'
          ? client.DELETE('/api/v1/preferences/me/hidden/posts/{postId}', {
              params: { path: { postId: id } },
            })
          : client.DELETE('/api/v1/preferences/me/hidden/topics/{blogId}', {
              params: { path: { blogId: id } },
            }),
      ),
  }
}

/** Whether `kind` names what may be hidden; the site's server takes nothing else. */
export function isHiddenKind(kind: unknown): kind is HiddenKind {
  return kind === 'posts' || kind === 'topics'
}

async function bff(path: string, method: 'GET' | 'PUT' | 'DELETE'): Promise<HiddenOutcome> {
  try {
    const response = await $fetch.raw(path, { method, ignoreResponseError: true })
    return { status: response.status, body: response._data }
  } catch {
    return { status: 0 }
  }
}

/** The calls as the browser makes them: through the site's own server, which answers with the API's status. */
export function hiddenApi(): HiddenApi {
  return {
    load: () => bff('/bff/hidden', 'GET'),
    topicState: (blogId) => bff(`/bff/hidden/topics/${encodeURIComponent(blogId)}`, 'GET'),
    hide: (kind, id) => bff(`/bff/hidden/${kind}/${encodeURIComponent(id)}`, 'PUT'),
    show: (kind, id) => bff(`/bff/hidden/${kind}/${encodeURIComponent(id)}`, 'DELETE'),
  }
}
