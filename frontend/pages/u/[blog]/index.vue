<script setup lang="ts">
// The «Посты» tab of a blog (E04-02). Posts come with E05; until then the tab shows its empty state.
import BlogTabEmpty from '~/components/blog/BlogTabEmpty.vue'

defineProps<{ owner?: boolean }>()
</script>

<template>
  <BlogTabEmpty tab="posts" :owner="owner" />
</template>
