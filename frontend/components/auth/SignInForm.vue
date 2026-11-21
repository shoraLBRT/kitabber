<script setup lang="ts">
// Sign-in (E03-02, FR-AUTH): with the email and the password, or with a one-time code from a letter and a resend
// timer. Shown on the /login page and, `embedded`, inside the sign-in window (AuthWindow, E03-04): there the window
// gives the heading and the link to the registration, so the form leaves out its own.
// The calls come in as `api` (api/sign-in.ts), so the form is tested without a server. Once the provider has signed
// the person in the form emits `signed-in`; the page then finishes the sign-in. No message tells whether an address
// is registered: the API does not say it either. With `resetApi` the password mode offers «Забыли пароль?», which
// turns the form into the password reset (PasswordResetForm, E03-05); the reset signs in too.
import { computed, onBeforeUnmount, ref } from 'vue'
import UiButton from '../ui/UiButton.vue'
import UiField from '../ui/UiField.vue'
import { formatCountdown } from '../../api/registration'
import type { PasswordResetApi } from '../../api/password'
import type { SignInApi } from '../../api/sign-in'
import PasswordResetForm from './PasswordResetForm.vue'

const props = defineProps<{ api: SignInApi; resetApi?: PasswordResetApi; embedded?: boolean }>()
const emit = defineEmits<{ 'signed-in': [] }>()

const unreachable = 'Не удалось связаться с сервером. Проверьте подключение и попробуйте ещё раз.'

const mode = ref<'password' | 'code' | 'reset'>('password')
const step = ref<'email' | 'code'>('email')
const email = ref('')
const password = ref('')
const code = ref('')
const challengeId = ref('')
const loading = ref(false)
const resending = ref(false)
const emailError = ref<string>()
const passwordError = ref<string>()
const codeError = ref<string>()
const formError = ref<string>()
const notice = ref<string>()
const resendLeft = ref(0)
let timer: ReturnType<typeof setInterval> | undefined

const canSignIn = computed(() => email.value.trim() !== '' && password.value !== '')
const canRequest = computed(() => email.value.trim() !== '')
const canConfirm = computed(() => /^\d{6}$/.test(code.value.trim()))

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
  emailError.value = passwordError.value = codeError.value = formError.value = notice.value = undefined
}

function switchMode(next: 'password' | 'code' | 'reset') {
  clearMessages()
  stopTimer()
  mode.value = next
  step.value = 'email'
}

async function signInWithPassword() {
  clearMessages()
  loading.value = true
  const result = await props.api.withPassword(email.value.trim(), password.value)
  loading.value = false
  if (result.status === 204) {
    emit('signed-in')
    return
  }
  if (result.status === 400) {
    passwordError.value = 'Неверная почта или пароль.'
    return
  }
  formError.value = result.status === 429 ? tooMany(result.retryAfter) : unreachable
}

async function requestCode(again = false) {
  clearMessages()
  if (again) resending.value = true
  else loading.value = true
  const result = await props.api.requestCode(email.value.trim())
  loading.value = resending.value = false
  if (result.status === 202) {
    const sent = result.body as { challengeId: string; resendAfterSeconds: number }
    challengeId.value = sent.challengeId
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

async function signInWithCode() {
  clearMessages()
  loading.value = true
  const result = await props.api.withCode(challengeId.value, code.value.trim())
  loading.value = false
  if (result.status === 204) {
    stopTimer()
    emit('signed-in')
    return
  }
  if (result.status === 400) {
    codeError.value = 'Неверный или просроченный код. Проверьте письмо или отправьте код снова.'
    return
  }
  codeError.value = result.status === 429 ? tooMany(result.retryAfter) : unreachable
}
</script>

<template>
  <section
    class="sign-in"
    :class="{ 'sign-in--embedded': embedded }"
    data-testid="sign-in"
    :aria-labelledby="embedded ? undefined : 'sign-in-title'"
  >
    <h1 v-if="!embedded" id="sign-in-title" class="sign-in__title">
      {{ mode === 'reset' ? 'Восстановление пароля' : 'Вход' }}
    </h1>

    <form v-if="mode === 'password'" class="sign-in__form" novalidate @submit.prevent="signInWithPassword">
      <UiField
        v-model="email"
        label="Почта"
        type="email"
        name="email"
        autocomplete="username"
        inputmode="email"
        required
      />
      <UiField
        v-model="password"
        label="Пароль"
        type="password"
        name="password"
        autocomplete="current-password"
        required
        :error="passwordError"
      />
      <p v-if="formError" class="sign-in__error" role="alert">{{ formError }}</p>
      <UiButton type="submit" block :loading="loading" :disabled="!canSignIn">Войти</UiButton>
      <UiButton variant="ghost" block @click="switchMode('code')">Войти по коду из письма</UiButton>
      <UiButton v-if="resetApi" variant="ghost" block @click="switchMode('reset')">Забыли пароль?</UiButton>
    </form>

    <PasswordResetForm
      v-else-if="mode === 'reset' && resetApi"
      :api="resetApi"
      :email="email.trim()"
      @signed-in="emit('signed-in')"
      @back="switchMode('password')"
    />

    <form v-else-if="step === 'email'" class="sign-in__form" novalidate @submit.prevent="requestCode()">
      <p class="sign-in__lead">Пришлём на почту код для входа. Если аккаунта ещё нет, код его создаст.</p>
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
      <p v-if="formError" class="sign-in__error" role="alert">{{ formError }}</p>
      <UiButton type="submit" block :loading="loading" :disabled="!canRequest">Получить код</UiButton>
      <UiButton variant="ghost" block @click="switchMode('password')">Войти с паролем</UiButton>
    </form>

    <form v-else class="sign-in__form" novalidate @submit.prevent="signInWithCode">
      <p class="sign-in__lead">
        Отправили код на <strong>{{ email.trim() }}</strong>. Он действует 10 минут.
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
      <p v-if="notice" class="sign-in__notice" role="status">{{ notice }}</p>
      <UiButton type="submit" block :loading="loading" :disabled="!canConfirm">Войти</UiButton>
      <div class="sign-in__resend">
        <p v-if="resendLeft > 0" class="sign-in__timer" data-testid="resend-timer" aria-live="polite">
          Отправить код снова можно через {{ formatCountdown(resendLeft) }}
        </p>
        <UiButton v-else variant="ghost" block :loading="resending" @click="requestCode(true)"
          >Отправить код снова</UiButton
        >
        <UiButton variant="ghost" block @click="switchMode('code')">Изменить почту</UiButton>
      </div>
    </form>

    <p v-if="!embedded" class="sign-in__footer">
      Нет аккаунта? <NuxtLink to="/register">Зарегистрироваться</NuxtLink>
    </p>
  </section>
</template>

<style scoped>
.sign-in {
  max-width: var(--size-measure);
  margin: 0 auto;
  padding: var(--space-8) var(--space-4);
  box-sizing: border-box;
}

.sign-in--embedded {
  padding: 0;
}

.sign-in__title {
  margin: 0 0 var(--space-4);
  font-size: var(--font-size-2xl);
}

.sign-in__form {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.sign-in__lead,
.sign-in__timer,
.sign-in__footer {
  margin: 0;
  color: var(--color-muted);
}

.sign-in__timer {
  font-size: var(--font-size-sm);
  text-align: center;
}

.sign-in__footer {
  margin-top: var(--space-6);
  text-align: center;
}

.sign-in__error {
  margin: 0;
  color: var(--color-danger);
}

.sign-in__notice {
  margin: 0;
  color: var(--color-success);
}

.sign-in__resend {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}
</style>
