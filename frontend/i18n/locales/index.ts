// The dictionaries by locale (E02-10, ADR-0063). A new language is its file next to ru.ts and its line here; the
// server routes share these modules, so the list is written out rather than globbed (Nitro has no import.meta.glob).
// tests/i18n/i18n.spec.ts fails when a file of this folder is missing from the list.
import ru from './ru'

export default { ru }
