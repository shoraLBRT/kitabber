<script setup lang="ts">
// The reposts of a post on its page (E10-08, FR-REACT, docs/ui/post.md, ADR-0117): «Сделать репост» for a signed-in
// reader of someone else's post opens a form with an optional text, and the list of the post's reposts follows, newest
// first — who reposted, when, and what they wrote. States: loading (a skeleton), a failure with «Повторить»,
// «Репостов пока нет», and «Показать ещё» while more pages are left. A repost lands in the reader's blog and in the
// «Моя лента» of their subscribers; the author of the post is notified by the server.
import { ref } from 'vue'
import UiAvatar from '../ui/UiAvatar.vue'
import UiButton from '../ui/UiButton.vue'
import UiState from '../ui/UiState.vue'
import { repostTextMaxLength } from '../../api/reposts'
import { blogTitle } from '../../api/profiles'
import { subsitePath } from '../../api/posts'
import type { RepostsState } from '../../composables/useReposts'
import { locale, t } from '../../i18n'

const props = withDefaults(
  defineProps<{
    state: RepostsState
    /** The reader may repost: signed in, and the post is not theirs. */
    canRepost?: boolean
    sending?: boolean
    sendError?: string | null
    /** The reader has just reposted the post. */
    reposted?: boolean
    loadingMore?: boolean
    moreFailed?: boolean
  }>(),
  {
    canRepost: false,
    sending: false,
    sendError: null,
    reposted: false,
    loadingMore: false,
    moreFailed: false,
  },
)

const emit = defineEmits<{ retry: []; more: []; repost: [text: string] }>()

// The list is drawn in the browser only, so the reader's own time zone is used.
const timeFormat = new Intl.DateTimeFormat(locale, {
  day: 'numeric',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
})

const open = ref(false)
const text = ref('')

function submit() {
  if (props.sending || text.value.length > repostTextMaxLength) return
  emit('repost', text.value)
}

function cancel() {
  open.value = false
  text.value = ''
}
</script>

<template>
  <section class="reposts" aria-labelledby="reposts-title" data-testid="reposts">
    <header class="reposts__header">
      <h2 id="reposts-title" class="reposts__title">{{ t('reposts.title') }}</h2>
      <UiButton
        v-if="canRepost && !open && !reposted"
        variant="secondary"
        size="sm"
        data-testid="repost-open"
        @click="open = true"
      >
        {{ t('reposts.open') }}
      </UiButton>
    </header>

    <p v-if="reposted" class="reposts__done" role="status" data-testid="repost-done">
      {{ t('reposts.done') }}
    </p>

    <form
      v-else-if="canRepost && open"
      class="reposts__form"
      data-testid="repost-form"
      @submit.prevent="submit"
    >
      <label class="reposts__label" for="repost-text">{{ t('reposts.textLabel') }}</label>
      <textarea
        id="repost-text"
        v-model="text"
        class="reposts__text"
        rows="3"
        :maxlength="repostTextMaxLength"
        :placeholder="t('reposts.textPlaceholder')"
        data-testid="repost-text"
      />
      <p v-if="sendError" class="reposts__error" role="alert" data-testid="repost-error">{{ sendError }}</p>
      <div class="reposts__actions">
        <UiButton type="submit" size="sm" :loading="sending" data-testid="repost-submit">
          {{ t('reposts.submit') }}
        </UiButton>
        <UiButton variant="ghost" size="sm" :disabled="sending" @click="cancel">
          {{ t('actions.cancel') }}
        </UiButton>
      </div>
    </form>

    <div v-if="state.status === 'loading'" aria-busy="true" data-testid="reposts-loading">
      <span class="visually-hidden" role="status">{{ t('reposts.loading') }}</span>
      <div v-for="n in 2" :key="n" class="reposts__skeleton" />
    </div>

    <UiState v-else-if="state.status === 'error'" tone="error" data-testid="reposts-error">
      {{ t('reposts.loadFailed') }}
      <template #actions>
        <UiButton variant="secondary" size="sm" @click="$emit('retry')">{{ t('common.retry') }}</UiButton>
      </template>
    </UiState>

    <p v-else-if="state.status === 'empty'" class="reposts__muted" data-testid="reposts-empty">
      {{ t('reposts.empty') }}
    </p>

    <template v-else>
      <ul class="reposts__list">
        <li v-for="item in state.reposts" :key="item.id" class="reposts__item" data-testid="repost-item">
          <div class="reposts__meta">
            <NuxtLink v-if="item.author" :to="subsitePath(item.author)" class="reposts__author">
              <UiAvatar
                :name="blogTitle(item.author)"
                :sources="item.author.avatarSources"
                size="sm"
                decorative
              />
              <span>{{ blogTitle(item.author) }}</span>
            </NuxtLink>
            <span v-else>{{ t('common.author') }}</span>
            <time class="reposts__muted" :datetime="item.publishedAt">{{
              timeFormat.format(new Date(item.publishedAt))
            }}</time>
          </div>
          <p v-if="item.text" class="reposts__item-text" dir="auto" data-testid="repost-item-text">
            {{ item.text }}
          </p>
        </li>
      </ul>
      <p v-if="moreFailed" class="reposts__error" role="alert">{{ t('reposts.moreFailed') }}</p>
      <UiButton
        v-if="state.hasMore"
        variant="secondary"
        size="sm"
        :loading="loadingMore"
        data-testid="reposts-more"
        @click="$emit('more')"
      >
        {{ t('reposts.more') }}
      </UiButton>
    </template>
  </section>
</template>

<style scoped>
.reposts {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  min-width: 0;
}

.reposts__header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}

.reposts__title {
  margin: 0;
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-bold);
}

.reposts__form {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.reposts__label {
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-medium);
}

.reposts__text {
  width: 100%;
  min-height: var(--space-16);
  padding: var(--space-2) var(--space-3);
  border: var(--size-border) solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: var(--color-text);
  font: inherit;
  resize: vertical;
}

.reposts__actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.reposts__list {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  margin: 0;
  padding: 0;
  list-style: none;
}

.reposts__item {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  min-width: 0;
}

.reposts__meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
  font-size: var(--font-size-sm);
}

.reposts__author {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  color: var(--color-text);
  font-weight: var(--font-weight-medium);
  text-decoration: none;
}

.reposts__author:hover {
  text-decoration: underline;
}

.reposts__item-text {
  margin: 0;
  white-space: pre-line;
  overflow-wrap: anywhere;
}

.reposts__muted {
  margin: 0;
  color: var(--color-muted);
}

.reposts__done {
  margin: 0;
  color: var(--color-success);
}

.reposts__error {
  margin: 0;
  color: var(--color-danger);
}

.reposts__skeleton {
  height: var(--space-10);
  margin-bottom: var(--space-2);
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}
</style>
