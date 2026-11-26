<script setup lang="ts">
// A blog's page (E04-02, FR-PROFILE, docs/ui/blog.md) at /u/<nickname>, or /u/<id> while the blog has no nickname;
// the tabs are child pages (/u/<blog> and /u/<blog>/comments), so they switch without a reload and keep their
// addresses. The first render happens on the server with the reader's access token, so a guest gets the whole page
// and the owner sees it as the owner; later lookups in the browser go through server/routes/bff/blog-profile.get.ts.
// The owner changes the cover here (E04-04): the picture goes through /bff/media, the cover through /bff/own-cover.
import BlogView, { type BlogTab } from '~/components/blog/BlogView.vue'
import type { CoverActions } from '~/components/blog/BlogCover.vue'
import type { MediaUpload } from '~/api/media'
import {
  blogPath,
  blogTitle,
  lookUpProfile,
  type Profile,
  type ProfileLookup,
  type ProfileSave,
} from '~/api/profiles'
import { subsitePath } from '~/api/posts'
import { t } from '../../i18n'

const route = useRoute()
const { $api } = useNuxtApp()
const segment = computed(() => String(route.params.blog))

const { data, pending, refresh } = await useAsyncData(
  () => `blog:${segment.value}`,
  (): Promise<ProfileLookup> =>
    import.meta.server
      ? lookUpProfile($api, segment.value)
      : $fetch<ProfileLookup>('/bff/blog-profile', { query: { blog: segment.value } }).catch(() => ({
          status: 'error' as const,
        })),
  { lazy: true },
)

const state = computed(() => (pending.value || !data.value ? { status: 'loading' as const } : data.value))
const tab = computed<BlogTab>(() => (route.path.endsWith('/comments') ? 'comments' : 'posts'))
const owner = computed(() => state.value.status === 'found' && state.value.owner)

if (import.meta.server) {
  const found = data.value
  // 404 for a missing blog, 500 when the API failed (E02-07, ADR-0057).
  const code = responseStatus(found)
  if (code) setResponseStatus(useRequestEvent()!, code)
  // One address per blog: an id, `@nick` or another case of the nickname leads to the blog's own address.
  // A topic lives at its own address (E11-01): /u/<topic> leads to /<topic>.
  if (found?.status === 'found' && found.profile.type === 'topic')
    await navigateTo(subsitePath(found.profile), { redirectCode: 301 })
  else if (found?.status === 'found') {
    const canonical = blogPath(found.profile)
    if (canonical !== `/u/${segment.value}`)
      await navigateTo(tab.value === 'comments' ? `${canonical}/comments` : canonical, { redirectCode: 301 })
  }
}

const failed = (): ProfileSave => ({ status: 'error' })
const coverActions: CoverActions = {
  upload(file) {
    const form = new FormData()
    form.append('file', file, file.name)
    return $fetch<MediaUpload>('/bff/media', { method: 'POST', body: form }).catch((): MediaUpload => ({
      status: 'error',
    }))
  },
  save: (input) => $fetch<ProfileSave>('/bff/own-cover', { method: 'PUT', body: input }).catch(failed),
  remove: () => $fetch<ProfileSave>('/bff/own-cover', { method: 'DELETE' }).catch(failed),
}

function onSaved(profile: Profile) {
  if (data.value?.status === 'found') data.value = { ...data.value, profile }
}

function onUnauthorized() {
  window.location.assign(`/auth/sign-in?returnTo=${encodeURIComponent(route.fullPath)}`)
}

useHead(() => ({
  title:
    state.value.status === 'found'
      ? blogTitle(state.value.profile)
      : state.value.status === 'not-found'
        ? t('blog.notFound')
        : t('blog.blog'),
}))
</script>

<template>
  <BlogView
    :state="state"
    :base="`/u/${segment}`"
    :tab="tab"
    :cover-actions="coverActions"
    @retry="refresh()"
    @saved="onSaved"
    @unauthorized="onUnauthorized"
  >
    <NuxtPage :owner="owner" />
  </BlogView>
</template>
