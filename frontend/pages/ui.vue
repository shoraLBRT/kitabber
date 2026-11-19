<script setup lang="ts">
// Showcase of the base UI components (E02-04, ADR-0013).
import brand from '../../brand/brand.json'

useHead({ title: `Компоненты — ${brand.name}` })
</script>

<template>
  <main class="ui-page">
    <h1>Базовые компоненты</h1>
    <p>
      Кнопка, поле, меню, вкладки, модальное окно, шторка, тост и аватар со всеми состояниями. Всё управляется
      с клавиатуры; тема переключается вкладками сверху.
    </p>
    <UiShowcase />
  </main>
</template>

<style scoped>
.ui-page {
  max-width: calc(var(--size-menu) + var(--size-feed) + var(--size-aside));
  padding: var(--space-4);
}
</style>
