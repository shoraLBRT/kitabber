<script setup lang="ts">
// The tab «Комментарии» of a blog and a topic (E09-15, FR-COMMENT, docs/ui/blog.md, ADR-0114): the comments newest
// first, each with the post it is under — the title leads to the comment on the post's page (ADR-0107) — its author
// and the time, and its text. States: loading (a skeleton of the list; the header of the page stays), a failure with
// «Повторить», «Пока нет комментариев», and «Показать ещё» while more pages are left. The server chooses the comments:
// deleted ones and the authors the reader blocked are never here.
import UiAvatar from '../ui/UiAvatar.vue'
import UiButton from '../ui/UiButton.vue'
import UiState from '../ui/UiState.vue'
import { blogCommentPath, type BlogComment } from '../../api/blog-comments'
import { blogPath, blogTitle, type Profile } from '../../api/profiles'
import type { BlogCommentsState } from '../../composables/useBlogComments'
import { locale, t } from '../../i18n'
import { isolateRtlText } from '../../utils/bidi'

const props = defineProps<{
  state: BlogCommentsState
  authors: ReadonlyMap<string, Profile | null>
  loadingMore?: boolean
  moreFailed?: boolean
}>()

defineEmits<{ retry: []; more: [] }>()

// The list is drawn in the browser only, so the reader's own time zone is used.
const timeFormat = new Intl.DateTimeFormat(locale, {
  day: 'numeric',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
})

function profileOf(comment: BlogComment): Profile | null {
  return comment.authorAccountId ? (props.authors.get(comment.authorAccountId) ?? null) : null
}

// A guest from Telegram by its name; a profile not read yet, or one that could not be read, by the word «Автор».
function nameOf(comment: BlogComment): string {
  const profile = profileOf(comment)
  if (profile) return blogTitle(profile)
  return comment.guest?.name ?? t('common.author')
}

const postTitle = (comment: BlogComment) => comment.postTitle ?? t('blogComments.untitledPost')
</script>

<template>
  <div
    v-if="state.status === 'loading'"
    class="blog-comments"
    aria-busy="true"
    data-testid="blog-comments-loading"
  >
    <span class="visually-hidden" role="status">{{ t('blogComments.loading') }}</span>
    <div v-for="n in 3" :key="n" class="blog-comments__skeleton" />
  </div>

  <UiState v-else-if="state.status === 'error'" tone="error" data-testid="blog-comments-error">
    {{ t('blogComments.failed') }}
    <template #actions>
      <UiButton variant="secondary" @click="$emit('retry')">{{ t('common.retry') }}</UiButton>
    </template>
  </UiState>

  <UiState v-else-if="state.status === 'empty'" data-testid="blog-comments-empty">
    {{ t('blog.noComments') }}
  </UiState>

  <div v-else class="blog-comments">
    <ol class="blog-comments__list" :aria-label="t('blogComments.list')" data-testid="blog-comments">
      <li v-for="comment in state.comments" :key="comment.id" class="blog-comment" data-testid="blog-comment">
        <NuxtLink
          :to="blogCommentPath(comment)"
          class="blog-comment__post"
          :aria-label="t('blogComments.toComment', { title: postTitle(comment) })"
          data-testid="blog-comment-post"
          dir="auto"
          >{{ isolateRtlText(postTitle(comment)) }}</NuxtLink
        >
        <p class="blog-comment__meta">
          <UiAvatar
            :name="nameOf(comment)"
            :sources="profileOf(comment)?.avatarSources ?? []"
            size="sm"
            decorative
          />
          <NuxtLink
            v-if="profileOf(comment)"
            :to="blogPath(profileOf(comment)!)"
            class="blog-comment__author"
            data-testid="blog-comment-author"
            >{{ nameOf(comment) }}</NuxtLink
          >
          <span v-else class="blog-comment__author" data-testid="blog-comment-author">{{
            nameOf(comment)
          }}</span>
          <time class="blog-comment__time" :datetime="comment.createdAt">{{
            timeFormat.format(new Date(comment.createdAt))
          }}</time>
          <span v-if="comment.editedAt" class="blog-comment__time">{{ t('blogComments.edited') }}</span>
        </p>
        <p class="blog-comment__text" data-testid="blog-comment-text" dir="auto">
          {{ isolateRtlText(comment.text) }}
        </p>
      </li>
    </ol>

    <div v-if="state.hasMore" class="blog-comments__more">
      <p v-if="moreFailed" class="blog-comments__muted" role="alert">{{ t('blogComments.moreFailed') }}</p>
      <span v-if="loadingMore" class="visually-hidden" role="status">{{
        t('blogComments.loadingMore')
      }}</span>
      <UiButton
        variant="secondary"
        :disabled="loadingMore"
        data-testid="blog-comments-more"
        @click="$emit('more')"
        >{{
          loadingMore ? t('feed.loadingShort') : moreFailed ? t('common.retry') : t('actions.showMore')
        }}</UiButton
      >
    </div>
  </div>
</template>

<style scoped>
.blog-comments {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  min-width: 0;
}

.blog-comments__list {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  margin: 0;
  padding: 0;
  list-style: none;
}

.blog-comment {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  min-width: 0;
  padding: var(--space-4) var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.blog-comment__post {
  color: var(--color-text);
  font-weight: var(--font-weight-bold);
  text-decoration: none;
  overflow-wrap: anywhere;
}

.blog-comment__post:hover {
  color: var(--color-accent);
}

.blog-comment__meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
  margin: 0;
  font-size: var(--font-size-sm);
}

.blog-comment__author {
  color: var(--color-text);
  font-weight: var(--font-weight-medium);
  text-decoration: none;
  overflow-wrap: anywhere;
}

.blog-comment__time,
.blog-comments__muted {
  margin: 0;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

/* Long comments are cut: the whole text is on the post's page. */
.blog-comment__text {
  display: -webkit-box;
  margin: 0;
  overflow: hidden;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 6;
  line-clamp: 6;
  overflow-wrap: anywhere;
  white-space: pre-line;
}

.blog-comments__more {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-2);
}

.blog-comments__skeleton {
  height: calc(var(--space-16) * 1.5);
  border-radius: var(--radius-lg);
  background: var(--color-surface-subtle);
}
</style>
