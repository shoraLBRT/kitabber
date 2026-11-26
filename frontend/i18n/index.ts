// The interface strings of the app (E02-10, ADR-0063). Each locale has its dictionary in i18n/locales/, named by its
// BCP 47 tag (ru.ts, en.ts) and listed in i18n/locales/index.ts; the locale in use is set in i18n/config.ts. Russian
// is the default and the full dictionary; the others may leave texts out and fall back to it.
import brand from '../../brand/brand.json'
import { defaultLocale, locale } from './config'
import { createI18n, type MessageKey, type PartialDictionary } from './core'
import locales from './locales'
import type ru from './locales/ru'

export type Messages = typeof ru
export type { MessageKey, MessageParams } from './core'

/** A dictionary of another locale, checked against the Russian one: `export default {…} satisfies LocaleMessages`. */
export type LocaleMessages = PartialDictionary<Messages>

/** The dictionaries of i18n/locales/, by locale: `{ ru: {…} }`. */
export const dictionaries: Record<string, LocaleMessages> = locales

export { defaultLocale, locale }

export const i18n = createI18n<Messages>({
  locale,
  fallbackLocale: defaultLocale,
  dictionaries,
  globals: { brand: brand.name },
})

/** The text of a key: `t('comments.reply')`, `t('comments.replies', { count: 3 })`. */
export const t = (key: MessageKey<Messages>, params?: Record<string, string | number>) => i18n.t(key, params)
export const formatNumber = i18n.formatNumber
export const formatDate = i18n.formatDate
