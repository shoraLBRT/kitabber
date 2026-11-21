// Uploading a file through the media service (E06-01, ADR-0030), here for the avatar (E04-03, ADR-0034). The API
// checks the type by content and the size; the outcome is what the form tells the person.
import type { ApiClient, components } from './client'

export type UploadedMedia = components['schemas']['UploadedMedia']

export type MediaUpload =
  | { status: 'uploaded'; media: UploadedMedia }
  | { status: 'unsupported' }
  | { status: 'too-large' }
  | { status: 'unauthorized' }
  | { status: 'error' }

/** POST /media with the file in the field `file` (multipart/form-data). */
export async function uploadMedia(client: ApiClient, file: Blob, fileName: string): Promise<MediaUpload> {
  try {
    const form = new FormData()
    // A File carries its name everywhere; some FormData implementations drop the name given next to a Blob.
    form.append('file', new File([file], fileName, { type: file.type }))
    const { data, response } = await client.POST('/api/v1/media', {
      // The generated type describes the form's fields; the body sent is the ready FormData itself.
      body: form as never,
      bodySerializer: (body) => body as unknown as FormData,
    })
    if (data) return { status: 'uploaded', media: data }
    if (response.status === 415) return { status: 'unsupported' }
    if (response.status === 413) return { status: 'too-large' }
    if (response.status === 401) return { status: 'unauthorized' }
    return { status: 'error' }
  } catch {
    return { status: 'error' }
  }
}
