<script setup lang="ts">
// Desktop shell (E02-05, docs/spec.md section 5, docs/ui/README.md): skip link, header, left menu --size-menu,
// central column --size-feed and the sticky right column --size-aside. Layouts by the breakpoint tokens:
//   from 1240 px  three columns;
//   925–1239 px   left menu and the central column, the right column is hidden;
//   640–924 px    one column with margins, the left menu is hidden;
//   up to 639 px  one column across the full width.
// Below 925 px navigation moves to the phone shell (E02-06): the bottom bar ShellTabBar (and below 640 px the floating
// "Написать" ShellWriteButton) in the "mobile" slot; the left column becomes the "Меню" screen (/menu).
// The header's search button opens the `search` slot (ShellSearch, E14-02), which gives the focus back when it closes.
// In the phone shell the header hides while the page is scrolled down and comes back when it is scrolled up (E08-14,
// useHideOnScroll, ADR-0111); `--shell-header-offset` is how much of the screen it covers, so what sticks under it (the
// «Показать новые» banner, «К следующему новому», «N новых»; each has the class `shell-sticky`) follows it up. A header holding the keyboard's focus stays.
import { nextTick, ref } from 'vue'
import { useHideOnScroll } from '../../composables/useHideOnScroll'
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
    /** Unread counters of the bottom bar by tab id; `notifications` is also the counter on the header's bell. */
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

const searchOpen = ref(false)
const header = useHideOnScroll()
const root = ref<HTMLElement | null>(null)

function openSearch() {
  searchOpen.value = true
  emit('search')
}

function closeSearch() {
  searchOpen.value = false
  void nextTick(() => root.value?.querySelector<HTMLElement>('.shell-header__search')?.focus())
}
</script>

<template>
  <div ref="root" class="shell" :class="{ 'shell--header-hidden': header.hidden.value }" data-testid="shell">
    <a href="#content" class="shell__skip">{{ t('shell.skipToContent') }}</a>
    <ShellHeader
      class="shell__header"
      :user="user"
      :unread="counts.notifications ?? 0"
      @search="openSearch()"
      @account="emit('account', $event)"
    >
      <template v-if="$slots.notifications" #notifications="{ close }">
        <slot name="notifications" :close="close" />
      </template>
    </ShellHeader>
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
    <slot v-if="searchOpen" name="search" :close="closeSearch" />
    <slot name="mobile">
      <ShellWriteButton v-if="writeButton" :quick="quickPost" @write="emit('write')" />
      <ShellTabBar :current-path="currentPath" :counts="counts" />
    </slot>
  </div>
</template>

<style scoped>
.shell {
  --shell-header-offset: calc(var(--space-16) + env(safe-area-inset-top));

  min-height: 100vh;
}

.shell__header {
  transition: transform 0.2s ease;
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

  /*
   * The phone header hides above the top edge; sticky things then keep clear of the notch's safe area only. The
   * offset changes on them alone (class `shell-sticky`), not on the shell: a variable changed on the shell is inherited
   * by the whole page, and every hide would restyle a tree of 1000 comments.
   */
  .shell--header-hidden:not(:has(.shell__header :focus-visible)) :deep(.shell-sticky) {
    --shell-header-offset: env(safe-area-inset-top);
  }

  .shell--header-hidden:not(:has(.shell__header :focus-visible)) .shell__header {
    transform: translateY(-100%);
  }
}

@media (prefers-reduced-motion: reduce) {
  .shell__header {
    transition: none;
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
