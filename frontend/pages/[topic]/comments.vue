<script setup lang="ts">
// The «Комментарии» tab of a topic (E09-15, ADR-0114): every comment under the topic's posts, guest ones from Telegram
// too, newest first. The topic page passes its id.
import BlogCommentsTab from '~/components/comments/BlogCommentsTab.vue'

defineProps<{ topicId?: string }>()
</script>

<template>
  <BlogCommentsTab v-if="topicId" :key="topicId" :blog-id="topicId" />
</template>
