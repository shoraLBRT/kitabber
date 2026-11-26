<script setup lang="ts">
// Desktop shell (E02-05, docs/spec.md section 5, docs/ui/README.md): skip link, header, left menu --size-menu,
// central column --size-feed and the sticky right column --size-aside. Layouts by the breakpoint tokens:
//   from 1240 px  three columns;
//   925–1239 px   left menu and the central column, the right column is hidden;
//   640–924 px    one column with margins, the left menu is hidden;
//   up to 639 px  one column across the full width.
// Below 925 px navigation moves to the phone shell (E02-06): the bottom bar ShellTabBar (and below 640 px the floating
// "Написать" ShellWriteButton) in the "mobile" slot; the left column becomes the "Меню" screen (/menu).
import type { NavItem } from '../../utils/navigation'
import ShellAside from './ShellAside.vue'
import ShellHeader, { type ShellUser } from './ShellHeader.vue'
import ShellNav from './ShellNav.vue'
import ShellOfflineBanner from './ShellOfflineBanner.vue'
import ShellTabBar from './ShellTabBar.vue'
import ShellWriteButton from './ShellWriteButton.vue'
import { t } from '../../i18n'

withDefaults(
  defineProps<{
    currentPath: string
    user?: ShellUser | null
    topics?: NavItem[]
    /** Unread counters of the bottom bar by tab id. */
    counts?: Partial<Record<string, number>>
    /** `false` hides the floating «Написать», as the post page does for its action panel (E07-04). */
    writeButton?: boolean
    /** The floating «Написать» opens the quick post (`write`) instead of the full editor (E05-29, ADR-0055). */
    quickPost?: boolean
    /** The browser is offline: «Нет подключения» above the content (E02-07, ADR-0057). */
    offline?: boolean
  }>(),
  { user: null, topics: () => [], counts: () => ({}), writeButton: true, quickPost: false, offline: false },
)
const emit = defineEmits<{ search: []; account: [id: string]; write: [] }>()
</script>

<template>
  <div class="shell" data-testid="shell">
    <a href="#content" class="shell__skip">{{ t('shell.skipToContent') }}</a>
    <ShellHeader :user="user" @search="emit('search')" @account="emit('account', $event)" />
    <div class="shell__body">
      <div class="shell__menu" data-column="menu">
        <ShellNav :current-path="currentPath" :topics="topics" />
      </div>
      <main id="content" class="shell__content" tabindex="-1" data-column="feed">
        <ShellOfflineBanner v-if="offline" />
        <slot />
      </main>
      <div class="shell__aside" data-column="aside">
        <slot name="aside"><ShellAside /></slot>
      </div>
    </div>
    <slot name="mobile">
      <ShellWriteButton v-if="writeButton" :quick="quickPost" @write="emit('write')" />
      <ShellTabBar :current-path="currentPath" :counts="counts" />
    </slot>
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
    /* Room for the fixed bottom bar: its height, the floating button and the safe area. */
    padding: var(--space-4) var(--space-4)
      calc(
        var(--size-tab-bar) + 2 * var(--space-1) + var(--size-border) + var(--space-16) +
          env(safe-area-inset-bottom)
      );
  }

  .shell__menu {
    display: none;
  }
}

@media (max-width: 639px) {
  .shell__body {
    grid-template-columns: minmax(0, 1fr);
    padding: var(--space-4) 0
      calc(
        var(--size-tab-bar) + 2 * var(--space-1) + var(--size-border) + var(--space-16) +
          env(safe-area-inset-bottom)
      );
  }
}
</style>
