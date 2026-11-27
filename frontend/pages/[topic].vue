<script setup lang="ts">
// The topic page (E11-01, FR-TOPIC, docs/ui/topic.md, ADR-0082) at /<topic>: the topic's address is its nickname, or its
// id while it has none. Static pages (/new, /write, /settings…) win over this one, and a post's address in a topic
// (/<topic>/<id>-<slug>) is post.vue's. The first render happens on the server, so a guest and a search engine get the
// header and the first posts; later lookups in the browser go through server/routes/bff/blog-profile.get.ts. An address
// that names no topic is the usual 404 page; a personal blog's nickname leads to the blog at /u/<nickname>. The tabs are
// child pages (/<topic> and /<topic>/comments, E09-15), so they switch without a reload and keep their addresses.
import TopicView, { type TopicTab } from '~/components/topic/TopicView.vue'
import { blogPath, blogTitle, lookUpProfile, type ProfileLookup } from '~/api/profiles'
import { subsitePath } from '~/api/posts'
import { t } from '../i18n'

const route = useRoute()
const { $api } = useNuxtApp()
const segment = computed(() => String(route.params.topic))
const tab = computed<TopicTab>(() => (route.path.endsWith('/comments') ? 'comments' : 'posts'))

const { data, pending, refresh } = await useAsyncData(
  () => `topic:${segment.value}`,
  (): Promise<ProfileLookup> =>
    import.meta.server
      ? lookUpProfile($api, segment.value)
      : $fetch<ProfileLookup>('/bff/blog-profile', { query: { blog: segment.value } }).catch(() => ({
          status: 'error' as const,
        })),
  { lazy: true },
)

const state = computed(() => {
  const found = data.value
  if (pending.value || !found) return { status: 'loading' as const }
  if (found.status === 'found' && found.profile.type === 'topic')
    return { status: 'found' as const, profile: found.profile }
  return { status: 'error' as const }
})

// The subscriber count and «Подписаться» (E11-03, ADR-0085).
const subscription = useSubscription(() => (state.value.status === 'found' ? state.value.profile : null))

const notFound = computed(() => data.value?.status === 'not-found')

if (import.meta.server) {
  const found = data.value
  // No topic at this address: the usual error page with 404 (E02-07, error.vue).
  if (found?.status === 'not-found')
    throw createError({ statusCode: 404, statusMessage: 'Not Found', fatal: true })
  if (found?.status === 'error') setResponseStatus(useRequestEvent()!, 500)
  if (found?.status === 'found') {
    // One address per topic: an id or another case of the nickname leads to the topic's own address, and a personal
    // blog's nickname to the blog.
    const canonical = found.profile.type === 'topic' ? subsitePath(found.profile) : blogPath(found.profile)
    if (canonical !== `/${segment.value}`)
      await navigateTo(tab.value === 'comments' ? `${canonical}/comments` : canonical, { redirectCode: 301 })
  }
}

// The same after a move in the browser.
watchEffect(() => {
  if (import.meta.client && notFound.value)
    showError(createError({ statusCode: 404, statusMessage: 'Not Found' }))
})

// A personal blog found after a move in the browser lives at its own address.
watchEffect(() => {
  const found = data.value
  if (import.meta.client && found?.status === 'found' && found.profile.type !== 'topic')
    void navigateTo(blogPath(found.profile), { replace: true })
})

useHead(() => ({
  title: state.value.status === 'found' ? blogTitle(state.value.profile) : t('topic.topic'),
}))
</script>

<template>
  <TopicView
    :state="state"
    :subscription="{ state: subscription.state.value, busy: subscription.busy.value }"
    :base="`/${segment}`"
    :tab="tab"
    @retry="refresh()"
    @subscribe="subscription.toggle()"
  >
    <template #default="{ profile }">
      <NuxtPage :topic-id="profile.id" />
    </template>
  </TopicView>
</template>
