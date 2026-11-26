import { describe, expect, it } from 'vitest'
import {
  clampCoverPosition,
  coverOverflow,
  coverRatio,
  dragCoverPosition,
  keyCoverPosition,
} from '../../utils/cover-position'

// The cover's position (E04-04, ADR-0079): percent of the hidden height, the same on every screen.
describe('cover position', () => {
  it('keeps a whole position from 0 to 100, the middle for anything else', () => {
    expect(clampCoverPosition(33.4)).toBe(33)
    expect(clampCoverPosition(-5)).toBe(0)
    expect(clampCoverPosition(140)).toBe(100)
    expect(clampCoverPosition(Number.NaN)).toBe(50)
  })

  it('measures the hidden height of a picture taller than the header, none of a wide one', () => {
    // A square picture in a 640 × 192 header is scaled to 640 × 640: 448 px are hidden.
    expect(coverOverflow(640, 192, 1000, 1000)).toBe(448)
    expect(coverOverflow(390, 117, 1000, 1000)).toBe(273)
    expect(coverOverflow(640, 192, 4000, 500)).toBe(0)
    expect(coverOverflow(0, 0, 1000, 1000)).toBe(0)
  })

  it('shows the same band on a desktop and on a phone: the header keeps one proportion', () => {
    const desktop = coverOverflow(640, 640 / coverRatio, 1200, 900)
    const phone = coverOverflow(390, 390 / coverRatio, 1200, 900)
    // The hidden share of the picture is the same, so a position is the same band.
    expect(desktop / 640).toBeCloseTo(phone / 390)
  })

  it('follows the pointer: dragging down shows the top, up the bottom, and stops at the edges', () => {
    expect(dragCoverPosition(50, 112, 448)).toBe(25)
    expect(dragCoverPosition(50, -112, 448)).toBe(75)
    expect(dragCoverPosition(50, 1000, 448)).toBe(0)
    expect(dragCoverPosition(50, -1000, 448)).toBe(100)
    // A picture that does not overflow keeps its position.
    expect(dragCoverPosition(40, 100, 0)).toBe(40)
  })

  it('moves with the keys: one step, ten with Shift, Home and End to the edges', () => {
    expect(keyCoverPosition(50, 'ArrowUp')).toBe(51)
    expect(keyCoverPosition(50, 'ArrowDown', true)).toBe(40)
    expect(keyCoverPosition(95, 'ArrowUp', true)).toBe(100)
    expect(keyCoverPosition(50, 'Home')).toBe(0)
    expect(keyCoverPosition(50, 'End')).toBe(100)
    expect(keyCoverPosition(50, 'Enter')).toBeNull()
  })
})
