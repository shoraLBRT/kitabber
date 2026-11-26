<script setup lang="ts">
// The floating action panel of the post page (E07-04, FR-POST, docs/ui/post.md, ADR-0050): «Нравится», «Комментарии»
// and «Поделиться» in a toolbar that sticks to the bottom of the window within the post's column while the post is
// read, and on a phone (below 925 px, where the bottom bar is) right above the bottom bar. «Нравится» (E10-01,
// ADR-0061) puts and takes back the like, with the count beside it; on one's own post and on a draft it only shows the
// count. A guest's press opens the sign-in window (useLikes). «Комментарии» leads to the comments under the post
// (#comments; the tree is E09-02). «Поделиться» copies the post's link; the phone's system menu and the social
// networks are E07-07 (#97). The arrow keys move between the buttons, as in a toolbar.
import { computed } from 'vue'
import { Heart, MessageCircle, Share2 } from 'lucide-vue-next'
import { useToast } from '../../composables/useToast'
import type { Likes } from '../../composables/useLikes'
import { copyPostLink } from '../../utils/post-menu'

const props = withDefaults(
  defineProps<{
    path: string
    /** The published post's id; `null` for a draft, which takes no likes. */
    postId?: string | null
    /** The person's own post: the like is not theirs to put. */
    own?: boolean
    likes?: Likes | null
  }>(),
  { postId: null, own: false, likes: null },
)

const like = computed(() => props.likes?.post.value ?? { count: 0, liked: false })
const likable = computed(() => props.likes !== null && props.postId !== null && !props.own)
const likeBusy = computed(() => props.postId !== null && props.likes?.isBusy('post', props.postId) === true)

function toggleLike() {
  if (likable.value && props.postId !== null) props.likes?.toggle('post', props.postId)
}

const toast = useToast()

const share = () => copyPostLink(props.path, toast)

function move(event: KeyboardEvent) {
  const step = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : 0
  if (!step) return
  const bar = event.currentTarget as HTMLElement
  const items = [...bar.querySelectorAll<HTMLElement>('a, button:not(:disabled)')]
  const at = items.indexOf(document.activeElement as HTMLElement)
  if (at < 0) return
  event.preventDefault()
  items[(at + step + items.length) % items.length]!.focus()
}
</script>

<template>
  <div
    class="post-actions"
    role="toolbar"
    aria-label="Действия с постом"
    data-testid="post-actions"
    @keydown="move"
  >
    <button
      type="button"
      class="post-actions__item"
      :class="{ 'post-actions__item--on': like.liked }"
      :disabled="!likable"
      :aria-pressed="likable ? (like.liked ? 'true' : 'false') : undefined"
      :aria-busy="likeBusy ? 'true' : undefined"
      :title="own ? 'Свой пост оценить нельзя' : undefined"
      data-testid="post-like"
      @click="toggleLike"
    >
      <Heart class="post-actions__icon" aria-hidden="true" />
      <span>Нравится</span>
      <b v-if="like.count > 0" class="post-actions__count" data-testid="post-like-count">{{ like.count }}</b>
    </button>
    <a href="#comments" class="post-actions__item" data-testid="post-comments">
      <MessageCircle class="post-actions__icon" aria-hidden="true" />
      <span>Комментарии</span>
    </a>
    <button type="button" class="post-actions__item" data-testid="post-share" @click="share">
      <Share2 class="post-actions__icon" aria-hidden="true" />
      <span>Поделиться</span>
    </button>
  </div>
</template>

<style scoped>
.post-actions {
  position: sticky;
  bottom: var(--space-4);
  z-index: 5;
  display: flex;
  align-self: center;
  gap: var(--space-1);
  padding: var(--space-1);
  border: var(--size-border) solid var(--color-border);
  border-radius: var(--radius-full);
  background: var(--color-surface);
  box-shadow: 0 var(--space-1) var(--space-3) var(--color-shadow);
}

.post-actions__item {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  min-height: var(--size-touch);
  padding: 0 var(--space-4);
  border: 0;
  border-radius: var(--radius-full);
  background: transparent;
  color: var(--color-text);
  font: inherit;
  font-size: var(--font-size-sm);
  text-decoration: none;
  cursor: pointer;
}

.post-actions__item:hover:not(:disabled) {
  background: var(--color-surface-subtle);
  color: var(--color-text);
}

.post-actions__item--on,
.post-actions__item--on:hover:not(:disabled) {
  color: var(--color-accent);
}

.post-actions__item--on .post-actions__icon {
  fill: currentcolor;
}

.post-actions__count {
  font-weight: var(--font-weight-bold);
  font-variant-numeric: tabular-nums;
}

.post-actions__item:disabled {
  color: var(--color-muted);
  cursor: default;
}

.post-actions__icon {
  width: var(--size-icon-md);
  height: var(--size-icon-md);
}

/* The phone shell: above the fixed bottom bar (its height, its padding and border, the safe area). */
@media (max-width: 924px) {
  .post-actions {
    bottom: calc(
      var(--size-tab-bar) + 2 * var(--space-1) + var(--size-border) + var(--space-2) +
        env(safe-area-inset-bottom)
    );
  }
}

/* Across the full width of a phone. */
@media (max-width: 639px) {
  .post-actions {
    align-self: stretch;
    justify-content: space-around;
    border-radius: var(--radius-lg);
  }

  .post-actions__item {
    padding: 0 var(--space-2);
  }

  /* Icons only, as docs/ui/post.md draws the phone: three labels are wider than 390 px and pushed the page sideways.
     The labels stay for screen readers. */
  .post-actions__item > span {
    position: absolute;
    width: var(--size-border);
    height: var(--size-border);
    overflow: hidden;
    clip-path: inset(50%);
    white-space: nowrap;
  }
}
</style>
