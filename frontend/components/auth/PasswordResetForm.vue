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

const props = defineProps<{ api: PasswordResetApi; email?: string }>()
const emit = defineEmits<{ 'signed-in': []; back: [] }>()

const unreachable = 'Не удалось связаться с сервером. Проверьте подключение и попробуйте ещё раз.'

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
  return `Слишком много попыток. Попробуйте через ${formatCountdown(retryAfter ?? 60)}.`
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
    if (again) notice.value = 'Отправили новый код. Прежний больше не действует.'
    startTimer(sent.resendAfterSeconds)
    return
  }
  if (result.status === 400) {
    emailError.value = 'Это не похоже на адрес почты.'
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
    passwordError.value = `Не короче ${passwordMinLength} символов.`
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
    passwordError.value = `Не короче ${passwordMinLength} и не длиннее 128 символов.`
    return
  }
  if (result.status === 400) {
    codeError.value = 'Неверный или просроченный код. Проверьте письмо или отправьте код снова.'
    return
  }
  formError.value = result.status === 429 ? tooMany(result.retryAfter) : unreachable
}
</script>

<template>
  <div class="password-reset" data-testid="password-reset">
    <form v-if="step === 'email'" class="password-reset__form" novalidate @submit.prevent="request()">
      <p class="password-reset__lead">
        Пришлём на почту аккаунта код. С ним вы зададите новый пароль, а все прежние входы в аккаунт
        закончатся.
      </p>
      <UiField
        v-model="email"
        label="Почта"
        type="email"
        name="email"
        autocomplete="email"
        inputmode="email"
        required
        :error="emailError"
      />
      <p v-if="formError" class="password-reset__error" role="alert">{{ formError }}</p>
      <UiButton type="submit" block :loading="loading" :disabled="!canRequest">Получить код</UiButton>
      <UiButton variant="ghost" block @click="emit('back')">Вернуться ко входу</UiButton>
    </form>

    <form v-else class="password-reset__form" novalidate @submit.prevent="reset">
      <p class="password-reset__lead">
        Если у <strong>{{ email.trim() }}</strong> есть аккаунт, мы отправили туда код. Он действует 15 минут.
      </p>
      <UiField
        v-model="code"
        label="Код из письма"
        name="code"
        autocomplete="one-time-code"
        inputmode="numeric"
        maxlength="6"
        required
        :error="codeError"
      />
      <UiField
        v-model="password"
        label="Новый пароль"
        type="password"
        name="new-password"
        autocomplete="new-password"
        required
        :hint="`Не короче ${passwordMinLength} символов`"
        :error="passwordError"
      />
      <p v-if="notice" class="password-reset__notice" role="status">{{ notice }}</p>
      <p v-if="formError" class="password-reset__error" role="alert">{{ formError }}</p>
      <UiButton type="submit" block :loading="loading" :disabled="!canReset"
        >Сохранить пароль и войти</UiButton
      >
      <div class="password-reset__resend">
        <p v-if="resendLeft > 0" class="password-reset__timer" data-testid="resend-timer" aria-live="polite">
          Отправить код снова можно через {{ formatCountdown(resendLeft) }}
        </p>
        <UiButton v-else variant="ghost" block :loading="resending" @click="request(true)"
          >Отправить код снова</UiButton
        >
        <UiButton variant="ghost" block @click="changeEmail">Изменить почту</UiButton>
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
