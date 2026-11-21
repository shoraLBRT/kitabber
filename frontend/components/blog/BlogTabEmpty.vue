<script setup lang="ts">
// The empty state of a blog tab (docs/ui/blog.md, «Пусто»). Posts and comments do not exist yet (E05, E07), so every
// blog shows it for now; the owner gets «Написать первый пост» on the posts tab.
import { writePath } from '../../utils/navigation'
import type { BlogTab } from './BlogView.vue'

defineProps<{ tab: BlogTab; owner: boolean }>()
</script>

<template>
  <div class="blog-empty" :data-testid="`blog-empty-${tab}`">
    <template v-if="tab === 'posts'">
      <p class="blog-empty__text">Здесь пока нет постов</p>
      <NuxtLink v-if="owner" :to="writePath" class="blog-empty__write">Написать первый пост</NuxtLink>
    </template>
    <p v-else class="blog-empty__text">Пока нет комментариев</p>
  </div>
</template>

<style scoped>
.blog-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-8) var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  text-align: center;
}

.blog-empty__text {
  margin: 0;
  color: var(--color-muted);
}

.blog-empty__write {
  font-weight: var(--font-weight-medium);
}
</style>
