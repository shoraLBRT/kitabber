<script setup lang="ts">
// The reposts of a published post on its page (E10-08, ADR-0117): PostReposts with its list read and the repost sent
// in the browser by useReposts. The section is under the comments; its list is read once it comes within a screen of
// the view, so a long comment tree is drawn and used first, without the list's request and render competing with it.
import { onBeforeUnmount, onMounted, ref } from 'vue'
import PostReposts from './PostReposts.vue'
import { useReposts } from '../../composables/useReposts'

const props = defineProps<{ postId: string; canRepost: boolean }>()

const reposts = useReposts(() => props.postId)
const { state, loadingMore, moreFailed, sending, sendError, reposted } = reposts

const root = ref<HTMLElement | null>(null)
let observer: IntersectionObserver | null = null

onMounted(() => {
  if (typeof IntersectionObserver === 'undefined' || !root.value) {
    reposts.start()
    return
  }
  observer = new IntersectionObserver(
    (entries) => {
      if (!entries.some((entry) => entry.isIntersecting)) return
      observer?.disconnect()
      reposts.start()
    },
    { rootMargin: '100% 0px' },
  )
  observer.observe(root.value)
})

onBeforeUnmount(() => observer?.disconnect())
</script>

<template>
  <div ref="root">
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
  </div>
</template>
