// Uploading an image from the browser (E04-03: the cropped avatar) through the media service (ADR-0030): the browser
// posts the file here, the server sends it on to POST /media with the person's access token (ADR-0018). The API
// checks the type by content and the size; the answer is the form's outcome.
import { uploadMedia } from '~/api/media'

// The image limit of the media service (Media:MaxBytes:Image) with room for the multipart framing.
const maxBytes = 20 * 1024 * 1024 + 64 * 1024

export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const client = ownApiClient(event)
  if (Number(getRequestHeader(event, 'content-length') ?? 0) > maxBytes)
    return { status: 'too-large' as const }
  const file = (await readMultipartFormData(event))?.find((part) => part.name === 'file' && part.filename)
  if (!file) throw createError({ statusCode: 400, message: 'The file is missing' })
  const blob = new Blob([new Uint8Array(file.data)], { type: file.type ?? 'application/octet-stream' })
  return uploadMedia(client, blob, file.filename!)
})
