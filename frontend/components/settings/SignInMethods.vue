<script setup lang="ts">
// «Способы входа» in the settings (#119, FR-AUTH, ADR-0098, ADR-0115): every external provider the API turns on, and
// any linked one it no longer does, with when it was linked and «Отвязать», or «Привязать», a link through the
// provider's sign-in that comes back to `returnUrl`. The last way to sign in cannot be unlinked: the API answers 409
// and the section says why. `error` is the message of a linking that failed (the page's `externalError`). Until the
// lists are read the section shows a skeleton, and «Повторить» when they could not be. The calls come in as props
// (api/identities.ts, api/external-sign-in.ts), so the section is tested without a server.
import { computed, onMounted, ref } from 'vue'
import UiButton from '../ui/UiButton.vue'
import type { ExternalIdentity, IdentitiesApi } from '../../api/identities'
import { providerName, type ExternalSignInApi } from '../../api/external-sign-in'
import { locale, t } from '../../i18n'

const props = defineProps<{
  api: IdentitiesApi
  external: ExternalSignInApi
  returnUrl: string
  error?: string
}>()

const state = ref<'loading' | 'error' | 'ready'>('loading')
const identities = ref<ExternalIdentity[]>([])
const providers = ref<string[]>([])
const unlinking = ref<string>()
const formError = ref<string>()
const notice = ref<string>()

const dateFormat = new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long', year: 'numeric' })

// The providers turned on, in the API's order, then any linked one that is not turned on any more.
const rows = computed(() => {
  const keys = [...providers.value]
  for (const identity of identities.value) if (!keys.includes(identity.provider)) keys.push(identity.provider)
  return keys.map((provider) => ({
    provider,
    identity: identities.value.find((i) => i.provider === provider),
    canLink: providers.value.includes(provider),
  }))
})

async function load() {
  state.value = 'loading'
  const [result, list] = await Promise.all([props.api.load(), props.external.providers()])
  if (result.status !== 200 || !Array.isArray(result.body) || !list) {
    state.value = 'error'
    return
  }
  identities.value = result.body as ExternalIdentity[]
  providers.value = list
  state.value = 'ready'
}

onMounted(load)

async function unlink(identity: ExternalIdentity) {
  formError.value = notice.value = undefined
  unlinking.value = identity.id
  const result = await props.api.unlink(identity.id)
  unlinking.value = undefined
  // 404: unlinked already, from somewhere else.
  if (result.status === 204 || result.status === 404) {
    identities.value = identities.value.filter((i) => i.id !== identity.id)
    notice.value = t('settings.signInMethodUnlinked', { provider: providerName(identity.provider) })
    return
  }
  formError.value =
    result.status === 409
      ? t('settings.signInMethodLast')
      : result.status === 401
        ? t('settings.sessionEnded')
        : t('settings.signInMethodUnlinkFailed')
}
</script>

<template>
  <section class="sign-in-methods" data-testid="sign-in-methods" :aria-busy="state === 'loading'">
    <h2 class="sign-in-methods__title">{{ t('settings.signInMethods') }}</h2>
    <p v-if="error" class="sign-in-methods__error" role="alert">{{ error }}</p>

    <template v-if="state === 'loading'">
      <span class="visually-hidden" role="status">{{ t('settings.signInMethodsLoading') }}</span>
      <div class="sign-in-methods__skeleton" data-testid="sign-in-methods-loading" />
    </template>

    <template v-else-if="state === 'error'">
      <p class="sign-in-methods__error" role="alert">{{ t('settings.signInMethodsLoadFailed') }}</p>
      <div>
        <UiButton variant="secondary" @click="load()">{{ t('common.retry') }}</UiButton>
      </div>
    </template>

    <template v-else>
      <p v-if="rows.length === 0" class="sign-in-methods__hint">{{ t('settings.signInMethodsNone') }}</p>
      <ul v-else class="sign-in-methods__list">
        <li
          v-for="row in rows"
          :key="row.provider"
          class="sign-in-methods__item"
          data-testid="sign-in-method"
          :data-provider="row.provider"
        >
          <div class="sign-in-methods__info">
            <p class="sign-in-methods__name">{{ providerName(row.provider) }}</p>
            <p class="sign-in-methods__state">
              {{
                row.identity
                  ? t('settings.signInMethodLinkedAt', {
                      date: dateFormat.format(new Date(row.identity.linkedAt)),
                    })
                  : t('settings.signInMethodNotLinked')
              }}
            </p>
          </div>
          <UiButton
            v-if="row.identity"
            variant="secondary"
            :loading="unlinking === row.identity.id"
            :aria-label="t('settings.signInMethodUnlinkLabel', { provider: providerName(row.provider) })"
            @click="unlink(row.identity)"
          >
            {{ t('settings.signInMethodUnlink') }}
          </UiButton>
          <a
            v-else-if="row.canLink"
            class="sign-in-methods__link"
            :href="external.startUrl(row.provider, returnUrl, true)"
            :aria-label="t('settings.signInMethodLinkLabel', { provider: providerName(row.provider) })"
          >
            {{ t('settings.signInMethodLink') }}
          </a>
        </li>
      </ul>

      <p v-if="formError" class="sign-in-methods__error" role="alert">{{ formError }}</p>
      <p v-if="notice" class="sign-in-methods__notice" role="status">{{ notice }}</p>
    </template>
  </section>
</template>

<style scoped>
.sign-in-methods {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
  min-width: 0;
}

.sign-in-methods__title {
  margin: 0;
  font-size: var(--font-size-lg);
}

.sign-in-methods__list {
  display: flex;
  flex-direction: column;
  margin: 0;
  padding: 0;
  list-style: none;
}

.sign-in-methods__item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
  padding: var(--space-3) 0;
  border-bottom: var(--size-border) solid var(--color-border);
}

.sign-in-methods__info {
  min-width: 0;
}

.sign-in-methods__name,
.sign-in-methods__state,
.sign-in-methods__hint {
  margin: 0;
  overflow-wrap: anywhere;
}

.sign-in-methods__state,
.sign-in-methods__hint {
  color: var(--color-muted);
}

.sign-in-methods__error {
  margin: 0;
  color: var(--color-danger);
}

.sign-in-methods__notice {
  margin: 0;
  color: var(--color-success);
}

.sign-in-methods__link {
  display: inline-flex;
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

.sign-in-methods__link:hover {
  background: var(--color-surface-subtle);
}

.sign-in-methods__skeleton {
  height: var(--size-touch);
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}
</style>
