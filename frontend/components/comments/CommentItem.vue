<script setup lang="ts">
// One comment of the tree (E09-02, docs/ui/comments.md) and, nested under it, the replies that were read. The branch
// line beside the replies is the button that folds them; a folded branch leaves «Развернуть · N ответов». Replies a
// read left out come on «Показать ещё N ответов», with a skeleton line while they load and «Повторить» on a failure.
// «Ответить» opens the reply form under the comment (E09-03); a reply being sent is dimmed until the server keeps it.
// The person's own comment has the «…» menu (E09-05, ADR-0059): «Редактировать» opens its text in a form while the time
// lasts, «Удалить» asks first. A changed text is marked «изменено»; a deleted comment with replies is the stub
// «Комментарий удалён», with no author, no text and no actions, over its replies.
import { computed, inject, ref, watch } from 'vue'
import UiAvatar from '../ui/UiAvatar.vue'
import CommentEdit from './CommentEdit.vue'
import CommentForm from './CommentForm.vue'
import CommentMenu from './CommentMenu.vue'
import { blogPath, blogTitle } from '../../api/profiles'
import { canReply, repliesText, type CommentNode } from '../../api/comments'
import { commentMenuItems } from '../../utils/comment-menu'
import { commentTreeKey } from './context'

const props = defineProps<{ node: CommentNode }>()

const tree = inject(commentTreeKey)!
const expanded = ref(true)

const comment = computed(() => props.node.comment)
const sending = computed(() => tree.pending.value[comment.value.id] === true)
const replying = ref(tree.resume.value === props.node.comment.id)
const replyable = computed(() => !sending.value && canReply(comment.value, tree.maxDepth.value))
const editing = ref(false)
const menuItems = computed(() =>
  sending.value
    ? []
    : commentMenuItems(comment.value, tree.own.value[comment.value.id] === true, tree.now.value),
)
const profile = computed(() =>
  comment.value.authorAccountId ? tree.authors.value.get(comment.value.authorAccountId) : undefined,
)
// A guest comment names its outside author (FR-TG-6); a native one its blog, once the profile is read; the own comment
// being sent is «Вы» (the page does not know its own account, FR-ID).
const name = computed(() =>
  comment.value.guest
    ? comment.value.guest.name
    : profile.value
      ? blogTitle(profile.value)
      : sending.value
        ? 'Вы'
        : 'Автор',
)

function startReply() {
  if (tree.signedIn.value) replying.value = true
  else tree.signIn(comment.value.id)
}

// A reply sent goes into this branch, shown; a reply that failed opens the form again with its text.
function closeReply() {
  replying.value = false
  expanded.value = true
}
watch(
  () => tree.failures.value[comment.value.id],
  (failure) => {
    if (failure) replying.value = true
  },
)
const branch = computed(() => tree.branches.value[comment.value.id])
const titleId = computed(() => `comment-${comment.value.id}`)

function remove() {
  return tree.remove(comment.value.id)
}

const timeFormat = new Intl.DateTimeFormat('ru', {
  day: 'numeric',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
})
</script>

<template>
  <li
    class="comment"
    :class="{ 'comment--sending': sending }"
    :aria-level="comment.depth"
    :aria-labelledby="titleId"
    :aria-busy="sending ? 'true' : undefined"
    data-testid="comment"
    :data-comment-id="comment.id"
  >
    <p v-if="comment.deleted" :id="titleId" class="comment__deleted" data-testid="comment-deleted">
      Комментарий удалён
    </p>
    <div v-else class="comment__head">
      <UiAvatar
        :name="name"
        :src="profile?.avatarUrl ?? undefined"
        :sources="profile?.avatarSources"
        size="sm"
        decorative
      />
      <NuxtLink
        v-if="profile"
        :id="titleId"
        :to="blogPath(profile)"
        class="comment__author"
        data-testid="comment-author"
        >{{ name }}</NuxtLink
      >
      <a
        v-else-if="comment.guest?.url"
        :id="titleId"
        :href="comment.guest.url"
        class="comment__author"
        rel="nofollow ugc noopener noreferrer"
        target="_blank"
        data-testid="comment-author"
        >{{ name }}</a
      >
      <span v-else :id="titleId" class="comment__author" data-testid="comment-author">{{ name }}</span>
      <span v-if="sending" class="comment__time" data-testid="comment-sending">Отправляется…</span>
      <time v-else class="comment__time" :datetime="comment.createdAt">{{
        timeFormat.format(new Date(comment.createdAt))
      }}</time>
      <span
        v-if="comment.editedAt && !sending"
        class="comment__time"
        :title="`Изменено ${timeFormat.format(new Date(comment.editedAt))}`"
        data-testid="comment-edited"
        >· изменено</span
      >
      <CommentMenu v-if="menuItems.length > 0" :items="menuItems" :remove="remove" @edit="editing = true" />
    </div>
    <CommentEdit
      v-if="editing && !comment.deleted"
      :comment-id="comment.id"
      :text="comment.text"
      @close="editing = false"
    />
    <p v-else-if="!comment.deleted" class="comment__text" data-testid="comment-text">{{ comment.text }}</p>
    <div v-if="replyable && !replying && !editing" class="comment__actions">
      <button type="button" class="comment__link" data-testid="comment-reply" @click="startReply">
        Ответить
      </button>
    </div>
    <CommentForm v-if="replying" :parent-id="comment.id" @close="closeReply" />

    <div v-if="node.replies.length > 0 && expanded" class="comment__branch">
      <button
        type="button"
        class="comment__line"
        aria-expanded="true"
        :aria-label="`Свернуть ветку: ${repliesText(comment.replies)}`"
        data-testid="comment-collapse"
        @click="expanded = false"
      />
      <ul class="comment__replies">
        <CommentItem v-for="reply in node.replies" :key="reply.comment.id" :node="reply" />
      </ul>
    </div>
    <button
      v-else-if="node.replies.length > 0"
      type="button"
      class="comment__link"
      aria-expanded="false"
      data-testid="comment-expand"
      @click="expanded = true"
    >
      Развернуть · {{ repliesText(comment.replies) }}
    </button>

    <template v-if="node.hidden > 0 && expanded">
      <div
        v-if="branch === 'loading'"
        class="comment__skeleton"
        aria-busy="true"
        data-testid="comment-more-loading"
      >
        <span class="visually-hidden" role="status">Загрузка ответов</span>
      </div>
      <p v-else class="comment__more">
        <span v-if="branch === 'error'" class="comment__failed" role="alert"
          >Не удалось загрузить ответы.</span
        >
        <button type="button" class="comment__link" data-testid="comment-more" @click="tree.more(comment.id)">
          {{ branch === 'error' ? 'Повторить' : `Показать ещё ${repliesText(node.hidden)}` }}
        </button>
      </p>
    </template>
  </li>
</template>

<style scoped>
.comment {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  min-width: 0;
}

.comment__head {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  min-width: 0;
}

.comment__author {
  min-width: 0;
  color: var(--color-text);
  font-weight: var(--font-weight-medium);
  overflow-wrap: anywhere;
  text-decoration: none;
}

a.comment__author:hover {
  text-decoration: underline;
}

.comment__time {
  flex: none;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.comment__deleted {
  margin: 0;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
  font-style: italic;
}

.comment--sending .comment__text {
  color: var(--color-muted);
}

.comment__actions {
  display: flex;
  gap: var(--space-3);
}

.comment__text {
  margin: 0;
  font-size: var(--font-size-md);
  overflow-wrap: anywhere;
  white-space: pre-line;
}

/* The replies sit one indent to the right; the line on the left of the indent folds them. */
.comment__branch {
  display: flex;
  min-width: 0;
  margin-top: var(--space-2);
}

.comment__line {
  flex: none;
  width: var(--space-8);
  padding: 0;
  border: 0;
  background: linear-gradient(var(--color-border), var(--color-border)) center / var(--size-border) 100%
    no-repeat;
  cursor: pointer;
}

.comment__line:hover {
  background-image: linear-gradient(var(--color-muted), var(--color-muted));
}

.comment__line:focus-visible {
  outline: var(--size-border) solid var(--color-accent);
  outline-offset: calc(-1 * var(--size-border));
}

.comment__replies {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: var(--space-4);
  min-width: 0;
  margin: 0;
  padding: 0;
  list-style: none;
}

.comment__more {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  margin: 0;
  padding-left: var(--space-8);
}

.comment__link {
  align-self: flex-start;
  padding: 0;
  border: 0;
  background: none;
  color: var(--color-accent);
  font: inherit;
  font-size: var(--font-size-sm);
  cursor: pointer;
}

.comment__link:hover {
  text-decoration: underline;
}

.comment__failed {
  color: var(--color-danger);
  font-size: var(--font-size-sm);
}

.comment__skeleton {
  width: 40%;
  height: var(--space-4);
  margin-left: var(--space-8);
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}

@media (max-width: 639px) {
  .comment__line {
    width: var(--space-4);
  }

  .comment__more {
    padding-left: var(--space-4);
  }

  .comment__skeleton {
    margin-left: var(--space-4);
  }
}
</style>
