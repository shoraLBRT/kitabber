import { describe, expect, it } from 'vitest'
import { clampCrop, cropScale, cropSource, maxZoom, minZoom } from '../../utils/avatar-crop'

// The avatar crop (E04-03, ADR-0034): the picture always covers the square frame, and the square under the frame is
// the part of the original that is uploaded.
const landscape = { width: 1000, height: 500 }
const frame = 250

describe('avatar crop', () => {
  it('fits the shorter side of the picture to the frame at zoom 1', () => {
    expect(cropScale(landscape, frame, 1)).toBe(0.5)
    expect(cropScale(landscape, frame, 2)).toBe(1)
  })

  it('takes the centre square of the picture before it is moved', () => {
    expect(cropSource({ zoom: 1, x: 0, y: 0 }, landscape, frame)).toEqual({ x: 250, y: 0, size: 500 })
  })

  it('keeps the picture over the whole frame and the zoom in its range', () => {
    // At zoom 1 the landscape picture is 500×250 on screen: it moves 125 px sideways and not at all up or down.
    expect(clampCrop({ zoom: 1, x: 400, y: -90 }, landscape, frame)).toEqual({ zoom: 1, x: 125, y: 0 })
    expect(clampCrop({ zoom: 0.2, x: 0, y: 0 }, landscape, frame).zoom).toBe(minZoom)
    expect(clampCrop({ zoom: 99, x: 0, y: 0 }, landscape, frame).zoom).toBe(maxZoom)
  })

  it('crops the part under the frame after a move and a zoom', () => {
    // Moved fully right, the left edge of the picture is under the frame.
    expect(cropSource({ zoom: 1, x: 125, y: 0 }, landscape, frame)).toEqual({ x: 0, y: 0, size: 500 })
    // At zoom 2 the frame shows 250 px of the original; moved 50 screen px down, the square goes 50 px up.
    expect(cropSource({ zoom: 2, x: 0, y: 50 }, landscape, frame)).toEqual({ x: 375, y: 75, size: 250 })
  })
})
