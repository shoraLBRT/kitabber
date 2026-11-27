<script setup lang="ts">
// The «Комментарии» tab of a blog (E04-02, E09-15, ADR-0114): the comments the blog's owner wrote, newest first. The
// blog page passes the blog's id; the list is read again when the reader blocks or unblocks the owner.
import BlogCommentsTab from '~/components/comments/BlogCommentsTab.vue'

defineProps<{ owner?: boolean; blogId?: string; blocked?: boolean }>()
</script>

<template>
  <BlogCommentsTab v-if="blogId" :key="`${blogId}:${blocked ? 'blocked' : 'shown'}`" :blog-id="blogId" />
</template>
