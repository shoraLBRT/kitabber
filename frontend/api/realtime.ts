// The hub ticket (E01-11, ADR-0019): the Nuxt server trades the person's access token for a short-lived ticket that
// only opens the realtime hub, so the browser never holds the token (ADR-0018). Called by
// server/routes/auth/realtime-ticket.post.ts with a client that carries the token.
import type { ApiClient, components } from './client'

export type RealtimeTicket = components['schemas']['RealtimeTicket']

/** The ticket, or null when the API does not take the token (expired or revoked: the person is signed out). */
export async function requestRealtimeTicket(client: ApiClient): Promise<RealtimeTicket | null> {
  const { data, response } = await client.POST('/api/v1/realtime/tickets')
  if (response.status === 401) return null
  if (!data) throw new Error(`The API gave no realtime ticket: ${response.status}`)
  return data
}
