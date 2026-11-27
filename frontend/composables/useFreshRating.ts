// The rating filter of «Свежее» (E08-07, FR-FEED, ADR-0106): the threshold the feed is read with, and its choice.
// Signed in, the threshold is the account's setting: the site's server reads it before it renders the feed, so the first
// page comes already filtered, and a new choice is saved to the account (a failed save keeps the filter for this visit
// and says so). A guest's threshold is kept in this browser and applied once the page is in the browser.
import { ownPreferences, type Preferences } from '~/api/preferences'
import type { FreshRating } from '~/api/feeds'
import { accountFreshRating, readFreshRating, rememberFreshRating } from '~/utils/fresh-rating'
import { useToast } from './useToast'
import { t } from '../i18n'

export async function useFreshRating() {
  const account = useAccount()
  const rating = useState<FreshRating>('fresh:min-rating', () => null)
  // Whether the threshold is the remembered one; once it is, later visits in this tab keep the one in use.
  const known = useState('fresh:min-rating-known', () => false)
  const nuxtApp = useNuxtApp()
  const { $api } = nuxtApp

  // A guest's threshold lives in the browser. On a page the server rendered it is applied once hydration is over (a
  // feed read again during hydration would be given the server's page); after a navigation in the browser, at once.
  if (import.meta.client && !known.value && !account.value.signedIn) {
    const applyGuest = () => {
      if (known.value) return
      known.value = true
      rating.value = readFreshRating(window.localStorage)
    }
    if (nuxtApp.isHydrating) onNuxtReady(applyGuest)
    else applyGuest()
  }

  if (!known.value && account.value.signedIn) {
    const saved = import.meta.server
      ? await ownPreferences($api)
      : await $fetch<Preferences>('/bff/preferences').catch(() => null)
    rating.value = accountFreshRating(saved)
    known.value = true
  }

  async function choose(next: FreshRating): Promise<void> {
    rating.value = next
    known.value = true
    if (!account.value.signedIn) {
      rememberFreshRating(window.localStorage, next)
      return
    }
    const saved = await $fetch('/bff/preferences/feeds', {
      method: 'PUT',
      body: { freshMinRating: next },
    }).then(
      () => true,
      () => false,
    )
    if (!saved) useToast().show(t('feed.freshRatingNotSaved'), { tone: 'error' })
  }

  return { rating: readonly(rating), choose }
}
