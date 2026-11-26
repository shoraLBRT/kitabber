// The cookie consent (E02-13, FR-UI, ADR-0066). A cookie is either necessary — the sign-in sessions and this choice
// itself — or optional, and an optional one is set only after the person allows its category. The only optional
// category is «analytics»; the site has no analytics yet, so its list of cookies is empty, and whatever brings one
// (product analytics, client error reports with cookies) starts through whenConsented('analytics', …) of
// composables/useCookieConsent.ts and names its cookies there, so a withdrawal removes them.

/** The optional categories a person can allow. */
export type ConsentCategory = 'analytics'
export const consentCategories: readonly ConsentCategory[] = ['analytics']

/** A recorded choice: which optional categories are allowed. */
export type ConsentChoice = Record<ConsentCategory, boolean>

/** The first-party cookie that remembers the choice. It is necessary: without it the banner would ask every time. */
export const consentCookieName = 'cookie_consent'
/** How long the choice is kept before the banner asks again. */
export const consentMaxAgeSeconds = 365 * 24 * 60 * 60
// A new version of the value (a new category) makes old choices unknown, so the banner asks again.
const consentVersion = 'v1'

/** Cookies the site sets without asking: the sign-in sessions and the choice itself. */
export const necessaryCookies: readonly string[] = ['auth.session', 'identity.session', consentCookieName]

export const acceptAll: ConsentChoice = { analytics: true }
export const necessaryOnly: ConsentChoice = { analytics: false }

/** The cookie value of a choice: `v1.analytics-1`. */
export function serializeConsent(choice: ConsentChoice): string {
  return [
    consentVersion,
    ...consentCategories.map((category) => `${category}-${choice[category] ? 1 : 0}`),
  ].join('.')
}

/** The choice in a cookie value, or null when there is none or it is not of the current version. */
export function parseConsent(value: string | null | undefined): ConsentChoice | null {
  if (!value) return null
  const [version, ...parts] = value.split('.')
  if (version !== consentVersion || parts.length !== consentCategories.length) return null
  const choice: ConsentChoice = { ...necessaryOnly }
  for (const [index, category] of consentCategories.entries()) {
    const part = parts[index]
    if (part === `${category}-1`) choice[category] = true
    else if (part !== `${category}-0`) return null
  }
  return choice
}

/** The value of one cookie in a `document.cookie` string. */
export function readCookie(cookies: string, name: string): string | null {
  for (const pair of cookies.split(';')) {
    const at = pair.indexOf('=')
    if (at > 0 && pair.slice(0, at).trim() === name) return pair.slice(at + 1).trim()
  }
  return null
}

/** The `document.cookie` assignment that saves a choice for the whole site. */
export function consentCookie(choice: ConsentChoice, secure: boolean): string {
  const flags = ['path=/', `max-age=${consentMaxAgeSeconds}`, 'samesite=lax']
  if (secure) flags.push('secure')
  return [`${consentCookieName}=${serializeConsent(choice)}`, ...flags].join('; ')
}

/** The `document.cookie` assignment that removes a cookie of the site. */
export function expiredCookie(name: string): string {
  return `${name}=; path=/; max-age=0; expires=Thu, 01 Jan 1970 00:00:00 GMT`
}
