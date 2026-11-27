<script setup lang="ts">
// The buttons of the external providers (#91, ADR-0115) under the sign-in form: one link per provider the API turns
// on, in its order. Without providers (no keys on the stand) the block is not shown at all; while the list loads it
// says so, and when it cannot be loaded it says that instead of buttons. `error` is the message of the last failed
// attempt (the page's `externalError`), shown above the buttons.
import { onMounted, ref } from 'vue'
import { providerName, type ExternalProvider, type ExternalSignInApi } from '../../api/external-sign-in'
import { t } from '../../i18n'

const props = defineProps<{ api: ExternalSignInApi; returnUrl: string; error?: string }>()

const state = ref<'loading' | 'ready' | 'unavailable'>('loading')
const providers = ref<ExternalProvider[]>([])

onMounted(async () => {
  const list = await props.api.providers()
  providers.value = list ?? []
  state.value = list ? 'ready' : 'unavailable'
})
</script>

<template>
  <div
    v-if="state !== 'ready' || providers.length > 0 || error"
    class="external-sign-in"
    data-testid="external-sign-in"
    :aria-busy="state === 'loading'"
  >
    <p v-if="error" class="external-sign-in__error" role="alert">{{ error }}</p>
    <template v-if="state !== 'ready' || providers.length > 0">
      <p class="external-sign-in__title">{{ t('auth.external.title') }}</p>
      <p v-if="state === 'loading'" class="external-sign-in__status" role="status">
        {{ t('auth.external.loading') }}
      </p>
      <p v-else-if="state === 'unavailable'" class="external-sign-in__status" role="status">
        {{ t('auth.external.unavailable') }}
      </p>
      <ul v-else class="external-sign-in__list">
        <li v-for="provider in providers" :key="provider">
          <a
            class="external-sign-in__button"
            :class="`external-sign-in__button--${provider}`"
            :href="api.startUrl(provider, returnUrl)"
            :aria-label="t('auth.external.continueWith', { provider: providerName(provider) })"
            :data-provider="provider"
          >
            {{ providerName(provider) }}
          </a>
        </li>
      </ul>
    </template>
  </div>
</template>

<style scoped>
.external-sign-in {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  margin-top: var(--space-6);
}

.external-sign-in__title,
.external-sign-in__status {
  margin: 0;
  color: var(--color-muted);
  font-size: var(--font-size-sm);
  text-align: center;
}

.external-sign-in__error {
  margin: 0;
  color: var(--color-danger);
}

.external-sign-in__list {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--space-2);
  margin: 0;
  padding: 0;
  list-style: none;
}

.external-sign-in__button {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: var(--space-10);
  padding: var(--space-2) var(--space-4);
  border: var(--size-border) solid var(--color-border-strong);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: var(--color-text);
  font-weight: var(--font-weight-medium);
  text-decoration: none;
  box-sizing: border-box;
}

.external-sign-in__button:hover {
  background: var(--color-surface-subtle);
}
</style>
