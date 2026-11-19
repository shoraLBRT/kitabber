<script setup lang="ts">
// Desktop shell (E02-05, docs/spec.md section 5, docs/ui/README.md): skip link, header, left menu --size-menu,
// central column --size-feed and the sticky right column --size-aside. Layouts by the breakpoint tokens:
//   from 1240 px  three columns;
//   925–1239 px   left menu and the central column, the right column is hidden;
//   640–924 px    one column with margins, the left menu is hidden;
//   up to 639 px  one column across the full width.
// Below 925 px navigation moves to the phone shell (#16): its bottom bar goes into the "mobile" slot.
import type { NavItem } from '../../utils/navigation'
import ShellAside from './ShellAside.vue'
import ShellHeader, { type ShellUser } from './ShellHeader.vue'
import ShellNav from './ShellNav.vue'

withDefaults(
  defineProps<{
    currentPath: string
    user?: ShellUser | null
    topics?: NavItem[]
  }>(),
  { user: null, topics: () => [] },
)
const emit = defineEmits<{ search: []; account: [id: string] }>()
</script>

<template>
  <div class="shell" data-testid="shell">
    <a href="#content" class="shell__skip">Перейти к содержимому</a>
    <ShellHeader :user="user" @search="emit('search')" @account="emit('account', $event)" />
    <div class="shell__body">
      <div class="shell__menu" data-column="menu">
        <ShellNav :current-path="currentPath" :topics="topics" />
      </div>
      <main id="content" class="shell__content" tabindex="-1" data-column="feed">
        <slot />
      </main>
      <div class="shell__aside" data-column="aside">
        <slot name="aside"><ShellAside /></slot>
      </div>
    </div>
    <slot name="mobile" />
  </div>
</template>

<style scoped>
.shell {
  min-height: 100vh;
}

.shell__skip {
  position: absolute;
  top: var(--space-2);
  left: var(--space-2);
  z-index: 20;
  padding: var(--space-2) var(--space-4);
  border-radius: var(--radius-md);
  background: var(--color-accent);
  color: var(--color-on-accent);
  transform: translateY(-200%);
}

.shell__skip:focus {
  transform: none;
}

.shell__body {
  display: grid;
  grid-template-columns: var(--size-menu) var(--size-feed) var(--size-aside);
  justify-content: center;
  align-items: start;
  column-gap: var(--space-6);
  padding: var(--space-6) var(--space-6) var(--space-16);
}

.shell__menu,
.shell__aside {
  position: sticky;
  top: calc(var(--space-16) + var(--space-6));
  max-height: calc(100vh - var(--space-16) - var(--space-6) * 2);
  overflow-y: auto;
}

.shell__content {
  min-width: 0;
}

.shell__content:focus {
  outline: none;
}

@media (max-width: 1239px) {
  .shell__body {
    grid-template-columns: var(--size-menu) minmax(0, var(--size-feed));
  }

  .shell__aside {
    display: none;
  }
}

@media (max-width: 924px) {
  .shell__body {
    grid-template-columns: minmax(0, var(--size-feed));
    padding: var(--space-4) var(--space-4) var(--space-16);
  }

  .shell__menu {
    display: none;
  }
}

@media (max-width: 639px) {
  .shell__body {
    grid-template-columns: minmax(0, 1fr);
    padding: var(--space-4) 0 var(--space-16);
  }
}
</style>
