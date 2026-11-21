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

/** One width of a responsive picture (ADR-0040): a link and the width in pixels it asks for, a srcset candidate. */
export interface ImageSource {
  url: string
  width: number
}
