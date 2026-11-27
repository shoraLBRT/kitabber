// «Поделиться» of a post (E07-07, FR-POST, docs/ui/post.md, ADR-0103): the canonical link of the post, the phone's
// system share menu (the Web Share API) and a list on a wide screen — «Скопировать ссылку» and the social networks
// as plain share addresses. Nothing here loads code from another site: a network's address only opens in a new tab.
import { ExternalLink, Link } from 'lucide-vue-next'
import type { MenuItem } from '../types/ui'
import { phoneQuery } from '../composables/useMediaQuery'
import { t } from '../i18n'

/** The networks of the list, in its order; each one is a share address that takes the link and the title. */
export const shareNetworks = ['telegram', 'vk', 'x'] as const
export type ShareNetwork = (typeof shareNetworks)[number]

export type ShareItemId = 'copy-link' | ShareNetwork

/**
 * The full canonical link of an address on this site: the origin and the path, without the query and the hash, so
 * what is shared is the post's own address and not how this reader came to it.
 */
export function canonicalUrl(path: string, origin: string): string {
  const url = new URL(path, origin)
  return `${url.origin}${url.pathname}`
}

/** The share address of a network for the link `url` with the post's title (empty for a short post without one). */
export function networkShareUrl(network: ShareNetwork, url: string, title: string): string {
  const query = (params: Record<string, string>) =>
    new URLSearchParams(Object.entries(params).filter(([, value]) => value !== '')).toString()
  switch (network) {
    case 'telegram':
      return `https://t.me/share/url?${query({ url, text: title })}`
    case 'vk':
      return `https://vk.com/share.php?${query({ url, title })}`
    case 'x':
      return `https://x.com/intent/post?${query({ url, text: title })}`
  }
}

const networkLabels: Record<ShareNetwork, () => string> = {
  telegram: () => t('share.telegram'),
  vk: () => t('share.vk'),
  x: () => t('share.x'),
}

/** The items of the list: copying the link first, then the networks. */
export function shareItems(): (MenuItem & { id: ShareItemId })[] {
  return [
    { id: 'copy-link', label: t('actions.copyLink'), icon: Link },
    ...shareNetworks.map((network) => ({ id: network, label: networkLabels[network](), icon: ExternalLink })),
  ]
}

/** Whether «Поделиться» opens the system menu: a phone-wide screen whose browser has the Web Share API. */
export function usesSystemShare(win: Window = window): boolean {
  return (
    typeof win.navigator.share === 'function' &&
    typeof win.matchMedia === 'function' &&
    win.matchMedia(phoneQuery).matches
  )
}
