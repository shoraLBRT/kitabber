<script setup lang="ts">
// Bottom bar of the phone shell (E02-06, docs/spec.md section 5, docs/ui/README.md): "Лента", "Рейтинг",
// "Уведомления", "Сообщения", "Меню" with unread counters on the icons. Shown below breakpoint.md (925 px), where
// the left menu is hidden. Respects the safe areas of the screen; every tab is at least --size-touch (44 px) tall.
// A second tap on the current tab scrolls the page to the top (docs/ui/README.md, "Кнопка «наверх»").
import { isActive, tabBarNav, type NavItem } from '../../utils/navigation'
import { t } from '../../i18n'

const props = withDefaults(
  defineProps<{
    /** Path of the current route. */
    currentPath: string
    /** Unread counters by tab id, e.g. { notifications: 3 }; empty until notifications and messages exist. */
    counts?: Partial<Record<string, number>>
  }>(),
  { counts: () => ({}) },
)

const countOf = (item: NavItem) => props.counts[item.id] ?? 0
const badge = (count: number) => (count > 99 ? '99+' : String(count))

function onClick(item: NavItem, event: MouseEvent) {
  if (!isActive(item, props.currentPath)) return
  event.preventDefault()
  window.scrollTo({ top: 0, behavior: 'smooth' })
}
</script>

<template>
  <nav class="tab-bar" :aria-label="t('shell.tabBar')" data-testid="tab-bar">
    <ul class="tab-bar__list">
      <li v-for="item in tabBarNav" :key="item.id" class="tab-bar__item">
        <NuxtLink
          :to="item.to"
          class="tab-bar__link"
          :aria-current="isActive(item, currentPath) ? 'page' : undefined"
          :data-nav="item.id"
          @click="onClick(item, $event)"
        >
          <span class="tab-bar__icon-wrap">
            <component :is="item.icon" class="tab-bar__icon" aria-hidden="true" />
            <span v-if="countOf(item) > 0" class="tab-bar__badge" aria-hidden="true" data-testid="tab-badge">
              {{ badge(countOf(item)) }}
            </span>
          </span>
          <span class="tab-bar__label">{{ item.label }}</span>
          <span v-if="countOf(item) > 0" class="tab-bar__sr">{{
            t('shell.unread', { count: countOf(item) })
          }}</span>
        </NuxtLink>
      </li>
    </ul>
  </nav>
</template>

<style scoped>
.tab-bar {
  position: fixed;
  right: 0;
  bottom: 0;
  left: 0;
  z-index: 10;
  display: none;
  padding: var(--space-1) env(safe-area-inset-right) calc(var(--space-1) + env(safe-area-inset-bottom))
    env(safe-area-inset-left);
  border-top: var(--size-border) solid var(--color-border);
  background: var(--color-surface);
}

.tab-bar__list {
  display: flex;
  margin: 0;
  padding: 0;
  list-style: none;
}

.tab-bar__item {
  flex: 1 1 0;
  min-width: 0;
}

.tab-bar__link {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: var(--space-1);
  height: var(--size-tab-bar);
  min-height: var(--size-touch);
  box-sizing: border-box;
  padding: 0;
  border-radius: var(--radius-md);
  color: var(--color-muted);
  font-size: var(--font-size-xs);
  text-decoration: none;
}

.tab-bar__link:hover {
  color: var(--color-text);
}

.tab-bar__link[aria-current='page'] {
  color: var(--color-accent);
  font-weight: var(--font-weight-medium);
}

.tab-bar__icon-wrap {
  position: relative;
  display: inline-flex;
}

.tab-bar__icon {
  width: var(--size-icon-md);
  height: var(--size-icon-md);
}

.tab-bar__badge {
  position: absolute;
  top: calc(-1 * var(--space-1));
  left: calc(100% - var(--space-2));
  min-width: var(--space-4);
  box-sizing: border-box;
  padding: 0 var(--space-1);
  border-radius: var(--radius-full);
  background: var(--color-accent);
  color: var(--color-on-accent);
  font-size: var(--font-size-xs);
  line-height: var(--line-height-normal);
  text-align: center;
}

.tab-bar__label {
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.tab-bar__sr {
  position: absolute;
  width: var(--size-border);
  height: var(--size-border);
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}

@media (max-width: 924px) {
  .tab-bar {
    display: block;
  }
}
</style>
