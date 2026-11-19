// Navigation of the shell (E02-05 desktop, E02-06 phone; docs/spec.md section 5, docs/ui/README.md). One place for
// every menu item and address: the left menu, the header, the bottom bar and the "Меню" screen all read it.
// Addresses are a proposal awaiting the owner's confirmation (docs/ui/README.md): the spec does not set them, so
// they live here and nowhere else.
import type { Component } from 'vue'
import {
  Bell,
  Bookmark,
  Clock,
  FileText,
  Flame,
  Menu,
  MessageCircle,
  Newspaper,
  Rss,
  Settings,
  Trophy,
  User,
} from 'lucide-vue-next'

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

/** Addresses of the header actions; on phones they move to the floating button and the "Меню" screen. */
export const writePath = '/write'
export const loginPath = '/login'
export const notificationsPath = '/notifications'
/** Address of the phone "Меню" screen, which replaces the left column below breakpoint.md. */
export const menuPath = '/menu'

const byId = (id: string): NavItem => mainNav.find((item) => item.id === id)!

/**
 * Bottom bar of the phone shell (docs/spec.md section 5): "Лента", "Рейтинг", "Уведомления", "Сообщения", "Меню".
 * "Лента" stands for all three feeds, which switch by tabs under the header on phones.
 */
export const tabBarNav: NavItem[] = [
  { id: 'feed', label: 'Лента', to: '/', icon: Rss, aliases: ['/popular', '/new', '/my'] },
  byId('rating'),
  { id: 'notifications', label: 'Уведомления', to: notificationsPath, icon: Bell },
  byId('messages'),
  { id: 'menu', label: 'Меню', to: menuPath, icon: Menu },
]

/**
 * Account items of the header's avatar menu and of the "Меню" screen. "Коллекция" and "подписка" are not shown
 * until they have an FR, donations never (docs/ui/README.md).
 */
export const accountNav: NavItem[] = [
  { id: 'profile', label: 'Профиль', to: '/profile', icon: User },
  { id: 'drafts', label: 'Черновики', to: '/drafts', icon: FileText },
  { id: 'bookmarks', label: 'Закладки', to: '/bookmarks', icon: Bookmark },
  { id: 'settings', label: 'Настройки', to: '/settings', icon: Settings },
]

/** Whether the item is the current screen: its own path, an alias, or a page below it. */
export function isActive(item: NavItem, path: string): boolean {
  const clean = path.length > 1 ? path.replace(/\/+$/, '') : path
  if (clean === item.to || item.aliases?.includes(clean)) return true
  return item.to !== '/' && clean.startsWith(`${item.to}/`)
}
