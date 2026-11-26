<script setup lang="ts">
// Password reset (E03-05, FR-AUTH, ADR-0036): the email, then the code from the letter and the new password, with a
// resend timer. Opened by «Забыли пароль?» of the sign-in form, on the /login page and in the sign-in window. The new
// password ends every earlier session of the account and signs the person in at the provider, so the form emits
// `signed-in` like the sign-in form does. No message tells whether an address is registered: the API does not say it.
import { computed, onBeforeUnmount, ref } from 'vue'
import UiButton from '../ui/UiButton.vue'
import UiField from '../ui/UiField.vue'
import { formatCountdown, passwordMinLength } from '../../api/registration'
import type { PasswordResetApi } from '../../api/password'
import { t } from '../../i18n'
import I18nText from '../../i18n/I18nText'

const props = defineProps<{ api: PasswordResetApi; email?: string }>()
const emit = defineEmits<{ 'signed-in': []; back: [] }>()

const unreachable = t('auth.unreachable')

const step = ref<'email' | 'code'>('email')
const email = ref(props.email ?? '')
const code = ref('')
const password = ref('')
const resetId = ref('')
const loading = ref(false)
const resending = ref(false)
const emailError = ref<string>()
const codeError = ref<string>()
const passwordError = ref<string>()
const formError = ref<string>()
const notice = ref<string>()
const resendLeft = ref(0)
let timer: ReturnType<typeof setInterval> | undefined

const canRequest = computed(() => email.value.trim() !== '')
const canReset = computed(() => /^\d{6}$/.test(code.value.trim()) && password.value !== '')

function startTimer(seconds: number) {
  stopTimer()
  resendLeft.value = seconds
  timer = setInterval(() => {
    resendLeft.value = Math.max(0, resendLeft.value - 1)
    if (resendLeft.value === 0) stopTimer()
  }, 1000)
}

function stopTimer() {
  if (timer) clearInterval(timer)
  timer = undefined
}

onBeforeUnmount(stopTimer)

function tooMany(retryAfter: number | undefined) {
  return t('auth.tooManyAttempts', { time: formatCountdown(retryAfter ?? 60) })
}

function clearMessages() {
  emailError.value = codeError.value = passwordError.value = formError.value = notice.value = undefined
}

function changeEmail() {
  clearMessages()
  stopTimer()
  step.value = 'email'
}

async function request(again = false) {
  clearMessages()
  if (again) resending.value = true
  else loading.value = true
  const result = await props.api.request(email.value.trim())
  loading.value = resending.value = false
  if (result.status === 202) {
    const sent = result.body as { resetId: string; resendAfterSeconds: number }
    resetId.value = sent.resetId
    step.value = 'code'
    code.value = ''
    if (again) notice.value = t('auth.codeResent')
    startTimer(sent.resendAfterSeconds)
    return
  }
  if (result.status === 400) {
    emailError.value = t('auth.badEmail')
    return
  }
  if (result.status === 429 && step.value === 'code') {
    startTimer(result.retryAfter ?? 60)
    return
  }
  formError.value = result.status === 429 ? tooMany(result.retryAfter) : unreachable
}

async function reset() {
  clearMessages()
  if (password.value.trim().length < passwordMinLength) {
    passwordError.value = t('auth.passwordTooShort', { min: passwordMinLength })
    return
  }
  loading.value = true
  const result = await props.api.confirm(resetId.value, code.value.trim(), password.value)
  loading.value = false
  if (result.status === 204) {
    stopTimer()
    emit('signed-in')
    return
  }
  const problem = result.body as { code?: string; errors?: Record<string, string[]> } | undefined
  if (result.status === 400 && problem?.errors?.password) {
    passwordError.value = t('auth.passwordLength', { min: passwordMinLength })
    return
  }
  if (result.status === 400) {
    codeError.value = t('auth.badCode')
    return
  }
  formError.value = result.status === 429 ? tooMany(result.retryAfter) : unreachable
}
</script>

<template>
  <div class="password-reset" data-testid="password-reset">
    <form v-if="step === 'email'" class="password-reset__form" novalidate @submit.prevent="request()">
      <p class="password-reset__lead">
        {{ t('auth.resetLead') }}
      </p>
      <UiField
        v-model="email"
        :label="t('auth.email')"
        type="email"
        name="email"
        autocomplete="email"
        inputmode="email"
        required
        :error="emailError"
      />
      <p v-if="formError" class="password-reset__error" role="alert">{{ formError }}</p>
      <UiButton type="submit" block :loading="loading" :disabled="!canRequest">{{
        t('auth.getCode')
      }}</UiButton>
      <UiButton variant="ghost" block @click="emit('back')">{{ t('auth.backToSignIn') }}</UiButton>
    </form>

    <form v-else class="password-reset__form" novalidate @submit.prevent="reset">
      <I18nText tag="p" class="password-reset__lead" keypath="auth.resetCodeSent">
        <template #email
          ><strong>{{ email.trim() }}</strong></template
        >
      </I18nText>
      <UiField
        v-model="code"
        :label="t('auth.code')"
        name="code"
        autocomplete="one-time-code"
        inputmode="numeric"
        maxlength="6"
        required
        :error="codeError"
      />
      <UiField
        v-model="password"
        :label="t('auth.newPassword')"
        type="password"
        name="new-password"
        autocomplete="new-password"
        required
        :hint="t('auth.passwordHint', { min: passwordMinLength })"
        :error="passwordError"
      />
      <p v-if="notice" class="password-reset__notice" role="status">{{ notice }}</p>
      <p v-if="formError" class="password-reset__error" role="alert">{{ formError }}</p>
      <UiButton type="submit" block :loading="loading" :disabled="!canReset">{{
        t('auth.savePassword')
      }}</UiButton>
      <div class="password-reset__resend">
        <p v-if="resendLeft > 0" class="password-reset__timer" data-testid="resend-timer" aria-live="polite">
          {{ t('auth.resendIn', { time: formatCountdown(resendLeft) }) }}
        </p>
        <UiButton v-else variant="ghost" block :loading="resending" @click="request(true)">{{
          t('auth.resend')
        }}</UiButton>
        <UiButton variant="ghost" block @click="changeEmail">{{ t('auth.changeEmail') }}</UiButton>
      </div>
    </form>
  </div>
</template>

<style scoped>
.password-reset__form {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.password-reset__lead,
.password-reset__timer {
  margin: 0;
  color: var(--color-muted);
}

.password-reset__timer {
  font-size: var(--font-size-sm);
  text-align: center;
}

.password-reset__error {
  margin: 0;
  color: var(--color-danger);
}

.password-reset__notice {
  margin: 0;
  color: var(--color-success);
}

.password-reset__resend {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}
</style>
