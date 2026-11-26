// The locale of the interface (E02-10, ADR-0063), kept apart from the dictionaries so nuxt.config.ts can read it.

/** The locale every text falls back to: Russian holds every key. */
export const defaultLocale = 'ru'

/** The locale of the interface: the language of the launch market (docs/spec.md, section 1). */
export const locale: string = defaultLocale
