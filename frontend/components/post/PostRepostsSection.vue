<script setup lang="ts">
// The reposts of a published post on its page (E10-08, ADR-0117): PostReposts with its list read and the repost sent
// in the browser by useReposts.
import PostReposts from './PostReposts.vue'
import { useReposts } from '../../composables/useReposts'

const props = defineProps<{ postId: string; canRepost: boolean }>()

const reposts = useReposts(() => props.postId)
const { state, loadingMore, moreFailed, sending, sendError, reposted } = reposts
</script>

<template>
  <PostReposts
    :state="state"
    :can-repost="canRepost"
    :sending="sending"
    :send-error="sendError"
    :reposted="reposted"
    :loading-more="loadingMore"
    :more-failed="moreFailed"
    @retry="reposts.retry()"
    @more="reposts.loadMore()"
    @repost="reposts.repost($event)"
  />
</template>
