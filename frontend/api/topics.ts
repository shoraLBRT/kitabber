// Topics the editor offers to publish in (E05-09, ADR-0046): GET /api/v1/topics, every topic by name. The topic page,
// subscriptions and the catalogue come with E11 (#76, #78).
import type { ApiClient, components } from './client'

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
