<script setup lang="ts">
// Header of the desktop shell (E02-05, docs/spec.md section 5, docs/ui/README.md): logo, search, "Написать",
// the bell and the avatar menu, or "Войти" for a guest. Search (FR-SEARCH, #85) and the bell's drop-down panel
// (docs/ui/notifications.md) come later: for now search only emits an event and the bell leads to the full page.
import { Bell, LogOut, PenLine, Search } from 'lucide-vue-next'
import { computed } from 'vue'
import type { MenuItem } from '../../types/ui'
import { accountNav, loginPath, notificationsPath, writePath } from '../../utils/navigation'
import BrandLogo from '../BrandLogo.vue'
import UiAvatar from '../ui/UiAvatar.vue'
import UiMenu from '../ui/UiMenu.vue'

export interface ShellUser {
  name: string
  avatar?: string
}

const props = withDefaults(defineProps<{ user?: ShellUser | null }>(), { user: null })
const emit = defineEmits<{ search: []; account: [id: string] }>()

// "Коллекция" has no FR yet and donations never appear (docs/ui/README.md); the theme switch lives in the left
// menu, so this menu does not repeat it.
const accountItems: MenuItem[] = [
  ...accountNav.map(({ id, label, icon }) => ({ id, label, icon })),
  { id: 'logout', label: 'Выйти', icon: LogOut, danger: true },
]
const accountLabel = computed(() => (props.user ? `Меню профиля: ${props.user.name}` : ''))
</script>

<template>
  <header class="shell-header" data-testid="shell-header">
    <div class="shell-header__inner">
      <NuxtLink to="/" class="shell-header__home" aria-label="На главную"><BrandLogo /></NuxtLink>
      <button
        type="button"
        class="shell-header__icon-button shell-header__search"
        aria-label="Поиск"
        @click="emit('search')"
      >
        <Search class="shell-header__icon" aria-hidden="true" />
      </button>
      <div class="shell-header__actions">
        <NuxtLink :to="writePath" class="shell-header__write" data-nav="write">
          <PenLine class="shell-header__icon" aria-hidden="true" />
          Написать
        </NuxtLink>
        <template v-if="user">
          <NuxtLink :to="notificationsPath" class="shell-header__icon-button" aria-label="Уведомления">
            <Bell class="shell-header__icon" aria-hidden="true" />
          </NuxtLink>
          <UiMenu :items="accountItems" :label="accountLabel" align="end" @select="emit('account', $event)">
            <template #trigger>
              <UiAvatar :name="user.name" :src="user.avatar" size="sm" decorative />
            </template>
          </UiMenu>
        </template>
        <NuxtLink v-else :to="loginPath" external class="shell-header__login" data-nav="login"
          >Войти</NuxtLink
        >
      </div>
    </div>
  </header>
</template>

<style scoped>
.shell-header {
  position: sticky;
  top: 0;
  z-index: 10;
  /* The notch: with viewport-fit=cover the header grows by the top safe area (E02-06). */
  height: calc(var(--space-16) + env(safe-area-inset-top));
  box-sizing: border-box;
  padding-top: env(safe-area-inset-top);
  border-bottom: var(--size-border) solid var(--color-border);
  background: var(--color-surface);
}

.shell-header__inner {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  box-sizing: border-box;
  height: 100%;
  padding: 0 var(--space-6);
}

.shell-header__home {
  display: inline-flex;
  text-decoration: none;
}

.shell-header__actions {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  margin-left: auto;
}

.shell-header__icon-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: var(--space-2);
  border: 0;
  border-radius: var(--radius-full);
  background: transparent;
  color: var(--color-text);
  cursor: pointer;
}

.shell-header__icon-button:hover {
  background: var(--color-surface-subtle);
  color: var(--color-text);
}

.shell-header__icon {
  width: var(--size-icon-md);
  height: var(--size-icon-md);
}

.shell-header__write,
.shell-header__login {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  min-height: var(--space-10);
  box-sizing: border-box;
  padding: var(--space-2) var(--space-4);
  border-radius: var(--radius-md);
  font-weight: var(--font-weight-medium);
  text-decoration: none;
}

.shell-header__write {
  background: var(--color-accent);
  color: var(--color-on-accent);
}

.shell-header__write:hover {
  background: var(--color-accent-hover);
  color: var(--color-on-accent);
}

.shell-header__write .shell-header__icon {
  width: var(--size-icon-sm);
  height: var(--size-icon-sm);
}

.shell-header__login {
  border: var(--size-border) solid var(--color-border-strong);
  color: var(--color-text);
}

.shell-header__login:hover {
  background: var(--color-surface-subtle);
  color: var(--color-text);
}

/* Phone header (docs/ui/README.md, 390 px): logo and search only; "Написать", the bell, the avatar and "Войти"
   move to the floating button, the bottom bar and the "Меню" screen of the phone shell (#16). */
@media (max-width: 639px) {
  .shell-header__inner {
    padding: 0 calc(var(--space-4) + env(safe-area-inset-right)) 0
      calc(var(--space-4) + env(safe-area-inset-left));
  }

  .shell-header__search {
    margin-left: auto;
  }

  .shell-header__actions {
    display: none;
  }
}
</style>
