<script setup lang="ts">
// Changing the email in the settings (E03-06, FR-AUTH, ADR-0020): the account's address, a new one, and the code from
// the letter to the new one. A confirmed email stays until that code is entered, then the old address gets a letter
// about the change. An address another account has confirmed is refused without saying whose it is. Until the email
// is read the section shows a skeleton, and «Повторить» when it could not be. The calls come in as `api`
// (api/email.ts), so the form is tested without a server.
import { onMounted, ref } from 'vue'
import UiButton from '../ui/UiButton.vue'
import UiField from '../ui/UiField.vue'
import type { AccountEmailState, EmailChangeApi, EmailOutcome } from '../../api/email'
import { t } from '../../i18n'

const props = defineProps<{ api: EmailChangeApi }>()

const state = ref<'loading' | 'error' | 'ready'>('loading')
const current = ref<AccountEmailState>({ email: null, confirmedAt: null, pendingEmail: null })
const address = ref('')
const code = ref('')
const sending = ref(false)
const confirming = ref(false)
const addressError = ref<string>()
const codeError = ref<string>()
const formError = ref<string>()
const notice = ref<string>()

async function load() {
  state.value = 'loading'
  const result = await props.api.load()
  if (result.status !== 200 || !result.body) {
    state.value = 'error'
    return
  }
  current.value = result.body as AccountEmailState
  state.value = 'ready'
}

onMounted(load)

function common(result: EmailOutcome, failed: string): string {
  if (result.status === 401) return t('settings.sessionEnded')
  return result.status === 429 ? t('settings.tooManyAttempts') : failed
}

function problemCode(result: EmailOutcome): string | undefined {
  return (result.body as { code?: string } | undefined)?.code
}

async function send() {
  addressError.value = codeError.value = formError.value = notice.value = undefined
  sending.value = true
  const result = await props.api.change(address.value.trim())
  sending.value = false
  if (result.status === 202) {
    current.value = current.value.confirmedAt
      ? { ...current.value, pendingEmail: address.value.trim() }
      : { email: address.value.trim(), confirmedAt: null, pendingEmail: null }
    address.value = code.value = ''
    notice.value = t('settings.emailCodeSent')
    return
  }
  if (result.status === 204) {
    address.value = ''
    notice.value = t('settings.emailAlreadyYours')
    return
  }
  if (result.status === 400) {
    addressError.value = t('settings.emailInvalid')
    return
  }
  if (result.status === 409 && problemCode(result) === 'email_taken') {
    addressError.value = t('settings.emailTaken')
    return
  }
  formError.value = common(result, t('settings.emailSendFailed'))
}

async function confirm() {
  codeError.value = formError.value = notice.value = undefined
  confirming.value = true
  const result = await props.api.confirm(code.value.trim())
  confirming.value = false
  if (result.status === 204) {
    const replaced = current.value.confirmedAt !== null
    code.value = ''
    await load()
    notice.value = replaced ? t('settings.emailChanged') : t('settings.emailConfirmedNow')
    return
  }
  if (result.status === 400) {
    codeError.value = t('settings.emailCodeInvalid')
    return
  }
  if (result.status === 409) {
    codeError.value = t('settings.emailTaken')
    return
  }
  formError.value = common(result, t('settings.emailConfirmFailed'))
}

/** The address the last code went to, while it waits for that code. */
function waiting(): string | null {
  return current.value.pendingEmail ?? (current.value.confirmedAt ? null : current.value.email)
}
</script>

<template>
  <section class="email-change" data-testid="email-change" :aria-busy="state === 'loading'">
    <h2 class="email-change__title">{{ t('settings.email') }}</h2>

    <template v-if="state === 'loading'">
      <span class="visually-hidden" role="status">{{ t('settings.emailLoading') }}</span>
      <div class="email-change__skeleton" data-testid="email-loading" />
    </template>

    <template v-else-if="state === 'error'">
      <p class="email-change__error" role="alert">{{ t('settings.emailLoadFailed') }}</p>
      <div>
        <UiButton variant="secondary" @click="load()">{{ t('common.retry') }}</UiButton>
      </div>
    </template>

    <template v-else>
      <p class="email-change__current" data-testid="email-current">
        <template v-if="current.email && current.confirmedAt">
          {{ t('settings.emailCurrent') }} <strong>{{ current.email }}</strong>
          <span class="email-change__badge">{{ t('settings.emailConfirmed') }}</span>
        </template>
        <template v-else>{{ t('settings.emailNone') }}</template>
      </p>

      <form class="email-change__form" novalidate @submit.prevent="send">
        <UiField
          v-model="address"
          :label="t('settings.newEmail')"
          type="email"
          name="new-email"
          autocomplete="email"
          inputmode="email"
          required
          :hint="t('settings.newEmailHint')"
          :error="addressError"
        />
        <div class="email-change__actions">
          <UiButton type="submit" :loading="sending" :disabled="address.trim() === ''">{{
            t('settings.sendEmailCode')
          }}</UiButton>
        </div>
      </form>

      <form v-if="waiting()" class="email-change__form" novalidate @submit.prevent="confirm">
        <p class="email-change__hint" data-testid="email-pending">
          {{ t('settings.emailPending', { email: waiting()! }) }}
        </p>
        <UiField
          v-model="code"
          :label="t('auth.code')"
          name="email-code"
          autocomplete="one-time-code"
          inputmode="numeric"
          required
          :error="codeError"
        />
        <div class="email-change__actions">
          <UiButton type="submit" :loading="confirming" :disabled="code.trim() === ''">{{
            t('settings.confirmEmail')
          }}</UiButton>
        </div>
      </form>

      <p v-if="formError" class="email-change__error" role="alert">{{ formError }}</p>
      <p v-if="notice" class="email-change__notice" role="status">{{ notice }}</p>
    </template>
  </section>
</template>

<style scoped>
.email-change,
.email-change__form {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
  min-width: 0;
}

.email-change__title {
  margin: 0;
  font-size: var(--font-size-lg);
}

.email-change__current,
.email-change__hint {
  margin: 0;
  overflow-wrap: anywhere;
}

.email-change__hint {
  color: var(--color-muted);
}

.email-change__badge {
  margin-inline-start: var(--space-2);
  color: var(--color-success);
}

.email-change__error {
  margin: 0;
  color: var(--color-danger);
}

.email-change__notice {
  margin: 0;
  color: var(--color-success);
}

.email-change__actions {
  display: flex;
  justify-content: flex-end;
}

.email-change__skeleton {
  height: var(--size-touch);
  border-radius: var(--radius-md);
  background: var(--color-surface-subtle);
}
</style>
