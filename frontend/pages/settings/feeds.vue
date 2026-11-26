<script setup lang="ts">
// «Ленты» of the settings (E04-08, FR-FEED, docs/ui/settings.md) at /settings/feeds. Only the section's place for now:
// the feed on the home page, the sorting and the video autoplay come with E08-13 (#102), hiding what was seen with
// E08-10 (#184). A guest signs in first and comes back here.
import SettingsScreen from '~/components/settings/SettingsScreen.vue'
import { t } from '../../i18n'

definePageMeta({ settingsNav: true })
useHead({ title: t('settings.feeds') })

const account = useAccount()
if (!account.value.signedIn) await navigateTo('/auth/sign-in?returnTo=/settings/feeds', { external: true })
</script>

<template>
  <SettingsScreen :title="t('settings.feeds')" testid="settings-feeds">
    <p class="settings-coming">{{ t('settings.feedsComing') }}</p>
  </SettingsScreen>
</template>

<style scoped>
.settings-coming {
  margin: 0;
  color: var(--color-muted);
}
</style>
