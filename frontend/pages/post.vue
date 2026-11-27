<script setup lang="ts">
// The post page (E07-02, FR-POST, ADR-0041) at `<blog address>/<id>-<slug>`: `/u/<blog>/<id>-<slug>` in a personal
// blog and `/<topic>/<id>-<slug>` in a topic, one page for both. The last segment must begin with a post id, so the
// page never takes the address of another page (`/u/<blog>/comments`, `/settings/blog`). The id alone finds the post;
// any other slug or blog in the address leads, with 301 on the server, to the canonical address. The first render
// happens on the server with the reader's access token, so the author sees their draft; later lookups in the browser
// go through server/routes/bff/post.get.ts.
import PostView from '~/components/post/PostView.vue'
import { lookUpPost, subsitePath, type PostLookup } from '~/api/posts'
import { t } from '../i18n'

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
      ? lookUpPost($api, segment.value, useRequestEvent()?.context.auth?.accountId ?? null)
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
  // 404 for a missing post, 500 when the API failed (E02-07, ADR-0057).
  const code = responseStatus(data.value)
  if (code) setResponseStatus(useRequestEvent()!, code)
  await canonical(data.value)
}

watch(data, (found) => canonical(found))

// The «…» menu (E07-05, ADR-0053): its flagged items, and deleting the author's post through the site's server
// (server/routes/bff/post.delete.ts); a deleted post leaves for its blog with a toast.
// The likes of the published post and of its comments (E10-01, ADR-0061), read in the browser once it is shown.
const likes = useLikes(() =>
  state.value.status === 'found' && state.value.post.status === 'published' ? state.value.post.id : null,
)

// The view counter (E07-06, ADR-0071): the opening of a reader's published post and, with consent, its reading time.
usePostViews(() =>
  state.value.status === 'found' && state.value.post.status === 'published' && !state.value.own
    ? state.value.post.id
    : null,
)

const features = useRuntimeConfig().public.features
const toast = useToast()
const account = useAccount()

async function remove(): Promise<boolean> {
  const found = data.value
  if (found?.status !== 'found') return false
  try {
    await $fetch('/bff/post', { method: 'DELETE', query: { id: found.post.id } })
  } catch {
    return false
  }
  toast.show(t('post.deleted'), { tone: 'success' })
  await navigateTo(subsitePath(found.blog), { replace: true })
  return true
}

useHead(() => ({
  title:
    state.value.status === 'found'
      ? (state.value.post.title ?? t('post.post'))
      : state.value.status === 'not-found'
        ? t('post.notFound')
        : t('post.post'),
}))
</script>

<template>
  <PostView
    :state="state"
    :features="features"
    :remove="remove"
    :likes="likes"
    :signed-in="account.signedIn"
    @retry="refresh()"
  />
</template>
