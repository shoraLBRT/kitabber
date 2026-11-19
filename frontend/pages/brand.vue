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
</script>

<template>
  <main class="showcase">
    <section
      v-for="theme in themes"
      :key="theme"
      class="showcase__theme"
      :data-theme="theme"
      :style="{ background: brand.palette[theme].background, color: brand.palette[theme].text }"
    >
      <h2>{{ theme === 'light' ? 'Светлая тема' : 'Тёмная тема' }}</h2>
      <BrandLogo :theme="theme" />
      <ul class="showcase__swatches">
        <li v-for="(hex, role) in brand.palette[theme]" :key="role">
          <span class="showcase__swatch" :style="{ background: hex }" />
          <code>{{ role }} {{ hex }}</code>
        </li>
      </ul>
      <ul class="showcase__icons" :style="{ color: brand.palette[theme].accent }">
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
  gap: 1rem;
  padding: 1rem;
}

.showcase__theme {
  padding: 1rem;
  border-radius: 0.75rem;
}

.showcase__swatches,
.showcase__icons {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  padding: 0;
  list-style: none;
}

.showcase__swatch {
  display: inline-block;
  width: 1rem;
  height: 1rem;
  margin-right: 0.25rem;
  border: 1px solid currentColor;
  vertical-align: middle;
}
</style>
