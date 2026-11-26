<script setup lang="ts">
// Right column of the desktop shell (E02-05, docs/spec.md section 5): "Популярные комментарии" and "Интересные
// блоги". Their data comes with the feed widgets (FR-FEED); until then both widgets show the empty state.
import ShellWidget, { type WidgetState } from './ShellWidget.vue'
import { t } from '../../i18n'

withDefaults(defineProps<{ commentsState?: WidgetState; blogsState?: WidgetState }>(), {
  commentsState: 'empty',
  blogsState: 'empty',
})
const emit = defineEmits<{ retry: [widget: 'comments' | 'blogs'] }>()
</script>

<template>
  <aside class="shell-aside" :aria-label="t('shell.aside')" data-testid="shell-aside">
    <ShellWidget
      :title="t('shell.popularComments')"
      :state="commentsState"
      :empty-text="t('shell.popularCommentsEmpty')"
      @retry="emit('retry', 'comments')"
    >
      <slot name="comments" />
    </ShellWidget>
    <ShellWidget
      :title="t('shell.blogs')"
      :state="blogsState"
      :empty-text="t('shell.blogsEmpty')"
      @retry="emit('retry', 'blogs')"
    >
      <slot name="blogs" />
    </ShellWidget>
  </aside>
</template>

<style scoped>
.shell-aside {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}
</style>
