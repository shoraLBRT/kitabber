<script setup lang="ts">
// A tab of the section «Закладки» (E04-12, ADR-0116): the list of BookmarkSectionList.vue read by useBookmarkSection.
import BookmarkSectionList from './BookmarkSectionList.vue'
import type { BookmarkSectionTab } from '../../api/bookmark-section'

const props = defineProps<{ tab: BookmarkSectionTab }>()

const { state, authors, loadingMore, moreFailed, loadMore, retry } = useBookmarkSection(props.tab)
</script>

<template>
  <BookmarkSectionList
    :tab="tab"
    :state="state"
    :authors="authors"
    :loading-more="loadingMore"
    :more-failed="moreFailed"
    @retry="retry()"
    @more="loadMore()"
  />
</template>
