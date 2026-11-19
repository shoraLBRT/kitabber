// Navigation of the desktop shell (E02-05, docs/spec.md section 5, docs/ui/README.md). One list, so the mobile
// "Меню" screen and bottom bar (#16) can reuse it. Addresses are a proposal awaiting the owner's confirmation
// (docs/ui/feed.md): the spec does not set them, so they live here and nowhere else.
import type { Component } from 'vue'
import { Clock, Flame, MessageCircle, Newspaper, Trophy } from 'lucide-vue-next'

export interface NavItem {
  id: string
  label: string
  to: string
  icon?: Component
  /** Other paths that show the same screen, e.g. "/" is the default feed. */
  aliases?: string[]
}

/** Main menu of the left column, in the order of docs/spec.md section 5. */
export const mainNav: NavItem[] = [
  { id: 'popular', label: 'Популярное', to: '/popular', icon: Flame, aliases: ['/'] },
  { id: 'new', label: 'Свежее', to: '/new', icon: Clock },
  { id: 'my', label: 'Моя лента', to: '/my', icon: Newspaper },
  { id: 'messages', label: 'Сообщения', to: '/messages', icon: MessageCircle },
  { id: 'rating', label: 'Рейтинг', to: '/rating', icon: Trophy },
]

/** Service links at the bottom of the left column. */
export const serviceNav: NavItem[] = [
  { id: 'about', label: 'О проекте', to: '/about' },
  { id: 'rules', label: 'Правила', to: '/rules' },
]

/** Address of the full topic list ("Показать все"). */
export const topicsPath = '/topics'

/** Whether the item is the current screen: its own path, an alias, or a page below it. */
export function isActive(item: NavItem, path: string): boolean {
  const clean = path.length > 1 ? path.replace(/\/+$/, '') : path
  if (clean === item.to || item.aliases?.includes(clean)) return true
  return item.to !== '/' && clean.startsWith(`${item.to}/`)
}
