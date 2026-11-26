<script setup lang="ts">
// The cookie banner (E02-13, FR-UI, ADR-0066): at the bottom of every page until the person chooses. «Разрешить все»
// and «Только необходимые» are buttons of the same weight, so refusing is as easy as agreeing; «Настроить» opens
// /settings/cookies, where the choice can be changed later. It does not block the page or take the focus: until a
// choice only the necessary cookies are set. It renders on the client only, once the saved choice is read, so the
// server page and the first client render agree and a person who has chosen never sees it flash. Below 925 px it covers
// the end of the page, so a space as tall as the banner follows the page: the last links (the service links of the
// «Меню» screen) can still be scrolled out from under it.
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { loadCookieConsent, useCookieConsent } from '../../composables/useCookieConsent'
import { acceptAll, necessaryOnly } from '../../utils/cookie-consent'
import { cookieSettingsPath } from '../../utils/navigation'
import UiButton from '../ui/UiButton.vue'
import { t } from '../../i18n'

const { loaded, decided, setCookieConsent } = useCookieConsent()
onMounted(loadCookieConsent)

const banner = ref<HTMLElement | null>(null)
const space = ref(0)
let observer: ResizeObserver | undefined
watch(banner, (element) => {
  observer?.disconnect()
  space.value = 0
  if (!element || typeof ResizeObserver === 'undefined') return
  observer = new ResizeObserver(() => {
    space.value = element.offsetHeight
  })
  observer.observe(element)
})
onBeforeUnmount(() => observer?.disconnect())
</script>

<template>
  <section
    v-if="loaded && !decided"
    ref="banner"
    class="cookie-banner"
    :aria-label="t('cookies.banner')"
    data-testid="cookie-banner"
  >
    <p class="cookie-banner__text">{{ t('cookies.bannerText') }}</p>
    <div class="cookie-banner__actions">
      <UiButton variant="secondary" size="sm" @click="setCookieConsent(necessaryOnly)">
        {{ t('cookies.necessaryOnly') }}
      </UiButton>
      <UiButton variant="secondary" size="sm" @click="setCookieConsent(acceptAll)">
        {{ t('cookies.acceptAll') }}
      </UiButton>
      <NuxtLink :to="cookieSettingsPath" class="cookie-banner__link">{{ t('cookies.customize') }}</NuxtLink>
    </div>
  </section>
  <div
    v-if="loaded && !decided"
    class="cookie-banner__space"
    :style="{ height: `${space}px` }"
    aria-hidden="true"
  />
</template>

<style scoped>
.cookie-banner {
  position: fixed;
  /* Over the right column on a desktop, where it hides neither the menu nor the post being read. */
  right: var(--space-4);
  bottom: var(--space-4);
  z-index: 40;
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  max-width: var(--size-aside);
  padding: var(--space-4);
  border: var(--size-border) solid var(--color-border);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  box-shadow: 0 var(--space-1) var(--space-4) var(--color-shadow);
  color: var(--color-text);
}

.cookie-banner__text {
  margin: 0;
  font-size: var(--font-size-sm);
  line-height: var(--line-height-normal);
}

.cookie-banner__actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--space-2);
}

.cookie-banner__link {
  display: inline-flex;
  align-items: center;
  min-height: var(--space-8);
  padding: 0 var(--space-2);
  color: var(--color-accent);
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-medium);
}

/* On a desktop the banner lies over the right column and covers no end of a page. */
.cookie-banner__space {
  display: none;
}

/* Above the bottom bar of the phone shell (ShellTabBar.vue). */
@media (max-width: 924px) {
  .cookie-banner__space {
    display: block;
  }

  .cookie-banner {
    bottom: calc(
      var(--size-tab-bar) + 2 * var(--space-1) + var(--size-border) + var(--space-2) +
        env(safe-area-inset-bottom)
    );
  }
}

/* A phone: across the screen, buttons wide enough for a thumb. */
@media (max-width: 639px) {
  .cookie-banner {
    right: var(--space-2);
    left: var(--space-2);
    max-width: none;
  }

  .cookie-banner__actions > * {
    flex: 1 1 auto;
    justify-content: center;
    min-height: var(--size-touch);
  }
}
</style>
