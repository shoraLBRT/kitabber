// Cropping the avatar in the browser (E04-03, ADR-0034): the picture lies under a square frame, the person moves and
// zooms it, and the part under the frame becomes a square image of `avatarSize` pixels that is uploaded instead of
// the original. The picture always covers the frame, so the avatar has no empty corners.

/** The side of the uploaded avatar in pixels. */
export const avatarSize = 512

export const minZoom = 1
export const maxZoom = 4

/** Where the picture lies: its zoom over "just covers the frame" and the shift of its centre from the frame's, in frame pixels. */
export interface CropState {
  zoom: number
  x: number
  y: number
}

export interface Size {
  width: number
  height: number
}

/** The scale at which the picture just covers a frame of side `frame`, times the zoom. */
export function cropScale(image: Size, frame: number, zoom: number): number {
  return (frame / Math.min(image.width, image.height)) * zoom
}

/** The state with the zoom kept in its range and the shift kept so that the picture still covers the frame. */
export function clampCrop(state: CropState, image: Size, frame: number): CropState {
  const zoom = Math.min(maxZoom, Math.max(minZoom, state.zoom))
  const scale = cropScale(image, frame, zoom)
  const limitX = Math.max(0, (image.width * scale - frame) / 2)
  const limitY = Math.max(0, (image.height * scale - frame) / 2)
  // `+ 0` turns -0 into 0.
  const clamp = (value: number, limit: number) => Math.min(limit, Math.max(-limit, value)) + 0
  return { zoom, x: clamp(state.x, limitX), y: clamp(state.y, limitY) }
}

/** The square of the original picture, in its own pixels, that lies under the frame. */
export function cropSource(
  state: CropState,
  image: Size,
  frame: number,
): { x: number; y: number; size: number } {
  const { zoom, x, y } = clampCrop(state, image, frame)
  const scale = cropScale(image, frame, zoom)
  const size = frame / scale
  return { x: image.width / 2 - x / scale - size / 2, y: image.height / 2 - y / scale - size / 2, size }
}

/** The part of `image` under the frame as a square JPEG of `avatarSize` pixels. */
export async function cropAvatar(image: HTMLImageElement, state: CropState, frame: number): Promise<Blob> {
  const source = cropSource(state, { width: image.naturalWidth, height: image.naturalHeight }, frame)
  const canvas = document.createElement('canvas')
  canvas.width = avatarSize
  canvas.height = avatarSize
  const context = canvas.getContext('2d')
  if (!context) throw new Error('No 2D canvas')
  // JPEG has no transparency: a transparent PNG gets a white background instead of a black one.
  context.fillStyle = '#ffffff'
  context.fillRect(0, 0, avatarSize, avatarSize)
  context.drawImage(image, source.x, source.y, source.size, source.size, 0, 0, avatarSize, avatarSize)
  return new Promise((resolve, reject) =>
    canvas.toBlob(
      (blob) => (blob ? resolve(blob) : reject(new Error('The avatar was not encoded'))),
      'image/jpeg',
      0.9,
    ),
  )
}
