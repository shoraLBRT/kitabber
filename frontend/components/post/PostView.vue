<script setup lang="ts">
// The post page (FR-POST, docs/ui/post.md) as far as E07-02 needs it: the post found by the id of its address, with
// its blog, its title (a short post has none) and its blocks drawn by PostBlocks (E07-03). The header with the author
// and the action panel come with E07-04 (#52). States: loading, not found, a failure with «Повторить», and the post.
import { computed } from 'vue'
import UiButton from '../ui/UiButton.vue'
import PostBlocks from './PostBlocks.vue'
import { blogTitle } from '../../api/profiles'
import { subsitePath, type PostLookup } from '../../api/posts'

const props = defineProps<{ state: PostLookup | { status: 'loading' } }>()

defineEmits<{ retry: [] }>()

const found = computed(() => (props.state.status === 'found' ? props.state : null))
</script>

<template>
  <section v-if="state.status === 'loading'" class="post" aria-busy="true" data-testid="post-loading">
    <span class="visually-hidden" role="status">Загрузка поста</span>
    <div class="post__skeleton post__skeleton--short" />
    <div class="post__skeleton post__skeleton--title" />
    <div v-for="n in 3" :key="n" class="post__skeleton post__skeleton--line" />
  </section>

  <section v-else-if="state.status === 'not-found'" class="post post__message" data-testid="post-not-found">
    <h1 class="post__title">Пост не найден</h1>
    <p class="post__muted">Возможно, адрес набран с ошибкой или пост удалён.</p>
    <NuxtLink to="/">На главную</NuxtLink>
  </section>

  <section v-else-if="state.status === 'error'" class="post post__message" data-testid="post-error">
    <h1 class="post__title">Не удалось загрузить пост</h1>
    <p class="post__muted" role="alert">Проверьте соединение и попробуйте ещё раз.</p>
    <UiButton variant="secondary" @click="$emit('retry')">Повторить</UiButton>
  </section>

  <article v-else-if="found" class="post" data-testid="post">
    <p class="post__muted">
      <NuxtLink :to="subsitePath(found.blog)" class="post__blog" data-testid="post-blog">{{
        blogTitle(found.blog)
      }}</NuxtLink>
      <span v-if="found.post.status === 'draft'" data-testid="post-draft"> · черновик</span>
    </p>
    <h1 v-if="found.post.title" class="post__title" data-testid="post-title">{{ found.post.title }}</h1>
    <PostBlocks :document="found.post.body" :images="found.post.images" />
  </article>
</template>

<style scoped>
.post {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  min-width: 0;
  padding: var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.post__message {
  align-items: flex-start;
}

.post__title {
  margin: 0;
  font-size: var(--font-size-3xl);
  font-weight: var(--font-weight-bold);
  overflow-wrap: anywhere;
}

.post__muted {
  margin: 0;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.post__blog {
  color: var(--color-text);
  font-weight: var(--font-weight-medium);
  text-decoration: none;
}

.post__blog:hover {
  text-decoration: underline;
}

.post__skeleton {
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}

.post__skeleton--short {
  width: 30%;
  height: var(--space-4);
}

.post__skeleton--title {
  width: 70%;
  height: var(--space-8);
}

.post__skeleton--line {
  height: var(--space-4);
}
</style>
