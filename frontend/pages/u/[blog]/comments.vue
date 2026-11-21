<script setup lang="ts">
// The «Комментарии» tab of a blog (E04-02). Comments come with E07; until then the tab shows its empty state.
import BlogTabEmpty from '~/components/blog/BlogTabEmpty.vue'

defineProps<{ owner?: boolean }>()
</script>

<template>
  <BlogTabEmpty tab="comments" :owner="owner" />
</template>
