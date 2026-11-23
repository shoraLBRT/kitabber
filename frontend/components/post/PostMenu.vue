<script setup lang="ts">
// The «…» menu of the post page (E07-05, FR-POST, docs/ui/post.md, ADR-0053): the author's items for their own post
// (edit, delete), a reader's for anyone else's (report, hide, copy the link) — utils/post-menu.ts decides which, and
// keeps the items whose feature is not built yet behind their flags. A dropdown menu (UiMenu) on a wide screen, a
// bottom sheet on a phone (narrower than 640 px). Deleting asks first in a dialog; `remove` deletes, and the dialog
// stays open with a toast when it fails. The flagged items are only passed on (`action`) to the tasks that build them.
import { Ellipsis } from 'lucide-vue-next'
import { computed, ref } from 'vue'
import UiButton from '../ui/UiButton.vue'
import UiMenu from '../ui/UiMenu.vue'
import UiModal from '../ui/UiModal.vue'
import UiSheet from '../ui/UiSheet.vue'
import { phoneQuery, useMediaQuery } from '../../composables/useMediaQuery'
import { useToast } from '../../composables/useToast'
import {
  copyPostLink,
  noPostMenuFeatures,
  postMenuItems,
  type PostMenuAction,
  type PostMenuFeatures,
} from '../../utils/post-menu'

const props = withDefaults(
  defineProps<{
    own: boolean
    path: string
    features?: PostMenuFeatures
    /** Deletes the post; `true` once it is deleted (the page then leaves), `false` when it failed. */
    remove?: () => Promise<boolean>
  }>(),
  { features: () => noPostMenuFeatures, remove: undefined },
)

const emit = defineEmits<{ action: [action: Exclude<PostMenuAction, 'delete' | 'copy-link'>] }>()

const label = 'Меню поста'
const toast = useToast()
const isPhone = useMediaQuery(phoneQuery)
const items = computed(() => postMenuItems(props.own, props.features))
const sheetOpen = ref(false)
const confirmOpen = ref(false)
const deleting = ref(false)

function choose(id: string) {
  sheetOpen.value = false
  const action = id as PostMenuAction
  if (action === 'copy-link') void copyPostLink(props.path, toast)
  else if (action === 'delete') confirmOpen.value = true
  else emit('action', action)
}

async function confirmDelete() {
  if (!props.remove || deleting.value) return
  deleting.value = true
  try {
    if (await props.remove()) confirmOpen.value = false
    else toast.show('Не удалось удалить пост', { tone: 'error' })
  } finally {
    deleting.value = false
  }
}
</script>

<template>
  <div class="post-menu" data-testid="post-menu">
    <template v-if="isPhone">
      <button
        type="button"
        class="post-menu__trigger"
        aria-haspopup="dialog"
        :aria-label="label"
        @click="sheetOpen = true"
      >
        <Ellipsis class="post-menu__icon" aria-hidden="true" />
      </button>
      <UiSheet v-model:open="sheetOpen" :title="label">
        <ul class="post-menu__sheet" data-testid="post-menu-sheet">
          <li v-for="item in items" :key="item.id">
            <button
              type="button"
              class="post-menu__sheet-item"
              :class="{ 'post-menu__sheet-item--danger': item.danger }"
              :data-id="item.id"
              @click="choose(item.id)"
            >
              <component :is="item.icon" class="post-menu__icon" aria-hidden="true" />
              {{ item.label }}
            </button>
          </li>
        </ul>
      </UiSheet>
    </template>
    <UiMenu v-else :items="items" :label="label" align="end" @select="choose">
      <template #trigger>
        <Ellipsis class="post-menu__icon" aria-hidden="true" />
      </template>
    </UiMenu>

    <UiModal v-model:open="confirmOpen" title="Удалить пост?">
      <p class="post-menu__text">Пост пропадёт из блога и лент, а его адрес перестанет открываться.</p>
      <template #footer>
        <UiButton variant="secondary" data-autofocus @click="confirmOpen = false">Отмена</UiButton>
        <UiButton
          variant="danger"
          :loading="deleting"
          data-testid="post-delete-confirm"
          @click="confirmDelete"
        >
          Удалить
        </UiButton>
      </template>
    </UiModal>
  </div>
</template>

<style scoped>
.post-menu {
  margin-left: auto;
}

.post-menu__trigger {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: var(--size-touch);
  min-height: var(--size-touch);
  border: 0;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--color-text);
  cursor: pointer;
}

.post-menu__trigger:hover {
  background: var(--color-surface-subtle);
}

.post-menu__icon {
  width: var(--size-icon-md);
  height: var(--size-icon-md);
}

.post-menu__sheet {
  display: flex;
  flex-direction: column;
  margin: 0;
  padding: 0;
  list-style: none;
}

.post-menu__sheet-item {
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

.post-menu__sheet-item:hover {
  background: var(--color-surface-subtle);
}

.post-menu__sheet-item--danger {
  color: var(--color-danger);
}

.post-menu__text {
  margin: 0;
}
</style>
