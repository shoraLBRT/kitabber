<script setup lang="ts">
// «Приватность и блокировки» of the settings (E11-06, FR-SUB, docs/ui/settings.md, ADR-0091) at /settings/privacy:
// the black list with «Разблокировать». The hidden topics (E11-07) and the ban on incoming messages (FR-MSG) come with
// their own issues. The list goes through the site's server (server/routes/bff/blocks*.ts), which holds the access
// token; the names come from the public profiles. A guest signs in first and comes back here.
import BlockList from '~/components/settings/BlockList.vue'
import SettingsScreen from '~/components/settings/SettingsScreen.vue'
import { blocksApi } from '~/api/blocks'
import { loadAuthors } from '~/api/comments'
import { t } from '../../i18n'

definePageMeta({ settingsNav: true })
useHead({ title: t('settings.privacy') })

const account = useAccount()
if (!account.value.signedIn) await navigateTo('/auth/sign-in?returnTo=/settings/privacy', { external: true })

const { $api } = useNuxtApp()
const api = blocksApi()
const profiles = (ids: readonly string[]) => loadAuthors($api, ids)
</script>

<template>
  <SettingsScreen :title="t('settings.privacy')" testid="settings-privacy">
    <BlockList :api="api" :profiles="profiles" />
  </SettingsScreen>
</template>
