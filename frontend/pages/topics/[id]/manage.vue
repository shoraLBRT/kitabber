<script setup lang="ts">
// The management of a topic (E11-12, FR-TOPIC, docs/ui/topic.md, ADR-0121) at /topics/<id>/manage: the rules and the
// moderators for everyone; for the owner the cover, the description, the rules, the moderators and the hand-over; for
// the person the topic is offered to, «Принять тему» and «Отказаться» (the notification of the offer leads here). The
// first render happens on the server with the reader's token, later reads go through
// server/routes/bff/topic-management.get.ts; changes go through …/topic-management.post.ts and …/topic-cover.*.ts.
// States: loading, a failure with «Повторить», and the page; an unknown topic is the usual 404 page.
import TopicManagement from '~/components/topic/TopicManagement.vue'
import UiButton from '~/components/ui/UiButton.vue'
import UiState from '~/components/ui/UiState.vue'
import type { CoverActions } from '~/components/blog/BlogCover.vue'
import type { MediaUpload } from '~/api/media'
import { subsitePath } from '~/api/posts'
import { lookUpProfile, type Profile, type ProfileLookup, type ProfileSave } from '~/api/profiles'
import {
  loadTopicManagement,
  topicManagementApi,
  type ManagementLookup,
  type TopicManagement as Management,
} from '~/api/topic-management'
import { t } from '../../../i18n'

const route = useRoute()
const { $api } = useNuxtApp()
const id = computed(() => String(route.params.id))

const { data, pending, refresh } = await useAsyncData(
  () => `topic-management:${id.value}`,
  async (): Promise<{ management: ManagementLookup; profile: ProfileLookup }> => {
    if (import.meta.server)
      return {
        management: await loadTopicManagement($api, id.value),
        profile: await lookUpProfile($api, id.value),
      }
    const [management, profile] = await Promise.all([
      $fetch<ManagementLookup>('/bff/topic-management', { query: { id: id.value } }).catch(
        (): ManagementLookup => ({ status: 'error' }),
      ),
      $fetch<ProfileLookup>('/bff/blog-profile', { query: { blog: id.value } }).catch((): ProfileLookup => ({
        status: 'error',
      })),
    ])
    return { management, profile }
  },
  { lazy: true },
)

const management = computed(() =>
  data.value?.management.status === 'found' ? data.value.management.management : null,
)
const profile = computed(() =>
  data.value?.profile.status === 'found' && data.value.profile.profile.type === 'topic'
    ? data.value.profile.profile
    : null,
)
const failed = computed(() => !pending.value && data.value && !management.value)
const notFound = computed(() => data.value?.management.status === 'not-found')

if (import.meta.server && notFound.value)
  throw createError({ statusCode: 404, statusMessage: 'Not Found', fatal: true })
watchEffect(() => {
  if (import.meta.client && notFound.value)
    showError(createError({ statusCode: 404, statusMessage: 'Not Found' }))
})

const api = topicManagementApi(id.value)
const failedSave = (): ProfileSave => ({ status: 'error' })
const coverActions: CoverActions = {
  upload(file) {
    const form = new FormData()
    form.append('file', file, file.name)
    return $fetch<MediaUpload>('/bff/media', { method: 'POST', body: form }).catch((): MediaUpload => ({
      status: 'error',
    }))
  },
  save: (input) =>
    $fetch<ProfileSave>('/bff/topic-cover', { method: 'PUT', query: { id: id.value }, body: input }).catch(
      failedSave,
    ),
  remove: () =>
    $fetch<ProfileSave>('/bff/topic-cover', { method: 'DELETE', query: { id: id.value } }).catch(failedSave),
}

function onChanged(next: Management) {
  if (data.value) data.value = { ...data.value, management: { status: 'found', management: next } }
  // The topic page reads its profile again: the description or the owner may have changed.
  clearNuxtData((key) => key.startsWith('topic:'))
}

function onCover(next: Profile) {
  if (data.value?.profile.status === 'found')
    data.value = { ...data.value, profile: { ...data.value.profile, profile: next } }
  clearNuxtData((key) => key.startsWith('topic:'))
}

function onUnauthorized() {
  window.location.assign(`/auth/sign-in?returnTo=${encodeURIComponent(route.fullPath)}`)
}

const title = computed(() =>
  management.value?.role === 'owner' ? t('topicManage.title') : t('topicManage.aboutTitle'),
)
const topicLink = computed(() =>
  management.value
    ? subsitePath({ id: management.value.id, nickname: management.value.nickname, type: 'topic' })
    : null,
)
useHead(() => ({ title: management.value?.name ? `${title.value} · ${management.value.name}` : title.value }))
</script>

<template>
  <section class="topic-manage" aria-labelledby="topic-manage-title">
    <div
      v-if="!data || pending"
      class="topic-manage__card"
      aria-busy="true"
      data-testid="topic-manage-loading"
    >
      <span class="visually-hidden" role="status">{{ t('topicManage.loading') }}</span>
    </div>

    <UiState
      v-else-if="failed && !notFound"
      tone="error"
      :title="t('topicManage.loadFailed')"
      heading="h1"
      data-testid="topic-manage-error"
    >
      {{ t('topicManage.loadFailedText') }}
      <template #actions>
        <UiButton variant="secondary" @click="refresh()">{{ t('common.retry') }}</UiButton>
      </template>
    </UiState>

    <template v-else-if="management">
      <header class="topic-manage__head">
        <h1 id="topic-manage-title" class="topic-manage__title">{{ title }}</h1>
        <NuxtLink v-if="topicLink" :to="topicLink" class="topic-manage__back" data-testid="topic-manage-back"
          >{{ t('topicManage.backToTopic') }}: {{ management.name }}</NuxtLink
        >
      </header>
      <TopicManagement
        :management="management"
        :api="api"
        :cover="profile?.cover ?? null"
        :cover-actions="profile ? coverActions : undefined"
        @changed="onChanged"
        @cover="onCover"
        @unauthorized="onUnauthorized"
      />
    </template>
  </section>
</template>

<style scoped>
.topic-manage {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.topic-manage__card {
  aspect-ratio: 3 / 1;
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.topic-manage__head {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
}

.topic-manage__title {
  margin: 0;
  font-size: var(--font-size-2xl);
  font-weight: var(--font-weight-bold);
}

.topic-manage__back {
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

@media (max-width: 639px) {
  .topic-manage__head {
    padding: 0 var(--space-4);
  }
}
</style>
