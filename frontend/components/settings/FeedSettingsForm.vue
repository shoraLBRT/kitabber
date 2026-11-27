<script setup lang="ts">
// «Ленты» of the settings (E08-13, FR-FEED, docs/ui/settings.md, ADR-0110): the feed the home page opens, the rating
// threshold of «Свежее» (ADR-0106) and the video autoplay, kept in the account so every device of the person applies
// them. A choice is saved at once; «Изменения сохранены» confirms it, a failure puts the previous choice back with a
// toast. Until the account's settings are read the section shows a skeleton, and «Повторить» when they could not be.
import { onMounted, ref, useId } from 'vue'
import { freshRatings, freshRatingValue, type FreshRating } from '../../api/feeds'
import type { FeedPreferences, HomeFeed, PreferencesApi } from '../../api/preferences'
import { useToast } from '../../composables/useToast'
import UiButton from '../ui/UiButton.vue'
import { t } from '../../i18n'

const props = defineProps<{ api: Pick<PreferencesApi, 'load' | 'saveFeeds'> }>()
const emit = defineEmits<{ saved: [feeds: FeedPreferences] }>()

const homeOptions: { value: HomeFeed; label: string }[] = [
  { value: 'popular', label: t('nav.popular') },
  { value: 'fresh', label: t('nav.new') },
  { value: 'my', label: t('nav.my') },
]

const state = ref<'loading' | 'loaded' | 'error'>('loading')
const feeds = ref<FeedPreferences>({ freshMinRating: null, homeFeed: 'popular', autoplayVideo: true })
const toast = useToast()
const id = useId()

async function load(): Promise<void> {
  state.value = 'loading'
  const saved = await props.api.load()
  if (saved) feeds.value = { ...saved.feeds }
  state.value = saved ? 'loaded' : 'error'
}
onMounted(load)

async function save(change: Partial<FeedPreferences>): Promise<void> {
  const previous = feeds.value
  const next = { ...previous, ...change }
  feeds.value = next
  if (await props.api.saveFeeds(next)) {
    toast.show(t('settings.saved'), { tone: 'success' })
    emit('saved', next)
  } else {
    feeds.value = previous
    toast.show(t('settings.saveFailed'), { tone: 'error' })
  }
}

function ratingLabel(rating: FreshRating): string {
  return rating === null
    ? t('feed.freshRatingAll')
    : t('feed.freshRatingFrom', { value: freshRatingValue(rating) })
}
</script>

<template>
  <div v-if="state === 'loading'" class="feed-settings" aria-busy="true" data-testid="settings-loading">
    <span class="visually-hidden" role="status">{{ t('settings.loading') }}</span>
    <div class="feed-settings__skeleton feed-settings__skeleton--label" />
    <div v-for="n in 3" :key="n" class="feed-settings__skeleton feed-settings__skeleton--option" />
  </div>

  <div v-else-if="state === 'error'" class="feed-settings" data-testid="settings-error">
    <h2 class="feed-settings__title">{{ t('settings.loadFailed') }}</h2>
    <p class="feed-settings__hint" role="alert">{{ t('settings.loadFailedText') }}</p>
    <div>
      <UiButton variant="secondary" @click="load()">{{ t('common.retry') }}</UiButton>
    </div>
  </div>

  <div v-else class="feed-settings__sections" data-testid="feed-settings">
    <fieldset class="feed-settings" :aria-describedby="`${id}-home-hint`" data-testid="feed-settings-home">
      <legend class="feed-settings__title">{{ t('settings.homeFeed') }}</legend>
      <p :id="`${id}-home-hint`" class="feed-settings__hint">{{ t('settings.homeFeedHint') }}</p>
      <label v-for="option in homeOptions" :key="option.value" class="feed-settings__option">
        <input
          type="radio"
          class="feed-settings__control"
          :name="`${id}-home`"
          :value="option.value"
          :checked="feeds.homeFeed === option.value"
          @change="save({ homeFeed: option.value })"
        />
        <span>{{ option.label }}</span>
      </label>
    </fieldset>

    <fieldset class="feed-settings" :aria-describedby="`${id}-fresh-hint`" data-testid="feed-settings-fresh">
      <legend class="feed-settings__title">{{ t('settings.freshThreshold') }}</legend>
      <p :id="`${id}-fresh-hint`" class="feed-settings__hint">{{ t('settings.freshThresholdHint') }}</p>
      <label v-for="rating in freshRatings" :key="String(rating)" class="feed-settings__option">
        <input
          type="radio"
          class="feed-settings__control"
          :name="`${id}-fresh`"
          :value="rating ?? 'all'"
          :checked="feeds.freshMinRating === rating"
          @change="save({ freshMinRating: rating })"
        />
        <span>{{ ratingLabel(rating) }}</span>
      </label>
    </fieldset>

    <fieldset class="feed-settings" data-testid="feed-settings-video">
      <legend class="feed-settings__title">{{ t('settings.video') }}</legend>
      <label class="feed-settings__option">
        <input
          type="checkbox"
          class="feed-settings__control"
          :checked="feeds.autoplayVideo"
          @change="save({ autoplayVideo: ($event.target as HTMLInputElement).checked })"
        />
        <span>{{ t('settings.autoplayVideo') }}</span>
      </label>
    </fieldset>
  </div>
</template>

<style scoped>
.feed-settings__sections {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
}

.feed-settings {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  min-width: 0;
  margin: 0;
  padding: 0;
  border: 0;
}

.feed-settings__title {
  margin: 0;
  padding: 0;
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-bold);
}

.feed-settings__hint {
  margin: 0 0 var(--space-2);
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.feed-settings__option {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  min-height: var(--size-touch);
  cursor: pointer;
}

.feed-settings__control {
  flex: none;
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
  margin: 0;
  accent-color: var(--color-accent);
}

.feed-settings__skeleton {
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}

.feed-settings__skeleton--label {
  width: 40%;
  height: var(--space-6);
}

.feed-settings__skeleton--option {
  height: var(--size-touch);
}
</style>
