<script setup lang="ts">
// Brand showcase (E02-01): the logo, the palette and sample icons in both themes.
import { Bell, House, MessageCircle, PenLine, Search, User } from 'lucide-vue-next'
import brand from '../../brand/brand.json'

const themes = ['light', 'dark'] as const
const icons = {
  Главная: House,
  Поиск: Search,
  Написать: PenLine,
  Уведомления: Bell,
  Комментарии: MessageCircle,
  Профиль: User,
}

useHead({ title: `Бренд — ${brand.name}` })
definePageMeta({ layout: 'bare' })
</script>

<template>
  <main class="showcase">
    <h1>Бренд</h1>
    <section v-for="theme in themes" :key="theme" class="showcase__theme" :data-theme="theme">
      <h2>{{ theme === 'light' ? 'Светлая тема' : 'Тёмная тема' }}</h2>
      <BrandLogo :theme="theme" />
      <ul class="showcase__swatches">
        <li v-for="(hex, role) in brand.palette[theme]" :key="role">
          <span class="showcase__swatch" :style="{ background: hex }" />
          <code>{{ role }} {{ hex }}</code>
        </li>
      </ul>
      <ul class="showcase__icons">
        <li v-for="(icon, label) in icons" :key="label" :title="label">
          <component :is="icon" :aria-label="label" />
        </li>
      </ul>
    </section>
    <p>Иконки: {{ brand.icons.package }} ({{ brand.icons.license }}), {{ brand.icons.source }}</p>
  </main>
</template>

<style scoped>
.showcase {
  display: grid;
  gap: var(--space-4);
  padding: var(--space-4);
}

.showcase__theme {
  padding: var(--space-4);
  border-radius: var(--radius-lg);
  background: var(--color-background);
  color: var(--color-text);
}

.showcase__icons {
  color: var(--color-accent);
}

.showcase__swatches,
.showcase__icons {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-3);
  padding: 0;
  list-style: none;
}

.showcase__swatch {
  display: inline-block;
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
  margin-right: var(--space-1);
  border: var(--size-border) solid currentColor;
  vertical-align: middle;
}
</style>
