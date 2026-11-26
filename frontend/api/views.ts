// The view counter of posts (E07-06, FR-POST, ADR-0071). The browser reports to the site's own server
// (server/routes/bff/views.post.ts) that a post's page was opened, which cards a feed showed and how long a page was
// read; the server passes the report on to the API with the person's access token when they are signed in, and with
// the reader's address and browser string, which the API needs to count a guest once a day and to leave bots out.
// Every report is fire-and-forget: a lost one costs one view, never an error on the page.
import type { ApiClient } from './client'

/** One report of the browser. */
export type ViewReport =
  | { kind: 'open'; postId: string }
  | { kind: 'impressions'; postIds: string[] }
  | { kind: 'reading'; postId: string; seconds: number }

/** How many cards one report names at most, as the API takes them. */
export const maxImpressions = 50

/** The longest reading time one report counts, in seconds, as the API takes it. */
export const maxReadSeconds = 7200

const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/** The report in `value` when it is one the API takes; `null` otherwise. */
export function parseViewReport(value: unknown): ViewReport | null {
  if (typeof value !== 'object' || value === null) return null
  const report = value as Record<string, unknown>
  const id = (v: unknown): v is string => typeof v === 'string' && uuid.test(v)
  if (report.kind === 'open' && id(report.postId)) return { kind: 'open', postId: report.postId }
  if (
    report.kind === 'impressions' &&
    Array.isArray(report.postIds) &&
    report.postIds.length > 0 &&
    report.postIds.length <= maxImpressions &&
    report.postIds.every(id)
  )
    return { kind: 'impressions', postIds: report.postIds }
  if (
    report.kind === 'reading' &&
    id(report.postId) &&
    Number.isInteger(report.seconds) &&
    (report.seconds as number) >= 1 &&
    (report.seconds as number) <= maxReadSeconds
  )
    return { kind: 'reading', postId: report.postId, seconds: report.seconds as number }
  return null
}

/** Sends the report from the browser; a fetch with keepalive survives the page being closed, as a beacon does. */
export function sendViewReport(report: ViewReport): void {
  try {
    void fetch('/bff/views', {
      method: 'POST',
      body: JSON.stringify(report),
      headers: { 'content-type': 'application/json' },
      keepalive: true,
      credentials: 'same-origin',
    }).catch(() => {})
  } catch {
    // A report that cannot be sent is a view not counted, nothing more.
  }
}

/** Passes the report on to the API (on the site's server); whether it was counted, the API does not say. */
export async function forwardViewReport(client: ApiClient, report: ViewReport): Promise<void> {
  if (report.kind === 'open') {
    await client.POST('/api/v1/posts/{id}/open', { params: { path: { id: report.postId } } })
  } else if (report.kind === 'impressions') {
    await client.POST('/api/v1/posts/impressions', { body: { postIds: report.postIds } })
  } else {
    await client.POST('/api/v1/posts/{id}/reading', {
      params: { path: { id: report.postId } },
      body: { seconds: report.seconds },
    })
  }
}
