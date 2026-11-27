<script setup lang="ts">
// The «Посты» tab of a topic (E11-01, ADR-0082): its published posts, newest first. The topic page passes its id.
import TopicFeed from '~/components/topic/TopicFeed.vue'

defineProps<{ topicId?: string }>()
</script>

<template>
  <TopicFeed v-if="topicId" :key="topicId" :topic-id="topicId" />
</template>
