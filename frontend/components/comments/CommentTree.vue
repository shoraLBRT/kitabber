<script setup lang="ts">
// The comments under a post (E09-02, FR-COMMENT, docs/ui/comments.md): the heading with their number and the tree of
// nested lists, a level per `aria-level`. States: a skeleton of three comments while the tree loads, a failure with
// «Повторить» (the post above stays), no comments yet, and the tree. The form to write a comment (E09-03) stands
// above the tree once it is read, open when there is none yet. Next to the heading, «Популярные | По дате» (E09-04,
// ADR-0058) picks the order of the threads; it stays while the tree is read again and is gone only when there are no
// comments to order.
import { computed, provide, toRef } from 'vue'
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
import { commentTreeKey, type BranchStatus, type CommentFailure, type FormTarget } from './context'

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
  }>(),
  { pending: () => ({}), failures: () => ({}), resume: null, sort: 'popular' },
)

const emit = defineEmits<{
  retry: []
  sort: [sort: CommentSort]
  more: [commentId: string]
  send: [text: string, parentId: string | null]
  dismiss: [target: FormTarget]
  'sign-in': [target: FormTarget]
}>()

provide(commentTreeKey, {
  postId: toRef(props, 'postId'),
  signedIn: toRef(props, 'signedIn'),
  maxDepth: computed(() => (props.state.status === 'found' ? props.state.maxDepth : 0)),
  authors: toRef(props, 'authors'),
  branches: toRef(props, 'branches'),
  pending: toRef(props, 'pending'),
  failures: toRef(props, 'failures'),
  resume: toRef(props, 'resume'),
  more: (commentId) => emit('more', commentId),
  send: (text, parentId) => emit('send', text, parentId),
  dismiss: (target) => emit('dismiss', target),
  signIn: (target) => emit('sign-in', target),
})

const comments = computed(() => (props.state.status === 'found' ? props.state.comments : []))
const roots = computed(() => nestComments(comments.value))
const count = computed(() => commentCount(comments.value))
const sortable = computed(() => props.state.status !== 'found' || roots.value.length > 0)
</script>

<template>
  <section class="comments" aria-labelledby="comments-title" data-testid="comments">
    <div class="comments__head">
      <h2 id="comments-title" class="comments__title">
        Комментарии
        <span
          v-if="state.status === 'found' && count > 0"
          class="comments__count"
          data-testid="comments-count"
          >{{ count }}</span
        >
      </h2>
      <div
        v-if="sortable"
        class="comments__sort"
        role="group"
        aria-label="Порядок комментариев"
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
      <span class="visually-hidden" role="status">Загрузка комментариев</span>
      <div v-for="n in 3" :key="n" class="comments__skeleton">
        <div class="comments__skeleton-mark" />
        <div class="comments__skeleton-lines">
          <div class="comments__skeleton-line" />
          <div class="comments__skeleton-line comments__skeleton-line--short" />
        </div>
      </div>
    </div>

    <div v-else-if="state.status === 'error'" class="comments__error" data-testid="comments-error">
      <p class="comments__muted" role="alert">Не удалось загрузить комментарии.</p>
      <UiButton variant="secondary" size="sm" @click="emit('retry')">Повторить</UiButton>
    </div>

    <template v-else>
      <CommentForm :inviting="roots.length === 0" />
      <p v-if="roots.length === 0" class="comments__muted" data-testid="comments-empty">
        Комментариев пока нет — будьте первым
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
