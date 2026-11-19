<script setup lang="ts">
// Design token showcase page (E02-02, ADR-0012).
import brand from '../../brand/brand.json'

useHead({ title: `Дизайн-токены — ${brand.name}` })
definePageMeta({ layout: 'bare' })
</script>

<template>
  <main class="tokens-page">
    <h1>Дизайн-токены</h1>
    <p>
      Цвета, типографика и отступы обеих тем. Источник — <code>brand/brand.json</code> и
      <code>brand/tokens.json</code>; компоненты берут значения только из CSS-переменных.
    </p>
    <TokenShowcase />
  </main>
</template>

<style scoped>
.tokens-page {
  padding: var(--space-4);
}
</style>
