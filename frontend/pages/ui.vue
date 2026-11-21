<script setup lang="ts">
// Showcase of the base UI components (E02-04, ADR-0013), and of the sign-in window for a guest's action (E03-04):
// the likes of posts come with the reactions, so "Нравится" here stands for every such button until then.
import brand from '../../brand/brand.json'
import UiButton from '~/components/ui/UiButton.vue'

useHead({ title: `Компоненты — ${brand.name}` })
definePageMeta({ layout: 'bare' })

const liked = ref(false)
const requireAccount = useRequireAccount()

function like() {
  requireAccount('like:showcase', () => (liked.value = !liked.value))
}

onSignedInAgain('like:showcase', () => (liked.value = true))
</script>

<template>
  <main class="ui-page">
    <h1>Базовые компоненты</h1>
    <p>
      Кнопка, поле, меню, вкладки, модальное окно, шторка, тост и аватар со всеми состояниями. Всё управляется
      с клавиатуры; тема переключается вкладками сверху.
    </p>
    <UiShowcase />
    <section class="ui-page__section" data-testid="showcase-auth-gate">
      <h2>Окно входа — действие гостя</h2>
      <p>Гость, нажавший «Нравится», видит окно входа, а после входа отметка ставится сама.</p>
      <UiButton
        :variant="liked ? 'primary' : 'secondary'"
        :aria-pressed="liked ? 'true' : 'false'"
        @click="like"
        >Нравится</UiButton
      >
    </section>
  </main>
</template>

<style scoped>
.ui-page {
  max-width: calc(var(--size-menu) + var(--size-feed) + var(--size-aside));
  padding: var(--space-4);
}

.ui-page__section {
  margin-top: var(--space-8);
}
</style>
