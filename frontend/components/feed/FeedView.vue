<script setup lang="ts">
// A feed (FR-FEED, docs/ui/feed.md): the posts in the feed's order, each with its title (or the first line of a short
// post) and its date, leading to the post. The full card is E08-02 (#55), «Показать новые» is E08-08 (#100).
// States: loading, empty, a failure with «Повторить», and the posts. The next page loads by itself when the end of the
// list comes near (E08-03, ADR-0051); a failure of a later page keeps the posts shown and offers «Повторить» instead
// of trying again on its own. Without IntersectionObserver «Показать ещё» asks for the next page.
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import UiButton from '../ui/UiButton.vue'
import { feedPostPath, type FeedState } from '../../api/feeds'
import { postText } from '../../api/posts'

const props = defineProps<{ title: string; state: FeedState; loadingMore?: boolean; moreFailed?: boolean }>()

const emit = defineEmits<{ retry: []; more: [] }>()

// The next page is asked for while the end of the list is still this far below the screen.
const preloadMargin = '0px 0px 800px 0px'

const sentinel = ref<HTMLElement | null>(null)
const unobservable = ref(false)
let observer: IntersectionObserver | null = null

onMounted(() => {
  if (typeof IntersectionObserver === 'undefined') {
    unobservable.value = true
    return
  }
  observer = new IntersectionObserver(
    (entries) => {
      if (entries.some((entry) => entry.isIntersecting)) emit('more')
    },
    { rootMargin: preloadMargin },
  )
  if (sentinel.value) observer.observe(sentinel.value)
})

// The mark is drawn anew after every page, so a list still too short for the screen asks for the page after.
watch(sentinel, (element, previous) => {
  if (previous) observer?.unobserve(previous)
  if (element) observer?.observe(element)
})

onBeforeUnmount(() => observer?.disconnect())

// A fixed time zone keeps the server and the browser on the same text, so hydration does not differ.
const dateFormat = new Intl.DateTimeFormat('ru', {
  day: 'numeric',
  month: 'long',
  hour: '2-digit',
  minute: '2-digit',
  timeZone: 'UTC',
})

const items = computed(() =>
  props.state.status === 'found'
    ? props.state.posts.map((post) => ({
        post,
        path: feedPostPath(post),
        heading: post.title ?? postText(post)[0] ?? 'Пост',
        excerpt: post.title ? postText(post)[0] : undefined,
        date: post.publishedAt ? dateFormat.format(new Date(post.publishedAt)) : '',
      }))
    : [],
)
</script>

<template>
  <section class="feed" aria-labelledby="feed-title" data-testid="feed">
    <h1 id="feed-title" class="feed__title">{{ title }}</h1>

    <div v-if="state.status === 'loading'" class="feed__list" aria-busy="true" data-testid="feed-loading">
      <span class="visually-hidden" role="status">Загрузка ленты</span>
      <div v-for="n in 3" :key="n" class="feed__item feed__skeleton" />
    </div>

    <div v-else-if="state.status === 'error'" class="feed__message" data-testid="feed-error">
      <p class="feed__muted" role="alert">
        Не удалось загрузить ленту. Проверьте соединение и попробуйте ещё раз.
      </p>
      <UiButton variant="secondary" @click="$emit('retry')">Повторить</UiButton>
    </div>

    <p v-else-if="items.length === 0" class="feed__message feed__muted" data-testid="feed-empty">
      Здесь пока нет постов.
    </p>

    <template v-else>
      <ol class="feed__list" data-testid="feed-list">
        <li v-for="item in items" :key="item.post.id" class="feed__item" data-testid="feed-item">
          <article class="feed__card">
            <NuxtLink :to="item.path" class="feed__link">
              <h2 class="feed__heading">{{ item.heading }}</h2>
            </NuxtLink>
            <p v-if="item.excerpt" class="feed__excerpt">{{ item.excerpt }}</p>
            <time
              v-if="item.post.publishedAt"
              class="feed__muted feed__date"
              :datetime="item.post.publishedAt"
              >{{ item.date }}</time
            >
          </article>
        </li>
      </ol>
      <div v-if="state.status === 'found' && state.hasMore" class="feed__more">
        <template v-if="moreFailed">
          <p class="feed__muted" role="alert">Не удалось загрузить следующие посты.</p>
          <UiButton
            variant="secondary"
            :disabled="loadingMore"
            data-testid="feed-more"
            @click="$emit('more')"
          >
            {{ loadingMore ? 'Загрузка…' : 'Повторить' }}
          </UiButton>
        </template>
        <p v-else-if="loadingMore" class="feed__muted" role="status" data-testid="feed-loading-more">
          Загрузка следующих постов…
        </p>
        <div
          v-else-if="!unobservable"
          ref="sentinel"
          class="feed__sentinel"
          aria-hidden="true"
          data-testid="feed-sentinel"
        />
        <UiButton v-else variant="secondary" data-testid="feed-more" @click="$emit('more')"
          >Показать ещё</UiButton
        >
      </div>
    </template>
  </section>
</template>

<style scoped>
.feed {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.feed__title {
  margin: 0;
  padding: 0 var(--space-5);
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-bold);
}

.feed__list {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  margin: 0;
  padding: 0;
  list-style: none;
}

.feed__item,
.feed__message {
  padding: var(--space-5);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
}

.feed__message {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: var(--space-3);
  margin: 0;
}

.feed__card {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  min-width: 0;
}

.feed__link {
  color: inherit;
  text-decoration: none;
}

.feed__link:hover .feed__heading {
  text-decoration: underline;
}

.feed__heading {
  margin: 0;
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-bold);
  overflow-wrap: anywhere;
}

.feed__excerpt {
  margin: 0;
  font-size: var(--font-size-md);
  overflow-wrap: anywhere;
}

.feed__muted {
  margin: 0;
  color: var(--color-muted);
}

.feed__date {
  font-size: var(--font-size-sm);
}

.feed__skeleton {
  height: calc(var(--space-5) * 5);
}

.feed__more {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-2);
}

/* The mark at the end of the list that asks for the next page; it takes almost no room. */
.feed__sentinel {
  width: 100%;
  height: var(--size-border);
}

@media (max-width: 639px) {
  .feed__item,
  .feed__message {
    border-radius: 0;
  }
}
</style>
