<script setup lang="ts">
// Design token showcase (E02-02): every colour of both palettes, the type scale with a long-read sample,
// the spacing scale, radii and sizes. Everything is read from brand/tokens.ts, so a new token shows up here.
import { breakpoints, colors, scales, themes } from '../../brand/tokens'

const scale = scales()
const pick = (prefix: string) => Object.keys(scale).filter((name) => name.startsWith(prefix))
const fontSizes = pick('--font-size-')
const spaces = pick('--space-')
const radii = pick('--radius-')
const others = Object.keys(scale).filter(
  (name) => !['--font-size-', '--space-', '--radius-'].some((prefix) => name.startsWith(prefix)),
)
const themeTitle = { light: 'Светлая тема', dark: 'Тёмная тема' } as const
</script>

<template>
  <div class="tokens">
    <section
      v-for="theme in themes"
      :key="theme"
      class="tokens__theme"
      :data-theme="theme"
      :data-testid="`colors-${theme}`"
    >
      <h2>{{ themeTitle[theme] }}</h2>
      <ul class="tokens__swatches">
        <li v-for="(hex, name) in colors(theme)" :key="name" class="tokens__swatch-item">
          <span class="tokens__swatch" :style="{ background: `var(${name})` }" />
          <code>{{ name }}</code>
          <code class="tokens__muted">{{ hex }}</code>
        </li>
      </ul>
      <article class="tokens__reading">
        <h3>Типографика для долгого чтения</h3>
        <p>
          Основной текст лонгрида набран кеглем <code>--font-size-reading</code> с интерлиньяжем
          <code>--line-height-reading</code>, а строка не шире <code>--size-measure</code>: так глазу легко
          переходить со строки на строку и не теряться в длинных абзацах.
        </p>
        <p class="tokens__muted">Второстепенный текст: подписи, даты, счётчики.</p>
        <p>
          <a href="#tokens-type">Ссылка</a> ·
          <span class="tokens__status tokens__status--success">Опубликовано</span> ·
          <span class="tokens__status tokens__status--warning">На модерации</span> ·
          <span class="tokens__status tokens__status--danger">Ошибка</span>
        </p>
      </article>
    </section>

    <section id="tokens-type" data-testid="type-scale">
      <h2>Кегли</h2>
      <p
        v-for="name in fontSizes"
        :key="name"
        class="tokens__type-sample"
        :style="{ fontSize: `var(${name})` }"
      >
        <code>{{ name }}</code> {{ scale[name] }} — Съешь же ещё этих мягких французских булок
      </p>
    </section>

    <section data-testid="space-scale">
      <h2>Отступы</h2>
      <ul class="tokens__list">
        <li v-for="name in spaces" :key="name" class="tokens__space-item">
          <span class="tokens__bar" :style="{ width: `var(${name})` }" />
          <code>{{ name }}</code> {{ scale[name] }}
        </li>
      </ul>
    </section>

    <section data-testid="radius-scale">
      <h2>Скругления</h2>
      <ul class="tokens__list tokens__list--row">
        <li
          v-for="name in radii"
          :key="name"
          class="tokens__radius"
          :style="{ borderRadius: `var(${name})` }"
        >
          <code>{{ name }}</code>
        </li>
      </ul>
    </section>

    <section data-testid="other-tokens">
      <h2>Шрифты, начертания и размеры</h2>
      <table class="tokens__table">
        <tbody>
          <tr v-for="name in others" :key="name">
            <td>
              <code>{{ name }}</code>
            </td>
            <td>{{ scale[name] }}</td>
          </tr>
        </tbody>
      </table>
      <p>
        Границы раскладок (в медиазапросах переменные не работают, поэтому это числа из
        <code>brand/tokens.json</code>):
        <span v-for="(px, name) in breakpoints" :key="name"> {{ name }} — {{ px }} px; </span>
      </p>
    </section>
  </div>
</template>

<style scoped>
.tokens {
  display: grid;
  gap: var(--space-6);
}

.tokens__theme {
  padding: var(--space-4);
  border: var(--size-border) solid var(--color-border);
  border-radius: var(--radius-lg);
  background: var(--color-background);
  color: var(--color-text);
}

.tokens__swatches,
.tokens__list {
  display: grid;
  gap: var(--space-2);
  margin: 0;
  padding: 0;
  list-style: none;
}

.tokens__list--row {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-4);
}

.tokens__swatch-item {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
}

.tokens__swatch {
  width: var(--size-icon-md);
  height: var(--size-icon-md);
  border: var(--size-border) solid var(--color-border-strong);
  border-radius: var(--radius-sm);
}

.tokens__muted {
  color: var(--color-muted);
}

.tokens__reading {
  max-width: var(--size-measure);
  margin-top: var(--space-6);
  padding: var(--space-4);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  font-family: var(--font-family-serif);
  font-size: var(--font-size-reading);
  line-height: var(--line-height-reading);
}

.tokens__reading h3 {
  font-family: var(--font-family-sans);
  font-size: var(--font-size-xl);
  line-height: var(--line-height-tight);
}

.tokens__status--success {
  color: var(--color-success);
}

.tokens__status--warning {
  color: var(--color-warning);
}

.tokens__status--danger {
  color: var(--color-danger);
}

.tokens__type-sample {
  margin: 0 0 var(--space-2);
  overflow-wrap: anywhere;
}

.tokens__space-item {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.tokens__bar {
  height: var(--space-4);
  background: var(--color-accent);
}

.tokens__radius {
  padding: var(--space-4);
  border: var(--size-border) solid var(--color-border-strong);
  background: var(--color-surface-subtle);
}

.tokens__table {
  border-collapse: collapse;
}

.tokens__table td {
  padding: var(--space-1) var(--space-3);
  border-bottom: var(--size-border) solid var(--color-border);
  overflow-wrap: anywhere;
}
</style>
