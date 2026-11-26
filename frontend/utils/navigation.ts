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
import { t } from '../i18n'

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
  { id: 'popular', label: t('nav.popular'), to: '/popular', icon: Flame, aliases: ['/'] },
  { id: 'new', label: t('nav.new'), to: '/new', icon: Clock },
  { id: 'my', label: t('nav.my'), to: '/my', icon: Newspaper },
  { id: 'messages', label: t('nav.messages'), to: '/messages', icon: MessageCircle },
  { id: 'rating', label: t('nav.rating'), to: '/rating', icon: Trophy },
]

/** The cookie choice (E02-13, ADR-0066): open to guests, so it is a service link and not an account item. */
export const cookieSettingsPath = '/settings/cookies'

/** Service links at the bottom of the left column. */
export const serviceNav: NavItem[] = [
  { id: 'about', label: t('nav.about'), to: '/about' },
  { id: 'rules', label: t('nav.rules'), to: '/rules' },
  { id: 'cookies', label: t('nav.cookies'), to: cookieSettingsPath },
]

/** Address of the full topic list ("Показать все"). */
export const topicsPath = '/topics'

/** Addresses of the header actions; on phones they move to the floating button and the "Меню" screen. */
export const writePath = '/write'
// Sign-in starts on the Nuxt server (E18-02): a server route, not a page, so links to it leave the app router.
export const loginPath = '/auth/sign-in'
export const notificationsPath = '/notifications'
/** Address of the phone "Меню" screen, which replaces the left column below breakpoint.md. */
export const menuPath = '/menu'

const byId = (id: string): NavItem => mainNav.find((item) => item.id === id)!

/**
 * Bottom bar of the phone shell (docs/spec.md section 5): "Лента", "Рейтинг", "Уведомления", "Сообщения", "Меню".
 * "Лента" stands for all three feeds, which switch by tabs under the header on phones.
 */
export const tabBarNav: NavItem[] = [
  { id: 'feed', label: t('nav.feed'), to: '/', icon: Rss, aliases: ['/popular', '/new', '/my'] },
  byId('rating'),
  { id: 'notifications', label: t('nav.notifications'), to: notificationsPath, icon: Bell },
  byId('messages'),
  { id: 'menu', label: t('nav.menu'), to: menuPath, icon: Menu },
]

/**
 * Account items of the header's avatar menu and of the "Меню" screen. "Коллекция" and "подписка" are not shown
 * until they have an FR, donations never (docs/ui/README.md).
 */
export const accountNav: NavItem[] = [
  { id: 'profile', label: t('nav.profile'), to: '/profile', icon: User },
  { id: 'drafts', label: t('nav.drafts'), to: '/drafts', icon: FileText },
  { id: 'bookmarks', label: t('nav.bookmarks'), to: '/bookmarks', icon: Bookmark },
  { id: 'settings', label: t('nav.settings'), to: '/settings', icon: Settings },
]

/** Whether the item is the current screen: its own path, an alias, or a page below it. */
export function isActive(item: NavItem, path: string): boolean {
  const clean = path.length > 1 ? path.replace(/\/+$/, '') : path
  if (clean === item.to || item.aliases?.includes(clean)) return true
  return item.to !== '/' && clean.startsWith(`${item.to}/`)
}
