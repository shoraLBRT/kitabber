<script setup lang="ts">
// Header of the desktop shell (E02-05, docs/spec.md section 5, docs/ui/README.md): logo, search, "Написать",
// the bell and the avatar menu, or "Войти" for a guest. Search (FR-SEARCH, #85) comes later: for now it only emits an
// event. The bell (E12-02, docs/ui/notifications.md) carries the unread counter («99+» above 99) in its name and opens
// the drop-down panel, whose list the layout puts in the `notifications` slot; the panel closes by Esc, by a click
// outside it and when a row is opened, and leads on to the full page.
import { Bell, LogOut, PenLine, Search } from 'lucide-vue-next'
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import type { MenuItem } from '../../types/ui'
import { accountNav, loginPath, notificationsPath, writePath } from '../../utils/navigation'
import { badgeText } from '../../api/notifications'
import BrandLogo from '../BrandLogo.vue'
import UiAvatar from '../ui/UiAvatar.vue'
import UiMenu from '../ui/UiMenu.vue'
import { t } from '../../i18n'

export interface ShellUser {
  name: string
  avatar?: string
}

const props = withDefaults(defineProps<{ user?: ShellUser | null; unread?: number }>(), {
  user: null,
  unread: 0,
})
const emit = defineEmits<{ search: []; account: [id: string] }>()

// "Коллекция" has no FR yet and donations never appear (docs/ui/README.md); the theme switch lives in the left
// menu, so this menu does not repeat it.
const accountItems: MenuItem[] = [
  ...accountNav.map(({ id, label, icon }) => ({ id, label, icon })),
  { id: 'logout', label: t('common.signOut'), icon: LogOut, danger: true },
]
const accountLabel = computed(() => (props.user ? t('shell.accountMenu', { name: props.user.name }) : ''))

const bellLabel = computed(() =>
  props.unread > 0 ? t('notifications.bellUnread', { count: props.unread }) : t('common.notifications'),
)
const panelOpen = ref(false)
const bellWrap = ref<HTMLElement | null>(null)
const bell = ref<HTMLButtonElement | null>(null)

function closePanel(returnFocus = false) {
  if (!panelOpen.value) return
  panelOpen.value = false
  if (returnFocus) bell.value?.focus()
}

function onDocumentPointerdown(event: PointerEvent) {
  if (bellWrap.value && !bellWrap.value.contains(event.target as Node)) closePanel()
}

function onDocumentKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') closePanel(true)
}

watch(panelOpen, (open) => {
  if (open) {
    document.addEventListener('pointerdown', onDocumentPointerdown)
    document.addEventListener('keydown', onDocumentKeydown)
  } else {
    document.removeEventListener('pointerdown', onDocumentPointerdown)
    document.removeEventListener('keydown', onDocumentKeydown)
  }
})

onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', onDocumentPointerdown)
  document.removeEventListener('keydown', onDocumentKeydown)
})
</script>

<template>
  <header class="shell-header" data-testid="shell-header">
    <div class="shell-header__inner">
      <NuxtLink to="/" class="shell-header__home" :aria-label="t('common.home')"><BrandLogo /></NuxtLink>
      <button
        type="button"
        class="shell-header__icon-button shell-header__search"
        :aria-label="t('common.search')"
        @click="emit('search')"
      >
        <Search class="shell-header__icon" aria-hidden="true" />
      </button>
      <div class="shell-header__actions">
        <NuxtLink :to="writePath" class="shell-header__write" data-nav="write">
          <PenLine class="shell-header__icon" aria-hidden="true" />
          {{ t('common.write') }}
        </NuxtLink>
        <template v-if="user">
          <div ref="bellWrap" class="shell-header__bell">
            <button
              ref="bell"
              type="button"
              class="shell-header__icon-button"
              :aria-label="bellLabel"
              :aria-expanded="panelOpen ? 'true' : 'false'"
              aria-controls="notifications-panel"
              data-testid="notifications-bell"
              @click="panelOpen = !panelOpen"
            >
              <Bell class="shell-header__icon" aria-hidden="true" />
              <span
                v-if="unread > 0"
                class="shell-header__badge"
                aria-hidden="true"
                data-testid="notifications-badge"
                >{{ badgeText(unread) }}</span
              >
            </button>
            <section
              v-if="panelOpen"
              id="notifications-panel"
              class="shell-header__panel"
              :aria-label="t('common.notifications')"
              data-testid="notifications-panel"
            >
              <h2 class="shell-header__panel-title">{{ t('common.notifications') }}</h2>
              <div class="shell-header__panel-list">
                <slot name="notifications" :close="() => closePanel()" />
              </div>
              <NuxtLink :to="notificationsPath" class="shell-header__panel-all" @click="closePanel()">
                {{ t('notifications.all') }}
              </NuxtLink>
            </section>
          </div>
          <UiMenu :items="accountItems" :label="accountLabel" align="end" @select="emit('account', $event)">
            <template #trigger>
              <UiAvatar :name="user.name" :src="user.avatar" size="sm" decorative />
            </template>
          </UiMenu>
        </template>
        <NuxtLink v-else :to="loginPath" external class="shell-header__login" data-nav="login">{{
          t('common.signIn')
        }}</NuxtLink>
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
  position: relative;
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

.shell-header__bell {
  position: relative;
}

.shell-header__badge {
  position: absolute;
  top: 0;
  right: 0;
  min-width: var(--space-4);
  box-sizing: border-box;
  padding: 0 var(--space-1);
  border-radius: var(--radius-full);
  background: var(--color-accent);
  color: var(--color-on-accent);
  font-size: var(--font-size-xs);
  line-height: var(--space-4);
  text-align: center;
}

.shell-header__panel {
  position: absolute;
  top: calc(100% + var(--space-2));
  right: 0;
  display: flex;
  flex-direction: column;
  width: var(--size-aside);
  max-width: calc(100vw - 2 * var(--space-4));
  max-height: 70vh;
  border: var(--size-border) solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  box-shadow: 0 var(--space-2) var(--space-6) var(--color-shadow);
}

.shell-header__panel-title {
  margin: 0;
  padding: var(--space-4) var(--space-5) var(--space-2);
  font-size: var(--font-size-md);
}

.shell-header__panel-list {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
}

.shell-header__panel-all {
  padding: var(--space-3) var(--space-5);
  border-top: var(--size-border) solid var(--color-border);
  font-size: var(--font-size-sm);
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
