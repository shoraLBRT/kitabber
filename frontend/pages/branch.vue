<script setup lang="ts">
// The screen of one comment branch (E09-06, FR-COMMENT, docs/ui/comments.md) at `<post address>/comments/<comment id>`:
// `/u/<blog>/<id>-<slug>/comments/<id>` and `/<topic>/<id>-<slug>/comments/<id>`. A phone opens a branch deeper than one
// read here, its comment at the left edge, so the nesting never narrows the text below a readable width. The branch is
// read in the browser, as the comments under a post are; replies, changes and deletion work here as there. A branch of
// another post, a deleted one or a wrong address says the branch is not found.
import PostComments from '~/components/comments/PostComments.vue'
import { postIdOf } from '~/api/posts'

definePageMeta({
  path: '/u/:blog/:post([0-9a-fA-F]{8}-[^/]*)/comments/:comment([0-9a-fA-F-]+)',
  alias: ['/:blog/:post([0-9a-fA-F]{8}-[^/]*)/comments/:comment([0-9a-fA-F-]+)'],
  // Reply forms open under the comments; the phone's floating «Написать» would cover them.
  writeButton: false,
})

const route = useRoute()
const postId = computed(() => postIdOf(String(route.params.post)) ?? '')
const commentId = computed(() => String(route.params.comment).toLowerCase())
// The post's address: the branch's own without its last two segments.
const postPath = computed(() => route.path.replace(/\/comments\/[^/]+\/?$/, ''))

useHead({ title: 'Ветка комментариев' })
</script>

<template>
  <section class="branch" aria-labelledby="branch-title" data-testid="branch">
    <NuxtLink :to="`${postPath}#comments`" class="branch__back" data-testid="branch-back">← К посту</NuxtLink>
    <h1 id="branch-title" class="branch__title">Ветка комментариев</h1>
    <PostComments
      :key="`${postId}:${commentId}`"
      :post-id="postId"
      :post-path="postPath"
      :branch-id="commentId"
    />
  </section>
</template>

<style scoped>
.branch {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
  /* The card of the post page (PostView), so the branch reads as the comments under the post. */
  padding: var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.branch__back {
  align-self: flex-start;
  color: var(--color-accent);
  font-size: var(--font-size-sm);
  text-decoration: none;
}

.branch__back:hover {
  text-decoration: underline;
}

.branch__title {
  margin: 0;
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-bold);
}
</style>
