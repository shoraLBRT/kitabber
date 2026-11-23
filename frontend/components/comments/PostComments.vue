<script setup lang="ts">
// The comments of a published post (E09-02), read in the browser by useComments and drawn by CommentTree.
import CommentTree from './CommentTree.vue'
import { useComments } from '../../composables/useComments'

const props = defineProps<{ postId: string }>()

const { state, branches, authors, retry, more } = useComments(() => props.postId)
</script>

<template>
  <CommentTree :state="state" :authors="authors" :branches="branches" @retry="retry()" @more="more" />
</template>
