<script setup lang="ts">
// The comments under a post (E09-02, FR-COMMENT, docs/ui/comments.md): the heading with their number and the tree of
// nested lists, a level per `aria-level`. States: a skeleton of three comments while the tree loads, a failure with
// «Повторить» (the post above stays), no comments yet, and the tree. The form to write a comment (E09-03) stands
// above the tree once it is read, open when there is none yet. Next to the heading, «Популярные | По дате» (E09-04,
// ADR-0058) picks the order of the threads; it stays while the tree is read again and is gone only when there are no
// comments to order. The person's own comments (`own`) take the «…» menu with «Редактировать» and «Удалить» (E09-05,
// ADR-0059); `edit` and `remove` do the work and answer how it went. On a phone a branch deeper than one read opens on
// its own screen (`branchLink`, E09-06); that screen draws the same tree for one branch (`branch`), without the
// heading, the order and the form under the post, and says so when the branch is gone. The comment a link named stands
// out (highlighted, E09-07). The comments new since the previous opening are marked «новый» (`fresh`, E09-08,
// ADR-0109), and «К следующему новому · N» stays under the site's header while the comments are on screen.
import { computed, onBeforeUnmount, onMounted, provide, ref, toRef } from 'vue'
import { ArrowDown } from 'lucide-vue-next'
import UiButton from '../ui/UiButton.vue'
import CommentForm from './CommentForm.vue'
import CommentItem from './CommentItem.vue'
import {
  commentCount,
  commentSorts,
  nestComments,
  type CommentSort,
  type CommentsState,
} from '../../api/comments'
import type { Profile } from '../../api/profiles'
import type { Likes } from '../../composables/useLikes'
import { commentTreeKey, type BranchStatus, type CommentFailure, type FormTarget } from './context'
import { formatNumber, t } from '../../i18n'

const props = withDefaults(
  defineProps<{
    postId: string
    signedIn: boolean
    state: CommentsState
    authors: ReadonlyMap<string, Profile | null>
    branches: Readonly<Record<string, BranchStatus>>
    pending?: Readonly<Record<string, true>>
    failures?: Readonly<Record<FormTarget, CommentFailure>>
    resume?: FormTarget | null
    sort?: CommentSort
    own?: Readonly<Record<string, true>>
    edit?: (id: string, text: string) => Promise<string | null>
    remove?: (id: string) => Promise<boolean>
    /** Where a deep branch opens on its own screen (a phone, E09-06); `null` reads it in place. */
    branchLink?: ((commentId: string) => string) | null
    /** The tree is one branch on its own screen (E09-06): no heading, no order, no form under the post. */
    branch?: boolean
    /** The comments' likes (E10-01); `null` shows no «Нравится». */
    likes?: Likes | null
    /** The reader may block the authors of others' comments from their menus (E11-06, ADR-0091). */
    canBlock?: boolean
    /** The comment a link named (E09-07, ADR-0107): it stands out once brought into view. */
    highlighted?: string | null
    /** The permanent link of a comment (E09-07): its time links there and «Скопировать ссылку» copies it. */
    commentLink?: ((commentId: string) => string) | null
    /** The comments new since the previous opening (E09-08, ADR-0109): each is marked «новый». */
    fresh?: Readonly<Record<string, true>>
    /** How many new comments «К следующему новому» has still to go to; none hides it. */
    freshLeft?: number
  }>(),
  {
    pending: () => ({}),
    failures: () => ({}),
    resume: null,
    sort: 'popular',
    own: () => ({}),
    edit: async () => t('comments.editFailed'),
    remove: async () => false,
    branchLink: null,
    branch: false,
    likes: null,
    canBlock: false,
    highlighted: null,
    commentLink: null,
    fresh: () => ({}),
    freshLeft: 0,
  },
)

const emit = defineEmits<{
  retry: []
  sort: [sort: CommentSort]
  more: [commentId: string]
  send: [text: string, parentId: string | null]
  dismiss: [target: FormTarget]
  'sign-in': [target: FormTarget]
  blocked: [accountId: string]
  'next-new': []
}>()

// The time the menus compare the end of the time to change a comment with: «Редактировать» goes once it is over.
const now = ref(Date.now())
let clock: ReturnType<typeof setInterval> | undefined
onMounted(() => {
  now.value = Date.now()
  clock = setInterval(() => (now.value = Date.now()), 30_000)
})
onBeforeUnmount(() => clearInterval(clock))

provide(commentTreeKey, {
  postId: toRef(props, 'postId'),
  signedIn: toRef(props, 'signedIn'),
  maxDepth: computed(() => (props.state.status === 'found' ? props.state.maxDepth : 0)),
  authors: toRef(props, 'authors'),
  branches: toRef(props, 'branches'),
  pending: toRef(props, 'pending'),
  failures: toRef(props, 'failures'),
  resume: toRef(props, 'resume'),
  branchLink: toRef(props, 'branchLink'),
  own: toRef(props, 'own'),
  likes: toRef(props, 'likes'),
  now,
  edit: (id, text) => props.edit(id, text),
  remove: (id) => props.remove(id),
  more: (commentId) => emit('more', commentId),
  send: (text, parentId) => emit('send', text, parentId),
  dismiss: (target) => emit('dismiss', target),
  signIn: (target) => emit('sign-in', target),
  canBlock: toRef(props, 'canBlock'),
  blocked: (accountId) => emit('blocked', accountId),
  highlighted: toRef(props, 'highlighted'),
  commentLink: toRef(props, 'commentLink'),
  fresh: toRef(props, 'fresh'),
})

const comments = computed(() => (props.state.status === 'found' ? props.state.comments : []))
const roots = computed(() => nestComments(comments.value))
const count = computed(() => commentCount(comments.value))
const sortable = computed(() => props.state.status !== 'found' || roots.value.length > 0)
</script>

<template>
  <section class="comments" aria-labelledby="comments-title" data-testid="comments">
    <div v-if="!branch" class="comments__head">
      <h2 id="comments-title" class="comments__title">
        {{ t('comments.title') }}
        <span
          v-if="state.status === 'found' && count > 0"
          class="comments__count"
          data-testid="comments-count"
          >{{ formatNumber(count) }}</span
        >
      </h2>
      <div
        v-if="sortable"
        class="comments__sort"
        role="group"
        :aria-label="t('comments.sortLabel')"
        data-testid="comments-sort"
      >
        <button
          v-for="option in commentSorts"
          :key="option.id"
          type="button"
          class="comments__sort-option"
          :aria-pressed="option.id === sort ? 'true' : 'false'"
          :data-testid="`comments-sort-${option.id}`"
          @click="emit('sort', option.id)"
        >
          {{ option.label }}
        </button>
      </div>
    </div>

    <div
      v-if="state.status === 'loading'"
      class="comments__loading"
      aria-busy="true"
      data-testid="comments-loading"
    >
      <span class="visually-hidden" role="status">{{ t('comments.loading') }}</span>
      <div v-for="n in 3" :key="n" class="comments__skeleton">
        <div class="comments__skeleton-mark" />
        <div class="comments__skeleton-lines">
          <div class="comments__skeleton-line" />
          <div class="comments__skeleton-line comments__skeleton-line--short" />
        </div>
      </div>
    </div>

    <div v-else-if="state.status === 'error'" class="comments__error" data-testid="comments-error">
      <p class="comments__muted" role="alert">{{ t('comments.loadFailed') }}</p>
      <UiButton variant="secondary" size="sm" @click="emit('retry')">{{ t('common.retry') }}</UiButton>
    </div>

    <p v-else-if="state.status === 'not-found'" class="comments__muted" data-testid="comments-not-found">
      {{ t('comments.branchNotFound') }}
    </p>

    <template v-else>
      <CommentForm v-if="!branch" :inviting="roots.length === 0" />
      <button
        v-if="freshLeft > 0 && roots.length > 0"
        type="button"
        class="comments__next-new"
        :aria-label="t('comments.nextNewLabel', { count: freshLeft })"
        data-testid="comments-next-new"
        @click="emit('next-new')"
      >
        <ArrowDown class="comments__next-new-icon" aria-hidden="true" />
        {{ t('comments.nextNew', { count: formatNumber(freshLeft) }) }}
      </button>
      <p v-if="roots.length === 0" class="comments__muted" data-testid="comments-empty">
        {{ t('comments.empty') }}
      </p>
      <ul v-else class="comments__tree" data-testid="comment-tree">
        <CommentItem v-for="root in roots" :key="root.comment.id" :node="root" />
      </ul>
    </template>
  </section>
</template>

<style scoped>
.comments {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
}

/* «К следующему новому» (E09-08): it stays under the site's header while the comments are scrolled through. */
.comments__next-new {
  position: sticky;
  /* Higher while the phone header is hidden (E08-14, ADR-0111). */
  top: calc(var(--shell-header-offset, calc(var(--space-16) + env(safe-area-inset-top))) + var(--space-2));
  transition: top 0.2s ease;
  z-index: 5;
  display: inline-flex;
  align-items: center;
  align-self: flex-end;
  gap: var(--space-2);
  min-height: var(--size-touch);
  padding: 0 var(--space-4);
  border: 0;
  border-radius: var(--radius-full);
  background: var(--color-accent);
  color: var(--color-on-accent);
  font: inherit;
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-medium);
  box-shadow: 0 var(--space-1) var(--space-3) var(--color-shadow);
  cursor: pointer;
}

.comments__next-new:hover {
  background: var(--color-accent-hover);
}

.comments__next-new-icon {
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
}

.comments__head {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2) var(--space-4);
}

.comments__sort {
  display: flex;
  gap: var(--space-1);
  padding: var(--space-1);
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}

.comments__sort-option {
  min-height: var(--space-8);
  padding: var(--space-1) var(--space-3);
  border: 0;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--color-muted);
  font: inherit;
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-medium);
  cursor: pointer;
}

.comments__sort-option:hover {
  color: var(--color-text);
}

.comments__sort-option[aria-pressed='true'] {
  background: var(--color-surface);
  color: var(--color-text);
}

.comments__title {
  margin: 0;
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-bold);
}

.comments__count {
  color: var(--color-muted);
  font-weight: var(--font-weight-medium);
}

.comments__muted {
  margin: 0;
  color: var(--color-muted);
}

.comments__error {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: var(--space-2);
}

.comments__tree {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
  margin: 0;
  padding: 0;
  list-style: none;
}

.comments__loading {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.comments__skeleton {
  display: flex;
  gap: var(--space-2);
}

.comments__skeleton-mark {
  flex: none;
  width: var(--space-8);
  height: var(--space-8);
  border-radius: 50%;
  background: var(--color-surface-subtle);
}

.comments__skeleton-lines {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: var(--space-2);
}

.comments__skeleton-line {
  height: var(--space-4);
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}

.comments__skeleton-line--short {
  width: 60%;
}
</style>
