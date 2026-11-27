<script setup lang="ts">
// The comments of the widget «Популярные комментарии» (E08-12, FR-FEED, ADR-0105): each is one link to the comment on
// its post's page — the avatar and the name of the author's blog and the first lines of the text cut with an ellipsis —
// and the title of the post below it as plain text, so the right column on every page adds no second link named as a
// post. The server chooses and orders them; the names come with the authors' profiles.
import UiAvatar from '../ui/UiAvatar.vue'
import { popularCommentPath, type PopularComment } from '../../api/popular-comments'
import { blogTitle, type Profile } from '../../api/profiles'
import { t } from '../../i18n'
import { isolateRtlText } from '../../utils/bidi'

const props = defineProps<{
  comments: readonly PopularComment[]
  authors: ReadonlyMap<string, Profile | null>
}>()

// A profile not read yet, or one that could not be read, leaves the comment with the word «Автор».
function nameOf(comment: PopularComment): string {
  const profile = props.authors.get(comment.authorAccountId)
  return profile ? blogTitle(profile) : t('common.author')
}
</script>

<template>
  <ol class="popular-comments" data-testid="popular-comments">
    <li v-for="comment in comments" :key="comment.id" class="popular-comments__item">
      <NuxtLink :to="popularCommentPath(comment)" class="popular-comment" data-testid="popular-comment">
        <span class="popular-comment__author">
          <UiAvatar
            :name="nameOf(comment)"
            :sources="authors.get(comment.authorAccountId)?.avatarSources ?? []"
            size="sm"
            decorative
          />
          <span class="popular-comment__name" data-testid="popular-comment-author">{{
            nameOf(comment)
          }}</span>
        </span>
        <span class="popular-comment__text" data-testid="popular-comment-text" dir="auto">{{
          isolateRtlText(comment.text)
        }}</span>
      </NuxtLink>
      <p v-if="comment.postTitle" class="popular-comment__post" data-testid="popular-comment-post" dir="auto">
        {{ isolateRtlText(comment.postTitle) }}
      </p>
    </li>
  </ol>
</template>

<style scoped>
.popular-comments {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  margin: 0;
  padding: 0;
  list-style: none;
}

.popular-comments__item {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.popular-comment {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  min-width: 0;
  padding: var(--space-2);
  border-radius: var(--radius-md);
  color: var(--color-text);
  text-decoration: none;
}

.popular-comment:hover {
  background: var(--color-surface-subtle);
}

.popular-comment:hover .popular-comment__name {
  text-decoration: underline;
}

.popular-comment__author {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  min-width: 0;
}

.popular-comment__name {
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-medium);
  overflow-wrap: anywhere;
}

/* The first lines only: three, the rest cut with an ellipsis. */
.popular-comment__text {
  display: -webkit-box;
  overflow: hidden;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 3;
  line-clamp: 3;
  font-size: var(--font-size-sm);
  white-space: pre-line;
  overflow-wrap: anywhere;
}

.popular-comment__post {
  margin: 0;
  padding: 0 var(--space-2) var(--space-2);
  overflow: hidden;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
