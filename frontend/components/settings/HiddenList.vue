<script setup lang="ts">
// The hidden topics and posts in «Приватность и блокировки» (E11-07, FR-SUB, FR-TOPIC, ADR-0119,
// docs/ui/settings.md): what the person hid from their feeds, the latest first, each with «Вернуть в ленты». A topic
// links to its page, a post to its address; a post that is gone says so and may still be brought back. Until the lists
// are read the section shows a skeleton, «Повторить» when they could not be, and a hint for an empty list. The calls
// come in as `api` (api/hidden.ts), so the lists are tested without a server.
import { onMounted, ref } from 'vue'
import UiButton from '../ui/UiButton.vue'
import type { HiddenApi, HiddenKind, HiddenList, HiddenPost, HiddenTopic } from '../../api/hidden'
import { locale, t } from '../../i18n'

const props = defineProps<{ api: HiddenApi }>()

const state = ref<'loading' | 'error' | 'ready'>('loading')
const topics = ref<HiddenTopic[]>([])
const posts = ref<HiddenPost[]>([])
const removing = ref<string>()
const formError = ref<string>()
const notice = ref<string>()

const dateFormat = new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long', year: 'numeric' })

async function load() {
  state.value = 'loading'
  const result = await props.api.load()
  const list = result.body as HiddenList | undefined
  if (result.status !== 200 || !Array.isArray(list?.posts) || !Array.isArray(list?.topics)) {
    state.value = 'error'
    return
  }
  topics.value = list.topics
  posts.value = list.posts
  state.value = 'ready'
}

onMounted(load)

function topicName(topic: HiddenTopic): string {
  return topic.name ?? (topic.nickname ? `/${topic.nickname}` : t('hidden.unknownTopic'))
}

function postName(post: HiddenPost): string {
  return post.gone ? t('hidden.gonePost') : (post.title ?? t('hidden.untitledPost'))
}

async function show(kind: HiddenKind, id: string, name: string) {
  formError.value = notice.value = undefined
  removing.value = id
  const result = await props.api.show(kind, id)
  removing.value = undefined
  if (result.status === 204) {
    if (kind === 'topics') topics.value = topics.value.filter((topic) => topic.blogId !== id)
    else posts.value = posts.value.filter((post) => post.postId !== id)
    notice.value = t('hidden.shown', { name })
    return
  }
  formError.value = result.status === 401 ? t('settings.sessionEnded') : t('hidden.showFailed')
}
</script>

<template>
  <section class="hidden" data-testid="hidden-list" :aria-busy="state === 'loading'">
    <h2 class="hidden__title">{{ t('hidden.title') }}</h2>
    <p class="hidden__hint">{{ t('hidden.hint') }}</p>

    <template v-if="state === 'loading'">
      <span class="visually-hidden" role="status">{{ t('hidden.loading') }}</span>
      <div class="hidden__skeleton" data-testid="hidden-list-loading" />
    </template>

    <template v-else-if="state === 'error'">
      <p class="hidden__error" role="alert">{{ t('hidden.loadFailed') }}</p>
      <div>
        <UiButton variant="secondary" @click="load()">{{ t('common.retry') }}</UiButton>
      </div>
    </template>

    <template v-else>
      <h3 class="hidden__subtitle">{{ t('hidden.topics') }}</h3>
      <p v-if="topics.length === 0" class="hidden__hint" data-testid="hidden-topics-empty">
        {{ t('hidden.topicsEmpty') }}
      </p>
      <ul v-else class="hidden__list">
        <li v-for="topic in topics" :key="topic.blogId" class="hidden__item" data-testid="hidden-topic">
          <div class="hidden__info">
            <NuxtLink class="hidden__name" :to="`/${topic.nickname ?? topic.blogId}`">{{
              topicName(topic)
            }}</NuxtLink>
            <p class="hidden__time">
              {{ t('hidden.since', { date: dateFormat.format(new Date(topic.hiddenAt)) }) }}
            </p>
          </div>
          <UiButton
            variant="secondary"
            :loading="removing === topic.blogId"
            :aria-label="t('hidden.showLabel', { name: topicName(topic) })"
            @click="show('topics', topic.blogId, topicName(topic))"
          >
            {{ t('hidden.show') }}
          </UiButton>
        </li>
      </ul>

      <h3 class="hidden__subtitle">{{ t('hidden.posts') }}</h3>
      <p v-if="posts.length === 0" class="hidden__hint" data-testid="hidden-posts-empty">
        {{ t('hidden.postsEmpty') }}
      </p>
      <ul v-else class="hidden__list">
        <li v-for="post in posts" :key="post.postId" class="hidden__item" data-testid="hidden-post">
          <div class="hidden__info">
            <p v-if="post.gone" class="hidden__name hidden__name--gone">{{ postName(post) }}</p>
            <NuxtLink v-else class="hidden__name" :to="`/u/-/${post.postId}`">{{ postName(post) }}</NuxtLink>
            <p class="hidden__time">
              {{ t('hidden.since', { date: dateFormat.format(new Date(post.hiddenAt)) }) }}
            </p>
          </div>
          <UiButton
            variant="secondary"
            :loading="removing === post.postId"
            :aria-label="t('hidden.showLabel', { name: postName(post) })"
            @click="show('posts', post.postId, postName(post))"
          >
            {{ t('hidden.show') }}
          </UiButton>
        </li>
      </ul>

      <p v-if="formError" class="hidden__error" role="alert">{{ formError }}</p>
      <p v-if="notice" class="hidden__notice" role="status">{{ notice }}</p>
    </template>
  </section>
</template>

<style scoped>
.hidden {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

.hidden__title {
  margin: 0;
  font-size: var(--font-size-lg);
}

.hidden__subtitle {
  margin: 0;
  font-size: var(--font-size-md);
}

.hidden__list {
  display: flex;
  flex-direction: column;
  margin: 0;
  padding: 0;
  list-style: none;
}

.hidden__item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
  padding: var(--space-3) 0;
  border-bottom: var(--size-border) solid var(--color-border);
}

.hidden__info {
  min-width: 0;
}

.hidden__name,
.hidden__time,
.hidden__hint {
  margin: 0;
  overflow-wrap: anywhere;
}

.hidden__name {
  color: var(--color-text);
  font-weight: var(--font-weight-medium);
}

.hidden__name--gone,
.hidden__time,
.hidden__hint {
  color: var(--color-muted);
}

.hidden__error {
  margin: 0;
  color: var(--color-danger);
}

.hidden__notice {
  margin: 0;
  color: var(--color-success);
}

.hidden__skeleton {
  height: var(--size-touch);
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}
</style>
