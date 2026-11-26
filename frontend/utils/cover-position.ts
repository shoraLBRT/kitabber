// The cover's position (E04-04, ADR-0079): the header shows the whole width of the picture and a band of its height,
// `object-fit: cover; object-position: 50% <position>%`. The header keeps one proportion on every screen (coverRatio),
// so the same position shows the same band on a desktop and on a phone. Dragging the picture up or down by some pixels
// moves the band by the share of the picture's hidden height those pixels are.

/** The header's proportion, width to height: the feed column (640 px) by 192 px, the same on a phone. */
export const coverRatio = 10 / 3

export const coverPositionMin = 0
export const coverPositionMax = 100
export const coverPositionDefault = 50

/** A whole position from 0 to 100; anything that is not a number is the middle. */
export function clampCoverPosition(position: number): number {
  if (!Number.isFinite(position)) return coverPositionDefault
  return Math.min(coverPositionMax, Math.max(coverPositionMin, Math.round(position)))
}

/**
 * How many pixels of the picture's height the header hides: the picture is scaled to cover the box, so a picture
 * relatively taller than the box overflows it vertically. 0 when it does not (a wide panorama): then the position
 * changes nothing.
 */
export function coverOverflow(
  boxWidth: number,
  boxHeight: number,
  imageWidth: number,
  imageHeight: number,
): number {
  if (boxWidth <= 0 || boxHeight <= 0 || imageWidth <= 0 || imageHeight <= 0) return 0
  const scale = Math.max(boxWidth / imageWidth, boxHeight / imageHeight)
  return Math.max(0, imageHeight * scale - boxHeight)
}

/**
 * The position after the picture was dragged by `deltaY` pixels from where it stood at `start`. Dragging down shows
 * more of the top, so the position goes down; the picture follows the finger or the mouse pixel for pixel.
 */
export function dragCoverPosition(start: number, deltaY: number, overflow: number): number {
  if (overflow <= 0) return clampCoverPosition(start)
  return clampCoverPosition(start - (deltaY / overflow) * 100)
}

/**
 * The position after a key, as the picture moves with it: up shows more of the bottom (the position grows, as on a
 * slider), down more of the top; one step, ten with Shift; Home and End go to the edges.
 */
export function keyCoverPosition(position: number, key: string, large = false): number | null {
  const step = large ? 10 : 1
  switch (key) {
    case 'ArrowUp':
      return clampCoverPosition(position + step)
    case 'ArrowDown':
      return clampCoverPosition(position - step)
    case 'Home':
      return coverPositionMin
    case 'End':
      return coverPositionMax
    default:
      return null
  }
}
