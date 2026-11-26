<script setup lang="ts">
// The set of reactions the platform offers (E02-12, FR-REACT, ADR-0095), as the directory gives it: each picture with
// its name, in the directory's order. Loading shows placeholders, a failure says so with «Повторить», an empty
// directory says that no reactions are offered. Choosing one of them is E10-02.
import { computed } from 'vue'
import ReactionIcon from './ReactionIcon.vue'
import UiButton from '../ui/UiButton.vue'
import UiSkeleton from '../ui/UiSkeleton.vue'
import UiState from '../ui/UiState.vue'
import type { ReactionSetItem } from '../../api/reactions'
import { reactionGraphic } from '../../utils/reaction-graphics'
import { t } from '../../i18n'

export type ReactionSetState =
  { status: 'loading' } | { status: 'error' } | { status: 'found'; items: ReactionSetItem[] }

const props = defineProps<{ state: ReactionSetState }>()
const emit = defineEmits<{ retry: [] }>()

// A reaction this build has no picture for (a newer directory) is left out rather than drawn blank.
const shown = computed(() =>
  props.state.status === 'found' ? props.state.items.filter((item) => reactionGraphic(item.code)) : [],
)
</script>

<template>
  <div class="reaction-set" data-testid="reaction-set">
    <div
      v-if="state.status === 'loading'"
      class="reaction-set__list"
      aria-busy="true"
      data-testid="reaction-set-loading"
    >
      <span class="visually-hidden" role="status">{{ t('reactions.set.loading') }}</span>
      <span v-for="n in 7" :key="n" class="reaction-set__item">
        <UiSkeleton shape="block" width="40px" height="40px" />
        <UiSkeleton size="sm" width="4em" />
      </span>
    </div>

    <UiState v-else-if="state.status === 'error'" tone="error">
      {{ t('reactions.set.failed') }}
      <template #actions>
        <UiButton variant="secondary" size="sm" @click="emit('retry')">{{ t('common.retry') }}</UiButton>
      </template>
    </UiState>

    <UiState v-else-if="shown.length === 0">{{ t('reactions.set.empty') }}</UiState>

    <ul v-else class="reaction-set__list" :aria-label="t('reactions.set.list')">
      <li v-for="item in shown" :key="item.code" class="reaction-set__item" data-testid="reaction-set-item">
        <ReactionIcon :code="item.code" size="lg" />
        <span class="reaction-set__title">{{ item.title }}</span>
      </li>
    </ul>
  </div>
</template>

<style scoped>
.reaction-set__list {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-3);
  margin: 0;
  padding: var(--space-3);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  list-style: none;
}

.reaction-set__item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-2);
  min-width: calc(var(--space-8) * 2);
  padding: var(--space-2);
}

.reaction-set__title {
  color: var(--color-text);
  font-size: var(--font-size-sm);
  text-align: center;
}
</style>
