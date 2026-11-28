<script setup lang="ts">
// «Приватность и блокировки» of the settings (E11-06, E11-07, FR-SUB, docs/ui/settings.md, ADR-0091, ADR-0119) at
// /settings/privacy: the black list with «Разблокировать», then the hidden topics and posts with «Вернуть в ленты».
// The ban on incoming messages (FR-MSG) comes with its own issue. The lists go through the site's server
// (server/routes/bff/blocks*.ts, hidden*.ts), which holds the access token; the names of the blocked come from the
// public profiles. A guest signs in first and comes back here.
import BlockList from '~/components/settings/BlockList.vue'
import HiddenList from '~/components/settings/HiddenList.vue'
import SettingsScreen from '~/components/settings/SettingsScreen.vue'
import { blocksApi } from '~/api/blocks'
import { hiddenApi } from '~/api/hidden'
import { loadAuthors } from '~/api/comments'
import { t } from '../../i18n'

definePageMeta({ settingsNav: true })
useHead({ title: t('settings.privacy') })

const account = useAccount()
if (!account.value.signedIn) await navigateTo('/auth/sign-in?returnTo=/settings/privacy', { external: true })

const { $api } = useNuxtApp()
const api = blocksApi()
const hidden = hiddenApi()
const profiles = (ids: readonly string[]) => loadAuthors($api, ids)
</script>

<template>
  <SettingsScreen :title="t('settings.privacy')" testid="settings-privacy">
    <BlockList :api="api" :profiles="profiles" />
    <HiddenList :api="hidden" />
  </SettingsScreen>
</template>
