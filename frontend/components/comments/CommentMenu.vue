<script setup lang="ts">
// The «…» menu of a comment (E09-05, docs/ui/comments.md, ADR-0059): its items come from utils/comment-menu.ts. A
// dropdown menu (UiMenu) on a wide screen, a bottom sheet on a phone (narrower than 640 px), as the post's menu.
// «Удалить» asks first in a dialog; `remove` deletes, and the dialog stays open with a toast when it fails.
// «Редактировать» is passed on (`edit`): the comment opens its text in a form.
import { Ellipsis } from 'lucide-vue-next'
import { nextTick, ref } from 'vue'
import UiButton from '../ui/UiButton.vue'
import UiMenu from '../ui/UiMenu.vue'
import UiModal from '../ui/UiModal.vue'
import UiSheet from '../ui/UiSheet.vue'
import { phoneQuery, useMediaQuery } from '../../composables/useMediaQuery'
import { useToast } from '../../composables/useToast'
import type { CommentMenuAction, CommentMenuItem } from '../../utils/comment-menu'

const props = defineProps<{
  items: CommentMenuItem[]
  /** Deletes the comment; `false` when it failed. */
  remove: () => Promise<boolean>
}>()

const emit = defineEmits<{ edit: [] }>()

const label = 'Меню комментария'
const toast = useToast()
const isPhone = useMediaQuery(phoneQuery)
const sheetOpen = ref(false)
const confirmOpen = ref(false)
const deleting = ref(false)

// The confirmation opens only once the sheet is gone, so one dialog closes (and gives focus back) before the next.
async function choose(id: string) {
  const action = id as CommentMenuAction
  sheetOpen.value = false
  await nextTick()
  if (action === 'delete') confirmOpen.value = true
  else emit('edit')
}

async function confirmDelete() {
  if (deleting.value) return
  deleting.value = true
  try {
    if (await props.remove()) {
      confirmOpen.value = false
      toast.show('Комментарий удалён')
    } else toast.show('Не удалось удалить комментарий', { tone: 'error' })
  } finally {
    deleting.value = false
  }
}
</script>

<template>
  <div class="comment-menu" data-testid="comment-menu">
    <template v-if="isPhone">
      <button
        type="button"
        class="comment-menu__trigger"
        aria-haspopup="dialog"
        :aria-label="label"
        @click="sheetOpen = true"
      >
        <Ellipsis class="comment-menu__icon" aria-hidden="true" />
      </button>
      <UiSheet v-model:open="sheetOpen" :title="label">
        <ul class="comment-menu__sheet" data-testid="comment-menu-sheet">
          <li v-for="item in items" :key="item.id">
            <button
              type="button"
              class="comment-menu__sheet-item"
              :class="{ 'comment-menu__sheet-item--danger': item.danger }"
              :data-id="item.id"
              @click="choose(item.id)"
            >
              <component :is="item.icon" class="comment-menu__icon" aria-hidden="true" />
              {{ item.label }}
            </button>
          </li>
        </ul>
      </UiSheet>
    </template>
    <UiMenu v-else :items="items" :label="label" align="end" @select="choose">
      <template #trigger>
        <Ellipsis class="comment-menu__icon" aria-hidden="true" />
      </template>
    </UiMenu>

    <UiModal v-model:open="confirmOpen" title="Удалить комментарий?">
      <p class="comment-menu__text">
        Текст пропадёт. Если на комментарий ответили, на его месте останется «Комментарий удалён».
      </p>
      <template #footer>
        <UiButton variant="secondary" data-autofocus @click="confirmOpen = false">Отмена</UiButton>
        <UiButton
          variant="danger"
          :loading="deleting"
          data-testid="comment-delete-confirm"
          @click="confirmDelete"
        >
          Удалить
        </UiButton>
      </template>
    </UiModal>
  </div>
</template>

<style scoped>
.comment-menu {
  margin-left: auto;
}

.comment-menu__trigger {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: var(--size-touch);
  min-height: var(--size-touch);
  border: 0;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--color-muted);
  cursor: pointer;
}

.comment-menu__trigger:hover {
  background: var(--color-surface-subtle);
  color: var(--color-text);
}

.comment-menu__icon {
  width: var(--size-icon-md);
  height: var(--size-icon-md);
}

.comment-menu__sheet {
  display: flex;
  flex-direction: column;
  margin: 0;
  padding: 0;
  list-style: none;
}

.comment-menu__sheet-item {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  width: 100%;
  min-height: var(--size-touch);
  padding: 0 var(--space-2);
  border: 0;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--color-text);
  font: inherit;
  text-align: start;
  cursor: pointer;
}

.comment-menu__sheet-item:hover {
  background: var(--color-surface-subtle);
}

.comment-menu__sheet-item--danger {
  color: var(--color-danger);
}

.comment-menu__text {
  margin: 0;
}
</style>
