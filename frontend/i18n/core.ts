// The interface strings (E02-10, FR-UI, ADR-0063): every text the person reads comes from a dictionary of the locale,
// i18n/locales/<locale>.ts. A text is a string with `{name}` slots, or the plural forms of Intl.PluralRules
// ({ one, few, many, other }) picked by the `count` parameter. A locale's dictionary may leave texts out: those come
// from the default locale's one. Dates and numbers are formatted by Intl for the same locale.

/** The forms of a text that depends on a number; `other` is required, the rest are the locale's own categories. */
export type PluralForms = Partial<Record<Intl.LDMLPluralRule, string>> & { other: string }

/** A dictionary: groups of texts, each a string or plural forms. */
export interface Dictionary {
  [key: string]: string | PluralForms | Dictionary
}

/** A dictionary of another locale: any text may be left out and is then taken from the default locale. */
export type PartialDictionary<T> = {
  [K in keyof T]?: T[K] extends string
    ? string
    : T[K] extends PluralForms
      ? PluralForms
      : PartialDictionary<T[K]>
}

/** The dotted keys of every text in a dictionary: `comments.sort.popular`. */
export type MessageKey<T> = {
  [K in keyof T & string]: T[K] extends string | PluralForms ? K : `${K}.${MessageKey<T[K]>}`
}[keyof T & string]

/** The values of a text's `{name}` slots; `count` also picks the plural form. */
export type MessageParams = Record<string, string | number>

export interface I18n<T extends Dictionary> {
  /** The locale in use, a BCP 47 tag: the `lang` of the page and the locale of Intl. */
  readonly locale: string
  /** The text of the key with its slots filled; a key missing everywhere is returned as it is, never thrown. */
  t(key: MessageKey<T>, params?: MessageParams): string
  /** A number in the locale's digits and separators. */
  formatNumber(value: number, options?: Intl.NumberFormatOptions): string
  /** A date and time in the locale's words and order. */
  formatDate(value: Date | string | number, options?: Intl.DateTimeFormatOptions): string
}

function isPlural(value: unknown): value is PluralForms {
  return typeof value === 'object' && value !== null && typeof (value as PluralForms).other === 'string'
}

function lookup(dictionary: Dictionary | undefined, key: string): string | PluralForms | undefined {
  let node: unknown = dictionary
  for (const part of key.split('.')) {
    if (typeof node !== 'object' || node === null || isPlural(node)) return undefined
    node = (node as Dictionary)[part]
  }
  return typeof node === 'string' || isPlural(node) ? node : undefined
}

/**
 * The strings of `locale`, falling back to the dictionary of `fallbackLocale`. `globals` are slots every text may use
 * without passing them — the product name, kept in brand/brand.json alone.
 */
export function createI18n<T extends Dictionary>(options: {
  locale: string
  fallbackLocale: string
  dictionaries: Readonly<Record<string, Dictionary | PartialDictionary<T>>>
  globals?: MessageParams
}): I18n<T> {
  const { locale, fallbackLocale, dictionaries, globals = {} } = options
  const own = dictionaries[locale] as Dictionary | undefined
  const fallback = dictionaries[fallbackLocale] as Dictionary | undefined
  const pluralRules = new Intl.PluralRules(locale)
  const numbers = new Intl.NumberFormat(locale)
  const cache = new Map<string, Intl.DateTimeFormat>()

  const formatNumber = (value: number, numberOptions?: Intl.NumberFormatOptions) =>
    numberOptions ? new Intl.NumberFormat(locale, numberOptions).format(value) : numbers.format(value)

  return {
    locale,
    t(key, params = {}) {
      const message = lookup(own, key) ?? lookup(fallback, key)
      if (message === undefined) return key
      const count = params.count
      const text = isPlural(message)
        ? ((typeof count === 'number' ? message[pluralRules.select(count)] : undefined) ?? message.other)
        : message
      const values: MessageParams = { ...globals, ...params }
      return text.replace(/\{(\w+)\}/g, (slot, name: string) => {
        const value = values[name]
        if (value === undefined) return slot
        return typeof value === 'number' ? formatNumber(value) : value
      })
    },
    formatNumber,
    formatDate(value, dateOptions = {}) {
      const id = JSON.stringify(dateOptions)
      let format = cache.get(id)
      if (!format) {
        format = new Intl.DateTimeFormat(locale, dateOptions)
        cache.set(id, format)
      }
      return format.format(value instanceof Date ? value : new Date(value))
    },
  }
}
