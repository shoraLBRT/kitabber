// Shared prop types of the UI components (E02-04, ADR-0013).
import type { Component } from 'vue'

export interface MenuItem {
  id: string
  label: string
  icon?: Component
  danger?: boolean
  disabled?: boolean
}

export interface TabItem {
  id: string
  label: string
  disabled?: boolean
}

/**
 * What stands in for a picture until it arrives (E06-03, ADR-0067): the dominant colour and a tiny version of the whole
 * picture as a data: link, computed by the server at upload.
 */
export interface ImagePreview {
  color: string
  image: string
}

/** One width of a responsive picture (ADR-0040): a link and the width in pixels it asks for, a srcset candidate. */
export interface ImageSource {
  url: string
  width: number
}
