<script setup lang="ts">
// New post (E05-01, docs/ui/editor.md): the editor window over the shell. Only the editing core so far: block types,
// the block menu, drafts and publishing come with their own tasks (E05-02…E05-10, #48, #49). Closing goes back.
import { Redo2, Undo2 } from 'lucide-vue-next'
import BlockEditor from '~/components/editor/BlockEditor.vue'
import EditorWindow from '~/components/editor/EditorWindow.vue'
import { emptyDocument } from '~/editor/document'

useHead({ title: 'Новый пост' })

const document = ref(emptyDocument())
const editor = ref<InstanceType<typeof BlockEditor>>()
const router = useRouter()

function close() {
  if (window.history.state?.back) router.back()
  else void navigateTo('/')
}
</script>

<template>
  <EditorWindow title="Новый пост" @close="close">
    <template #actions>
      <button
        type="button"
        class="editor-window__button"
        aria-label="Отменить"
        title="Отменить (Ctrl+Z)"
        data-testid="editor-undo"
        :disabled="!editor?.canUndo"
        @click="editor?.undo()"
      >
        <Undo2 aria-hidden="true" />
      </button>
      <button
        type="button"
        class="editor-window__button"
        aria-label="Повторить"
        title="Повторить (Ctrl+Shift+Z)"
        data-testid="editor-redo"
        :disabled="!editor?.canRedo"
        @click="editor?.redo()"
      >
        <Redo2 aria-hidden="true" />
      </button>
    </template>
    <BlockEditor ref="editor" v-model:document="document" />
  </EditorWindow>
</template>
