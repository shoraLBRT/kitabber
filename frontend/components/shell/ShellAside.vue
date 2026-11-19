<script setup lang="ts">
// Right column of the desktop shell (E02-05, docs/spec.md section 5): "Популярные комментарии" and "Интересные
// блоги". Their data comes with the feed widgets (FR-FEED); until then both widgets show the empty state.
import ShellWidget, { type WidgetState } from './ShellWidget.vue'

withDefaults(defineProps<{ commentsState?: WidgetState; blogsState?: WidgetState }>(), {
  commentsState: 'empty',
  blogsState: 'empty',
})
const emit = defineEmits<{ retry: [widget: 'comments' | 'blogs'] }>()
</script>

<template>
  <aside class="shell-aside" aria-label="Боковая колонка" data-testid="shell-aside">
    <ShellWidget
      title="Популярные комментарии"
      :state="commentsState"
      empty-text="Здесь появятся обсуждаемые комментарии"
      @retry="emit('retry', 'comments')"
    >
      <slot name="comments" />
    </ShellWidget>
    <ShellWidget
      title="Интересные блоги"
      :state="blogsState"
      empty-text="Здесь появятся блоги, на которые стоит подписаться"
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
