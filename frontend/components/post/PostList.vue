<script setup lang="ts">
// A list block of a post (E07-03): `ul` or `ol`, a nested item is a list inside its parent's `li` (ADR-0033).
// The list is `dir="auto"` (E02-14, ADR-0068): it takes the direction of its first item, and its items follow it, so an
// Arabic list puts its markers and its indent on the right.
import type { ListNode } from '../../utils/post-blocks'

defineProps<{ ordered: boolean; items: ListNode[]; nested?: boolean }>()
</script>

<!-- eslint-disable vue/no-v-html -- every text is cleaned by cleanInlineHtml (ADR-0023, ADR-0045) -->
<template>
  <component
    :is="ordered ? 'ol' : 'ul'"
    class="post-list"
    :class="{ 'post-list--nested': nested }"
    dir="auto"
  >
    <li v-for="(item, index) in items" :key="index" class="post-list__item">
      <span v-html="item.html" />
      <PostList v-if="item.children.length > 0" :ordered="ordered" :items="item.children" nested />
    </li>
  </component>
</template>

<style scoped>
.post-list {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  margin: 0;
  padding-inline-start: var(--space-6);
}

.post-list--nested {
  margin-block-start: var(--space-1);
}

ul.post-list--nested {
  list-style-type: circle;
}

ol.post-list--nested {
  list-style-type: lower-alpha;
}

.post-list__item {
  overflow-wrap: anywhere;
}
</style>
