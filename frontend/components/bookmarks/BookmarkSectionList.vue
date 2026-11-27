<script setup lang="ts">
// A tab of the section «Закладки» (E04-12, FR-PROFILE, docs/ui/blog.md, ADR-0116): the person's bookmarks in the order
// they were put, newest first. A post is its feed card (FeedCard, with «В закладки» to take it out); a comment is its
// post's title — a link to the comment on the post's page (ADR-0107) — its author, the time and the text. A post or a
// comment that is no longer shown keeps its place as a stub. States: loading (a skeleton), a failure with «Повторить»,
// the empty state, and «Показать ещё» while more pages are left.
import { computed, inject, onMounted, watch } from 'vue'
import FeedCard from '../feed/FeedCard.vue'
import UiAvatar from '../ui/UiAvatar.vue'
import UiButton from '../ui/UiButton.vue'
import UiState from '../ui/UiState.vue'
import { blogCommentPath, type BlogComment } from '../../api/blog-comments'
import type { BookmarkedComment, BookmarkedPost, BookmarkSectionTab } from '../../api/bookmark-section'
import { blogPath, blogTitle, type Profile } from '../../api/profiles'
import type { BookmarkSectionState } from '../../composables/useBookmarkSection'
import { bookmarksKey } from '../../composables/useBookmarks'
import { locale, t } from '../../i18n'
import { isolateRtlText } from '../../utils/bidi'

const props = defineProps<{
  tab: BookmarkSectionTab
  state: BookmarkSectionState<BookmarkedPost | BookmarkedComment>
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

const items = computed(() => (props.state.status === 'found' ? props.state.items : []))
const posts = computed(() => items.value.filter((item): item is BookmarkedPost => 'card' in item))
const comments = computed(() => items.value.filter((item): item is BookmarkedComment => 'commentId' in item))

// The cards' «В закладки» reads its state from the store app.vue provides, each new page's posts at once.
const bookmarks = inject(bookmarksKey, null)
onMounted(() => {
  watch(
    () => posts.value.flatMap((item) => (item.card ? [item.card.post.id] : [])),
    (ids) => void bookmarks?.loadPosts(ids),
    { immediate: true },
  )
})

function profileOf(comment: BlogComment): Profile | null {
  return comment.authorAccountId ? (props.authors.get(comment.authorAccountId) ?? null) : null
}

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
    class="bookmark-section"
    aria-busy="true"
    data-testid="bookmarks-loading"
  >
    <span class="visually-hidden" role="status">{{ t('bookmarkSection.loading') }}</span>
    <div v-for="n in 3" :key="n" class="bookmark-section__skeleton" />
  </div>

  <UiState v-else-if="state.status === 'error'" tone="error" data-testid="bookmarks-error">
    {{ t('bookmarkSection.failed') }}
    <template #actions>
      <UiButton variant="secondary" @click="$emit('retry')">{{ t('common.retry') }}</UiButton>
    </template>
  </UiState>

  <UiState v-else-if="state.status === 'empty'" data-testid="bookmarks-empty">
    {{ tab === 'posts' ? t('bookmarkSection.noPosts') : t('bookmarkSection.noComments') }}
  </UiState>

  <div v-else class="bookmark-section">
    <ol
      v-if="tab === 'posts'"
      class="bookmark-section__list"
      :aria-label="t('bookmarkSection.posts')"
      data-testid="bookmarked-posts"
    >
      <li v-for="item in posts" :key="item.postId" data-testid="bookmarked-post">
        <FeedCard v-if="item.card" :item="item.card" :bookmarks="bookmarks" />
        <p v-else class="bookmark-section__stub" data-testid="bookmark-stub">
          {{ t('bookmarkSection.postGone') }}
        </p>
      </li>
    </ol>

    <ol
      v-else
      class="bookmark-section__list"
      :aria-label="t('bookmarkSection.comments')"
      data-testid="bookmarked-comments"
    >
      <li v-for="item in comments" :key="item.commentId" data-testid="bookmarked-comment">
        <article v-if="item.comment" class="bookmark-comment">
          <NuxtLink
            :to="blogCommentPath(item.comment)"
            class="bookmark-comment__post"
            :aria-label="t('blogComments.toComment', { title: postTitle(item.comment) })"
            data-testid="bookmarked-comment-post"
            dir="auto"
            >{{ isolateRtlText(postTitle(item.comment)) }}</NuxtLink
          >
          <p class="bookmark-comment__meta">
            <UiAvatar
              :name="nameOf(item.comment)"
              :sources="profileOf(item.comment)?.avatarSources ?? []"
              size="sm"
              decorative
            />
            <NuxtLink
              v-if="profileOf(item.comment)"
              :to="blogPath(profileOf(item.comment)!)"
              class="bookmark-comment__author"
              >{{ nameOf(item.comment) }}</NuxtLink
            >
            <span v-else class="bookmark-comment__author">{{ nameOf(item.comment) }}</span>
            <time class="bookmark-comment__time" :datetime="item.comment.createdAt">{{
              timeFormat.format(new Date(item.comment.createdAt))
            }}</time>
          </p>
          <p class="bookmark-comment__text" data-testid="bookmarked-comment-text" dir="auto">
            {{ isolateRtlText(item.comment.text) }}
          </p>
        </article>
        <p v-else class="bookmark-section__stub" data-testid="bookmark-stub">
          {{ t('bookmarkSection.commentGone') }}
        </p>
      </li>
    </ol>

    <div v-if="state.hasMore" class="bookmark-section__more">
      <p v-if="moreFailed" class="bookmark-section__muted" role="alert">
        {{ t('bookmarkSection.moreFailed') }}
      </p>
      <span v-if="loadingMore" class="visually-hidden" role="status">{{
        t('bookmarkSection.loadingMore')
      }}</span>
      <UiButton
        variant="secondary"
        :disabled="loadingMore"
        data-testid="bookmarks-more"
        @click="$emit('more')"
        >{{
          loadingMore ? t('feed.loadingShort') : moreFailed ? t('common.retry') : t('actions.showMore')
        }}</UiButton
      >
    </div>
  </div>
</template>

<style scoped>
.bookmark-section,
.bookmark-section__list {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  min-width: 0;
}

.bookmark-section__list {
  margin: 0;
  padding: 0;
  list-style: none;
}

.bookmark-comment {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  min-width: 0;
  padding: var(--space-4) var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.bookmark-comment__post {
  color: var(--color-text);
  font-weight: var(--font-weight-bold);
  text-decoration: none;
  overflow-wrap: anywhere;
}

.bookmark-comment__post:hover {
  color: var(--color-accent);
}

.bookmark-comment__meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
  margin: 0;
  font-size: var(--font-size-sm);
}

.bookmark-comment__author {
  color: var(--color-text);
  font-weight: var(--font-weight-medium);
  text-decoration: none;
  overflow-wrap: anywhere;
}

.bookmark-comment__time,
.bookmark-section__muted {
  margin: 0;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

/* Long comments are cut: the whole text is on the post's page. */
.bookmark-comment__text {
  display: -webkit-box;
  margin: 0;
  overflow: hidden;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 6;
  line-clamp: 6;
  overflow-wrap: anywhere;
  white-space: pre-line;
}

/* The place of a deleted post or comment: the bookmark stays, the content is gone. */
.bookmark-section__stub {
  margin: 0;
  padding: var(--space-4) var(--space-5);
  border: var(--size-border) dashed var(--color-border);
  border-radius: var(--radius-lg);
  color: var(--color-muted);
}

.bookmark-section__more {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-2);
}

.bookmark-section__skeleton {
  height: calc(var(--space-16) * 1.5);
  border-radius: var(--radius-lg);
  background: var(--color-surface-subtle);
}
</style>
