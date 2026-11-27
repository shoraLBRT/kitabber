// Signing in with an external provider (#91, FR-AUTH, ADR-0115): Yandex, VK, Google, Telegram. The list names the
// providers the API's settings turn on; a provider without its keys is not there, so its button is not shown. A
// button is a plain link: the browser goes to the API, the API sends it to the provider, and back on the site it
// lands on `returnUrl` with the provider's session set, or with `externalError` when it failed.
import type { ApiClient, components } from './client'
import { t } from '../i18n'

export type ExternalProvider = components['schemas']['ExternalProviderView']['key']

export interface ExternalSignInApi {
  /** The providers turned on; `undefined` when the list could not be loaded. */
  providers(): Promise<ExternalProvider[] | undefined>
  /** Where a button sends the browser: sign in (or, with `link`, link to the signed-in account) and go to `returnUrl`. */
  startUrl(provider: ExternalProvider, returnUrl: string, link?: boolean): string
}

/** The calls over the typed API client; `apiBase` is the API's origin for the browser ('' for the same origin). */
export function externalSignInApi(client: ApiClient, apiBase: string): ExternalSignInApi {
  return {
    providers: async () => {
      try {
        const { data } = await client.GET('/api/v1/sign-in/external', { credentials: 'include' })
        return data?.map((p) => p.key)
      } catch {
        return undefined
      }
    },
    startUrl: (provider, returnUrl, link = false) =>
      `${apiBase}/api/v1/sign-in/external/${encodeURIComponent(provider)}?returnUrl=${encodeURIComponent(returnUrl)}` +
      (link ? '&link=true' : ''),
  }
}

const known = ['yandex', 'vk', 'google', 'telegram'] as const

/** The provider's name as people know it; the key itself for a provider the site does not know yet. */
export function providerName(provider: string): string {
  return (known as readonly string[]).includes(provider)
    ? t(`auth.external.providers.${provider as (typeof known)[number]}`)
    : provider
}

const errors = [
  'expired',
  'denied',
  'failed',
  'email_required',
  'taken_by_another_account',
  'provider_already_linked',
  'session_required',
] as const

/** The message of the API's `externalError`; `undefined` when there is none. */
export function externalErrorMessage(code: unknown): string | undefined {
  if (typeof code !== 'string' || code === '') return undefined
  const known = (errors as readonly string[]).includes(code) ? (code as (typeof errors)[number]) : 'failed'
  return t(`auth.external.errors.${known}`)
}

/**
 * The query the API's Telegram callback expects, from where Telegram Login returned to (/auth/telegram): the signed
 * fields of `#tgAuthResult=` (base64 of JSON), or the query of the widget's redirect; `undefined` when neither is
 * there, as when the person declined.
 */
export function telegramCallbackQuery(hash: string, search: string): string | undefined {
  const fromHash = /(?:^#|&)tgAuthResult=([^&]+)/.exec(hash)?.[1]
  if (fromHash) {
    try {
      const base64 = decodeURIComponent(fromHash).replace(/-/g, '+').replace(/_/g, '/')
      const json = new TextDecoder().decode(Uint8Array.from(atob(base64), (c) => c.charCodeAt(0)))
      const data: unknown = JSON.parse(json)
      if (!data || typeof data !== 'object') return undefined
      const query = new URLSearchParams()
      for (const [key, value] of Object.entries(data)) {
        if (typeof value === 'string' || typeof value === 'number') query.set(key, String(value))
      }
      return query.has('hash') ? query.toString() : undefined
    } catch {
      return undefined
    }
  }
  const query = new URLSearchParams(search)
  return query.has('hash') && query.has('id') ? query.toString() : undefined
}
