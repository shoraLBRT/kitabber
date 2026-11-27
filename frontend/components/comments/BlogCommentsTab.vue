<script setup lang="ts">
// The tab «Комментарии» of a blog or a topic (E09-15, ADR-0114): the list of BlogCommentList.vue read by
// useBlogComments. The page keys it by the blog, so another blog reads its own list.
import BlogCommentList from './BlogCommentList.vue'

const props = defineProps<{ blogId: string }>()

const { state, authors, loadingMore, moreFailed, loadMore, retry } = useBlogComments(() => props.blogId)
</script>

<template>
  <BlogCommentList
    :state="state"
    :authors="authors"
    :loading-more="loadingMore"
    :more-failed="moreFailed"
    @retry="retry()"
    @more="loadMore()"
  />
</template>
