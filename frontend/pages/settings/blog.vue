<script setup lang="ts">
// The blog settings (E04-03, FR-PROFILE, docs/ui/settings.md) at /settings/blog: the avatar, the name and the
// description of the person's own blog. The nickname (E04-05) and the other switches of the section come with their
// own issues. The first render happens on the server with the person's access token; later loads, the save and the
// avatar upload go through the site's server (server/routes/bff/own-profile.*.ts, media.post.ts, ADR-0018). A guest
// signs in first and comes back here.
import BlogSettingsForm from '~/components/settings/BlogSettingsForm.vue'
import UiButton from '~/components/ui/UiButton.vue'
import type { MediaUpload } from '~/api/media'
import { blogPath, ownProfile, type Profile, type ProfileInput, type ProfileSave } from '~/api/profiles'
import { t } from '../../i18n'

const signIn = '/auth/sign-in?returnTo=/settings/blog'
useHead({ title: t('settings.blog') })

const account = useAccount()
if (!account.value.signedIn) await navigateTo(signIn, { external: true })

const { $api } = useNuxtApp()
const { data, pending, refresh } = await useAsyncData(
  'own-profile',
  (): Promise<Profile | null> =>
    import.meta.server ? ownProfile($api) : $fetch<Profile>('/bff/own-profile').catch(() => null),
  { lazy: true },
)

const save = (input: ProfileInput) =>
  $fetch<ProfileSave>('/bff/own-profile', { method: 'PUT', body: input }).catch((): ProfileSave => ({
    status: 'error',
  }))

function upload(avatar: Blob, fileName: string): Promise<MediaUpload> {
  const form = new FormData()
  form.append('file', avatar, fileName)
  return $fetch<MediaUpload>('/bff/media', { method: 'POST', body: form }).catch((): MediaUpload => ({
    status: 'error',
  }))
}

function onSaved(profile: Profile) {
  data.value = profile
  // The blog page asks again when it opens next, so the change is there at once.
  clearNuxtData((key) => key.startsWith('blog:'))
}

function onUnauthorized() {
  window.location.assign(signIn)
}
</script>

<template>
  <section class="settings" aria-labelledby="settings-title">
    <header class="settings__header">
      <NuxtLink
        v-if="data"
        :to="blogPath(data)"
        class="settings__back"
        :aria-label="t('settings.backToBlog')"
        data-testid="settings-back"
      >
        ←
      </NuxtLink>
      <h1 id="settings-title" class="settings__title">{{ t('settings.blog') }}</h1>
    </header>

    <div v-if="pending" class="settings__card" aria-busy="true" data-testid="settings-loading">
      <span class="visually-hidden" role="status">{{ t('settings.loading') }}</span>
      <div class="settings__skeleton settings__skeleton--avatar" />
      <div v-for="n in 2" :key="n" class="settings__skeleton-field">
        <div class="settings__skeleton settings__skeleton--label" />
        <div class="settings__skeleton settings__skeleton--control" />
      </div>
    </div>

    <div v-else-if="!data" class="settings__card settings__message" data-testid="settings-error">
      <h2 class="settings__section-title">{{ t('settings.loadFailed') }}</h2>
      <p class="settings__muted" role="alert">{{ t('settings.loadFailedText') }}</p>
      <UiButton variant="secondary" @click="refresh()">{{ t('common.retry') }}</UiButton>
    </div>

    <div v-else class="settings__card">
      <h2 class="settings__section-title">{{ t('settings.blogSection') }}</h2>
      <BlogSettingsForm
        :key="data.id"
        :profile="data"
        :save="save"
        :upload="upload"
        @saved="onSaved"
        @unauthorized="onUnauthorized"
      />
    </div>
  </section>
</template>

<style scoped>
.settings {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.settings__header {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}

.settings__back {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: var(--size-touch);
  min-height: var(--size-touch);
  color: var(--color-text);
  font-size: var(--font-size-xl);
  text-decoration: none;
}

.settings__title {
  margin: 0;
  font-size: var(--font-size-2xl);
  font-weight: var(--font-weight-bold);
}

.settings__card {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
  padding: var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.settings__message {
  align-items: flex-start;
  gap: var(--space-2);
}

.settings__section-title {
  margin: 0;
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-bold);
}

.settings__muted {
  margin: 0;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.settings__skeleton {
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}

.settings__skeleton--avatar {
  width: var(--space-16);
  height: var(--space-16);
  border-radius: var(--radius-full);
}

.settings__skeleton-field {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.settings__skeleton--label {
  width: 30%;
  height: var(--space-4);
}

.settings__skeleton--control {
  height: var(--size-touch);
}

@media (max-width: 639px) {
  .settings__card {
    padding: var(--space-4);
  }
}
</style>
