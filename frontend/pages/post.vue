<script setup lang="ts">
// The post page (E07-02, FR-POST, ADR-0041) at `<blog address>/<id>-<slug>`: `/u/<blog>/<id>-<slug>` in a personal
// blog and `/<topic>/<id>-<slug>` in a topic, one page for both. The last segment must begin with a post id, so the
// page never takes the address of another page (`/u/<blog>/comments`, `/settings/blog`). The id alone finds the post;
// any other slug or blog in the address leads, with 301 on the server, to the canonical address. The first render
// happens on the server with the reader's access token, so the author sees their draft; later lookups in the browser
// go through server/routes/bff/post.get.ts.
import PostView from '~/components/post/PostView.vue'
import { lookUpPost, type PostLookup } from '~/api/posts'

definePageMeta({
  path: '/u/:blog/:post([0-9a-fA-F]{8}-[^/]*)',
  alias: ['/:blog/:post([0-9a-fA-F]{8}-[^/]*)'],
  // The action panel takes the place of the phone's floating «Написать» (E07-04).
  writeButton: false,
})

const route = useRoute()
const { $api } = useNuxtApp()
const segment = computed(() => String(route.params.post))

const { data, pending, refresh } = await useAsyncData(
  () => `post:${segment.value}`,
  (): Promise<PostLookup> =>
    import.meta.server
      ? lookUpPost($api, segment.value)
      : $fetch<PostLookup>('/bff/post', { query: { post: segment.value } }).catch(() => ({
          status: 'error' as const,
        })),
  { lazy: true },
)

const state = computed(() => (pending.value || !data.value ? { status: 'loading' as const } : data.value))

// One address per post: a wrong or old slug, a blog id instead of its nickname or another blog lead to its own one.
const canonical = (found: PostLookup | null | undefined) =>
  found?.status === 'found' && found.path !== route.path
    ? navigateTo(
        { path: found.path, query: route.query, hash: route.hash },
        { redirectCode: 301, replace: true },
      )
    : undefined

if (import.meta.server) {
  if (data.value?.status === 'not-found') setResponseStatus(useRequestEvent()!, 404)
  await canonical(data.value)
}

watch(data, (found) => canonical(found))

useHead(() => ({
  title:
    state.value.status === 'found'
      ? (state.value.post.title ?? 'Пост')
      : state.value.status === 'not-found'
        ? 'Пост не найден'
        : 'Пост',
}))
</script>

<template>
  <PostView :state="state" @retry="refresh()" />
</template>
