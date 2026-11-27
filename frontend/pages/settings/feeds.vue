<script setup lang="ts">
// «Ленты» of the settings (E04-08, E08-13, FR-FEED, docs/ui/settings.md, ADR-0110) at /settings/feeds: the feed on the
// home page, the rating threshold of «Свежее» and the video autoplay, kept in the account. Hiding what was seen comes
// with E08-10 (#184); the sorting of «Моя лента» with #412. A guest signs in first and comes back here.
import type { FeedPreferences } from '~/api/preferences'
import { preferencesApi } from '~/api/preferences'
import type { FreshRating } from '~/api/feeds'
import FeedSettingsForm from '~/components/settings/FeedSettingsForm.vue'
import SettingsScreen from '~/components/settings/SettingsScreen.vue'
import { t } from '../../i18n'

definePageMeta({ settingsNav: true })
useHead({ title: t('settings.feeds') })

const account = useAccount()
if (!account.value.signedIn) await navigateTo('/auth/sign-in?returnTo=/settings/feeds', { external: true })

const api = preferencesApi()
// «Свежее» in this tab reads with the threshold chosen here (composables/useFreshRating.ts).
const freshRating = useState<FreshRating>('fresh:min-rating', () => null)
const freshKnown = useState('fresh:min-rating-known', () => false)
function adopt(feeds: FeedPreferences) {
  freshRating.value = feeds.freshMinRating as FreshRating
  freshKnown.value = true
}
</script>

<template>
  <SettingsScreen :title="t('settings.feeds')" testid="settings-feeds">
    <FeedSettingsForm :api="api" @saved="adopt" />
  </SettingsScreen>
</template>
