<script setup lang="ts">
// The feed of a hashtag (E11-08, FR-TOPIC, ADR-0120) at `/tags/<tag>`, where every hashtag of a post's text leads. One
// address per tag: another case leads to the lower-case one. A word that is not a tag (`/tags/2026`) is the usual 404.
// The feed is keyed by the tag, so moving from one tag to another reads the new feed.
import HashtagFeed from '~/components/hashtag/HashtagFeed.vue'
import { parseTag, tagPath } from '~/utils/hashtags'

const route = useRoute()
const asked = computed(() => String(route.params.tag ?? ''))
const tag = computed(() => parseTag(asked.value))

if (tag.value === null) throw createError({ statusCode: 404, statusMessage: 'Not Found', fatal: true })
if (tag.value !== asked.value) await navigateTo(tagPath(tag.value), { redirectCode: 301, replace: true })

useHead(() => ({ title: tag.value ? `#${tag.value}` : undefined }))
</script>

<template>
  <HashtagFeed v-if="tag" :key="tag" :tag="tag" />
</template>
