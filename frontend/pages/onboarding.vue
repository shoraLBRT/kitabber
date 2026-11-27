<script setup lang="ts">
// The onboarding after sign-up (E03-10, FR-AUTH, ADR-0102) at /onboarding?returnTo=…: the sign-in
// callback leads a new account here once, and the page leads on to `returnTo` when it is done or was done already. A
// guest signs in first. The own profile is read on the server with the person's access token, later loads and the save
// go through the site's server (server/routes/bff, ADR-0018); the topics come from the open catalogue.
import OnboardingSteps from '~/components/onboarding/OnboardingSteps.vue'
import UiButton from '~/components/ui/UiButton.vue'
import type { MediaUpload } from '~/api/media'
import { safeReturn, type OnboardingFinish, type OnboardingInput } from '~/api/onboarding'
import { ownProfile, type Profile } from '~/api/profiles'
import { loadTopicCatalog, type TopicCard } from '~/api/topics'
import { t } from '../i18n'

useHead({ title: t('onboarding.title') })

const route = useRoute()
const returnTo = safeReturn(route.query.returnTo)
const account = useAccount()
if (!account.value.signedIn) {
  await navigateTo(`/auth/sign-in?returnTo=${encodeURIComponent(route.fullPath)}`, { external: true })
}

const { $api } = useNuxtApp()
const { data, pending, refresh } = await useAsyncData('own-profile', (): Promise<Profile | null> =>
  import.meta.server ? ownProfile($api) : $fetch<Profile>('/bff/own-profile').catch(() => null),
)
// Done already (another tab, another device, an old link): nothing to show, the person goes on.
if (data.value && !data.value.onboardingPending) await navigateTo(returnTo, { replace: true })

const topics = useAsyncData(
  'onboarding-topics',
  async (): Promise<TopicCard[] | null> => {
    const lookup = await loadTopicCatalog($api, '')
    return lookup.status === 'found' ? lookup.page.items : null
  },
  { lazy: true },
)

const save = (input: OnboardingInput) =>
  $fetch<OnboardingFinish>('/bff/onboarding', { method: 'POST', body: input }).catch(
    (): OnboardingFinish => ({ status: 'error' }),
  )

function upload(avatar: Blob, fileName: string): Promise<MediaUpload> {
  const form = new FormData()
  form.append('file', avatar, fileName)
  return $fetch<MediaUpload>('/bff/media', { method: 'POST', body: form }).catch((): MediaUpload => ({
    status: 'error',
  }))
}

async function onDone() {
  // The blog, the menu's topics and the own profile are asked again with the new choices.
  clearNuxtData((key) => key === 'own-profile' || key.startsWith('blog:') || key.startsWith('topic'))
  await navigateTo(returnTo, { replace: true })
}

function onUnauthorized() {
  window.location.assign(`/auth/sign-in?returnTo=${encodeURIComponent(route.fullPath)}`)
}
</script>

<template>
  <section class="onboarding-page" aria-labelledby="onboarding-title">
    <h1 id="onboarding-title" class="onboarding-page__title">{{ t('onboarding.title') }}</h1>

    <div v-if="pending" class="onboarding-page__card" aria-busy="true" data-testid="onboarding-loading">
      <span class="visually-hidden" role="status">{{ t('onboarding.loading') }}</span>
      <div class="onboarding-page__skeleton onboarding-page__skeleton--title" />
      <div class="onboarding-page__skeleton onboarding-page__skeleton--control" />
    </div>

    <div v-else-if="!data" class="onboarding-page__card" data-testid="onboarding-error">
      <h2 class="onboarding-page__subtitle">{{ t('settings.loadFailed') }}</h2>
      <p class="onboarding-page__muted" role="alert">{{ t('settings.loadFailedText') }}</p>
      <div class="onboarding-page__actions">
        <UiButton variant="secondary" @click="refresh()">{{ t('common.retry') }}</UiButton>
        <UiButton variant="ghost" @click="navigateTo(returnTo, { replace: true })">{{
          t('onboarding.skip')
        }}</UiButton>
      </div>
    </div>

    <div v-else class="onboarding-page__card">
      <OnboardingSteps
        :profile="data"
        :topics="topics.pending.value ? undefined : (topics.data.value ?? null)"
        :save="save"
        :upload="upload"
        @done="onDone"
        @unauthorized="onUnauthorized"
        @retry-topics="topics.refresh()"
      />
    </div>
  </section>
</template>

<style scoped>
.onboarding-page {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  width: 100%;
  max-width: var(--size-measure);
  min-width: 0;
  margin: 0 auto;
}

.onboarding-page__title {
  margin: 0;
  font-size: var(--font-size-2xl);
}

.onboarding-page__subtitle {
  margin: 0;
  font-size: var(--font-size-lg);
}

.onboarding-page__card {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  padding: var(--space-6);
  border: var(--size-border) solid var(--color-border);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.onboarding-page__muted {
  margin: 0;
  color: var(--color-muted);
}

.onboarding-page__actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.onboarding-page__skeleton {
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}

.onboarding-page__skeleton--title {
  width: 60%;
  height: var(--space-6);
}

.onboarding-page__skeleton--control {
  height: var(--size-touch);
}

@media (max-width: 639px) {
  .onboarding-page__card {
    padding: var(--space-4);
  }
}
</style>
