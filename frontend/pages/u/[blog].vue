<script setup lang="ts">
// A blog's page (E04-02, FR-PROFILE, docs/ui/blog.md) at /u/<nickname>, or /u/<id> while the blog has no nickname;
// the tabs are child pages (/u/<blog> and /u/<blog>/comments), so they switch without a reload and keep their
// addresses. The first render happens on the server with the reader's access token, so a guest gets the whole page
// and the owner sees it as the owner; later lookups in the browser go through server/routes/bff/blog-profile.get.ts.
import BlogView, { type BlogTab } from '~/components/blog/BlogView.vue'
import { blogPath, blogTitle, lookUpProfile, type ProfileLookup } from '~/api/profiles'

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
  if (found?.status === 'not-found') setResponseStatus(useRequestEvent()!, 404)
  // One address per blog: an id, `@nick` or another case of the nickname leads to the blog's own address.
  if (found?.status === 'found') {
    const canonical = blogPath(found.profile)
    if (canonical !== `/u/${segment.value}`)
      await navigateTo(tab.value === 'comments' ? `${canonical}/comments` : canonical, { redirectCode: 301 })
  }
}

useHead(() => ({
  title:
    state.value.status === 'found'
      ? blogTitle(state.value.profile)
      : state.value.status === 'not-found'
        ? 'Блог не найден'
        : 'Блог',
}))
</script>

<template>
  <BlogView :state="state" :base="`/u/${segment}`" :tab="tab" @retry="refresh()">
    <NuxtPage :owner="owner" />
  </BlogView>
</template>
