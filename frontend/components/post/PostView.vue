<script setup lang="ts">
// The post page (FR-POST, docs/ui/post.md): the post found by the id of its address, with its header (E07-04,
// ADR-0050: the author's avatar and name leading to their blog, the topic when the post is in one, the date or
// «черновик»), its title (a short post has none), its blocks drawn by PostBlocks (E07-03) and the floating action
// panel PostActions; the «…» menu PostMenu in the header holds the author's or a reader's items (E07-05, ADR-0053).
// States: loading, not found, a failure with «Повторить», and the post. Under a published post, its reposts and
// «Сделать репост» (E10-08, ADR-0117); a repost itself shows its text and leads to the original.
import { computed } from 'vue'
import UiAvatar from '../ui/UiAvatar.vue'
import UiButton from '../ui/UiButton.vue'
import PostActions from './PostActions.vue'
import PostBlocks from './PostBlocks.vue'
import PostComments from '../comments/PostComments.vue'
import PostMenu from './PostMenu.vue'
import PostRepostsSection from './PostRepostsSection.vue'
import type { PostMenuFeatures } from '../../utils/post-menu'
import { blogPath, blogTitle } from '../../api/profiles'
import { subsitePath, type PostLookup } from '../../api/posts'
import type { Likes } from '../../composables/useLikes'
import type { Bookmarks } from '../../composables/useBookmarks'
import { locale, t } from '../../i18n'
import { isolateRtlText } from '../../utils/bidi'

const props = defineProps<{
  state: PostLookup | { status: 'loading' }
  /** The flags of the «…» menu's items (E07-05). */
  features?: PostMenuFeatures
  /** Deletes the post from the author's menu; see PostMenu. */
  remove?: () => Promise<boolean>
  /**
   * The likes of the published post and of its comments (E10-01), read once by the page for the action panel and the
   * tree; without them «Нравится» is shown and disabled.
   */
  likes?: Likes | null
  /** The bookmarks of the published post and of its comments (E10-07); without them no «В закладки» is shown. */
  bookmarks?: Bookmarks | null
  /** The reader is signed in: someone else's post then offers «Заблокировать автора» in its menu (E11-06). */
  signedIn?: boolean
}>()

defineEmits<{ retry: [] }>()

// A fixed time zone keeps the server and the browser on the same text, so hydration does not differ.
const dateFormat = new Intl.DateTimeFormat(locale, {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  timeZone: 'UTC',
})

const found = computed(() => (props.state.status === 'found' ? props.state : null))

const repostOf = computed(() => found.value?.post.repostOfPostId ?? null)

const publishedId = computed(() => (found.value?.post.status === 'published' ? found.value.post.id : null))

// The author a signed-in reader may block from the menu (E11-06, ADR-0091): never the reader themselves.
const blockable = computed(() =>
  found.value && props.signedIn && !found.value.own
    ? {
        accountId: found.value.post.authorAccountId,
        name: found.value.author ? blogTitle(found.value.author) : t('common.author'),
      }
    : null,
)

// The blog is named apart from the author only when it is not the author's own: a topic.
const inBlog = computed(() =>
  found.value && found.value.author?.id !== found.value.blog.id ? found.value.blog : null,
)
</script>

<template>
  <section v-if="state.status === 'loading'" class="post" aria-busy="true" data-testid="post-loading">
    <span class="visually-hidden" role="status">{{ t('post.loading') }}</span>
    <div class="post__skeleton post__skeleton--short" />
    <div class="post__skeleton post__skeleton--title" />
    <div v-for="n in 3" :key="n" class="post__skeleton post__skeleton--line" />
  </section>

  <section v-else-if="state.status === 'not-found'" class="post post__message" data-testid="post-not-found">
    <h1 class="post__title">{{ t('post.notFound') }}</h1>
    <p class="post__muted">{{ t('post.notFoundText') }}</p>
    <NuxtLink to="/">{{ t('common.home') }}</NuxtLink>
  </section>

  <section v-else-if="state.status === 'error'" class="post post__message" data-testid="post-error">
    <h1 class="post__title">{{ t('post.loadFailed') }}</h1>
    <p class="post__muted" role="alert">{{ t('post.loadFailedText') }}</p>
    <UiButton variant="secondary" @click="$emit('retry')">{{ t('common.retry') }}</UiButton>
  </section>

  <article v-else-if="found" class="post" data-testid="post">
    <header class="post__header" data-testid="post-header">
      <NuxtLink
        v-if="found.author"
        :to="blogPath(found.author)"
        class="post__author"
        data-testid="post-author"
      >
        <UiAvatar
          :name="blogTitle(found.author)"
          :src="found.author.avatarUrl ?? undefined"
          :sources="found.author.avatarSources"
          size="md"
          decorative
        />
        <span class="post__author-name">{{ blogTitle(found.author) }}</span>
      </NuxtLink>
      <p class="post__muted post__meta">
        <template v-if="inBlog">
          <NuxtLink :to="subsitePath(inBlog)" class="post__blog" data-testid="post-blog">{{
            blogTitle(inBlog)
          }}</NuxtLink>
          <span aria-hidden="true"> · </span>
        </template>
        <span v-if="found.post.status === 'draft'" data-testid="post-draft">{{ t('post.draft') }}</span>
        <time v-else-if="found.post.publishedAt" :datetime="found.post.publishedAt" data-testid="post-date">{{
          dateFormat.format(new Date(found.post.publishedAt))
        }}</time>
      </p>
      <PostMenu
        :own="found.own"
        :path="found.path"
        :features="features"
        :remove="remove"
        :author="blockable"
      />
    </header>
    <h1 v-if="found.post.title" class="post__title" data-testid="post-title" dir="auto">
      {{ isolateRtlText(found.post.title) }}
    </h1>
    <template v-if="repostOf">
      <p v-if="found.post.repostText" class="post__repost-text" dir="auto" data-testid="post-repost-text">
        {{ found.post.repostText }}
      </p>
      <NuxtLink :to="`/u/-/${repostOf}`" class="post__repost-link" data-testid="post-repost-original">{{
        t('reposts.original')
      }}</NuxtLink>
    </template>
    <PostBlocks
      v-else
      :document="found.post.body"
      :images="found.post.images"
      :previews="found.post.imagePreviews"
    />
    <PostRepostsSection
      v-if="publishedId && !repostOf"
      :key="`reposts:${publishedId}`"
      class="post__reposts"
      :post-id="publishedId"
      :can-repost="Boolean(signedIn) && !found.own"
    />
    <div id="comments" class="post__comments">
      <PostComments
        v-if="found.post.status === 'published'"
        :key="found.post.id"
        :post-id="found.post.id"
        :post-path="found.path"
        :likes="likes"
      />
    </div>
    <PostActions
      :path="found.path"
      :title="found.post.title"
      :post-id="publishedId"
      :own="found.own"
      :likes="likes"
      :bookmarks="bookmarks"
    />
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

.post__header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2) var(--space-3);
  min-width: 0;
}

.post__author {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  min-width: 0;
  color: var(--color-text);
  font-weight: var(--font-weight-medium);
  text-decoration: none;
}

.post__author:hover .post__author-name {
  text-decoration: underline;
}

.post__author-name {
  overflow-wrap: anywhere;
}

.post__meta {
  overflow-wrap: anywhere;
}

/* The comments under the post (E09-02); «Комментарии» of the action panel leads to them. */
.post__repost-text {
  margin: 0;
  white-space: pre-line;
  overflow-wrap: anywhere;
}

.post__repost-link {
  align-self: flex-start;
  color: var(--color-accent);
  font-weight: var(--font-weight-medium);
}

.post__reposts {
  margin-top: var(--space-6);
}

.post__comments {
  margin-top: var(--space-6);
  scroll-margin-top: calc(var(--space-16) + var(--space-4));
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
