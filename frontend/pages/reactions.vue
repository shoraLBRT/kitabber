<script setup lang="ts">
// Showcase of the set of reactions (E02-12, ADR-0095): the reactions the directory offers, each with its picture, in
// either theme, and the credit the pictures' licence asks for. The set is read in the browser, so the page shows the
// loading, empty and error states as the product will.
import brand from '../../brand/brand.json'
import ReactionSet, { type ReactionSetState } from '~/components/reactions/ReactionSet.vue'
import { loadReactionSet } from '~/api/reactions'
import { reactionGraphicsSource } from '~/utils/reaction-graphics'
import { t } from '~/i18n'

useHead({ title: `Реакции — ${brand.name}` })
definePageMeta({ layout: 'bare' })

const { $api } = useNuxtApp()
const { data, status, refresh } = await useAsyncData('reaction-set', () => loadReactionSet($api), {
  server: false,
})

const state = computed<ReactionSetState>(() => {
  if (status.value === 'pending' || (status.value === 'idle' && !data.value)) return { status: 'loading' }
  const lookup = data.value
  if (!lookup || lookup.status !== 'found') return { status: 'error' }
  return { status: 'found', items: lookup.items }
})
</script>

<template>
  <main class="reactions-page">
    <h1>Набор реакций</h1>
    <p>
      Сдержанный набор без насмешек и пошлости. Его задаёт справочник реакций; каждая реакция рисуется своей
      картинкой, поэтому выглядит одинаково на всех системах.
    </p>
    <ReactionSet :state="state" @retry="refresh()" />
    <p class="reactions-page__credit" data-testid="reaction-credit">
      {{ t('reactions.set.credit', reactionGraphicsSource) }}
      <a :href="reactionGraphicsSource.url" rel="noopener" target="_blank">{{
        reactionGraphicsSource.url
      }}</a>
    </p>
  </main>
</template>

<style scoped>
.reactions-page {
  max-width: var(--size-feed);
  padding: var(--space-4);
}

.reactions-page__credit {
  margin-top: var(--space-4);
  color: var(--color-muted);
  font-size: var(--font-size-sm);
  overflow-wrap: anywhere;
}
</style>
