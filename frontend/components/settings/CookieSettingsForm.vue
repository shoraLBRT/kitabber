<script setup lang="ts">
// The cookie choice of /settings/cookies (E02-13, FR-UI, ADR-0066), open to guests: the necessary cookies are
// always on, the optional categories are checkboxes. «Сохранить выбор» records the choice — the banner then stops
// asking — and a withdrawn category's cookies are removed at once (composables/useCookieConsent.ts).
import { onMounted, ref, watch } from 'vue'
import { loadCookieConsent, useCookieConsent } from '../../composables/useCookieConsent'
import { useToast } from '../../composables/useToast'
import { necessaryOnly } from '../../utils/cookie-consent'
import UiButton from '../ui/UiButton.vue'
import { t } from '../../i18n'

const { choice, loaded, decided, setCookieConsent } = useCookieConsent()
const toast = useToast()
const analytics = ref(false)

watch(
  choice,
  (value) => {
    analytics.value = (value ?? necessaryOnly).analytics
  },
  { immediate: true },
)
onMounted(loadCookieConsent)

function save() {
  setCookieConsent({ analytics: analytics.value })
  toast.show(t('cookies.saved'), { tone: 'success' })
}
</script>

<template>
  <form class="cookie-settings" data-testid="cookie-settings" @submit.prevent="save">
    <p class="cookie-settings__text">{{ t('cookies.intro') }}</p>
    <p v-if="loaded && !decided" class="cookie-settings__text cookie-settings__muted">
      {{ t('cookies.undecided') }}
    </p>
    <fieldset class="cookie-settings__group">
      <legend class="cookie-settings__legend">{{ t('cookies.categories') }}</legend>
      <label class="cookie-settings__option">
        <input
          type="checkbox"
          class="cookie-settings__check"
          checked
          disabled
          aria-describedby="cookies-necessary"
        />
        <span class="cookie-settings__name">{{ t('cookies.necessary') }}</span>
      </label>
      <p id="cookies-necessary" class="cookie-settings__hint">{{ t('cookies.necessaryText') }}</p>
      <label class="cookie-settings__option">
        <input
          v-model="analytics"
          type="checkbox"
          class="cookie-settings__check"
          :disabled="!loaded"
          aria-describedby="cookies-analytics"
        />
        <span class="cookie-settings__name">{{ t('cookies.analytics') }}</span>
      </label>
      <p id="cookies-analytics" class="cookie-settings__hint">{{ t('cookies.analyticsText') }}</p>
    </fieldset>
    <div class="cookie-settings__actions">
      <UiButton type="submit" :disabled="!loaded">{{ t('cookies.save') }}</UiButton>
    </div>
  </form>
</template>

<style scoped>
.cookie-settings {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
}

.cookie-settings__text {
  margin: 0;
  line-height: var(--line-height-normal);
}

.cookie-settings__muted {
  color: var(--color-muted);
  font-size: var(--font-size-sm);
}

.cookie-settings__group {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  margin: 0;
  padding: 0;
  border: 0;
}

.cookie-settings__legend {
  margin-bottom: var(--space-2);
  padding: 0;
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-medium);
}

.cookie-settings__option {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  min-height: var(--size-touch);
  cursor: pointer;
}

.cookie-settings__check {
  width: var(--space-5);
  height: var(--space-5);
  margin: 0;
  accent-color: var(--color-accent);
}

.cookie-settings__name {
  font-weight: var(--font-weight-medium);
}

.cookie-settings__hint {
  margin: 0 0 var(--space-3) calc(var(--space-5) + var(--space-3));
  color: var(--color-muted);
  font-size: var(--font-size-sm);
  line-height: var(--line-height-normal);
}

@media (max-width: 639px) {
  .cookie-settings__actions > * {
    width: 100%;
  }
}
</style>
