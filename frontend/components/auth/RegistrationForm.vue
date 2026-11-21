<script setup lang="ts">
// Registration (E03-01, FR-AUTH): email and password, then the code from the letter with a resend timer. Shown on the
// /register page and, `embedded`, inside the sign-in window (AuthWindow, E03-04), which gives the heading instead.
// The calls come in as `api` (api/registration.ts), so the form is tested without a server. When the code is
// accepted the provider has signed the person in and the form emits `confirmed`; the page then finishes the sign-in.
import { computed, onBeforeUnmount, ref } from 'vue'
import UiButton from '../ui/UiButton.vue'
import UiField from '../ui/UiField.vue'
import { formatCountdown, passwordMinLength, type RegistrationApi } from '../../api/registration'

const props = defineProps<{ api: RegistrationApi; embedded?: boolean }>()
const emit = defineEmits<{ confirmed: [] }>()

const unreachable = 'Не удалось связаться с сервером. Проверьте подключение и попробуйте ещё раз.'

const step = ref<'form' | 'code'>('form')
const email = ref('')
const password = ref('')
const code = ref('')
const registrationId = ref('')
const loading = ref(false)
const resending = ref(false)
const emailError = ref<string>()
const passwordError = ref<string>()
const codeError = ref<string>()
const formError = ref<string>()
const notice = ref<string>()
const resendLeft = ref(0)
let timer: ReturnType<typeof setInterval> | undefined

const canSubmit = computed(() => email.value.trim() !== '' && password.value !== '')
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

function fieldErrors(body: unknown): Record<string, unknown> {
  const errors = (body as { errors?: Record<string, unknown> } | undefined)?.errors
  return errors ?? {}
}

function tooSoon(retryAfter: number | undefined) {
  return `Слишком часто. Попробуйте через ${formatCountdown(retryAfter ?? 60)}.`
}

async function register() {
  emailError.value = passwordError.value = formError.value = undefined
  if (password.value.length < passwordMinLength) {
    passwordError.value = `Пароль — не короче ${passwordMinLength} символов.`
    return
  }
  loading.value = true
  const result = await props.api.register(email.value.trim(), password.value)
  loading.value = false
  if (result.status === 202) {
    const sent = result.body as { registrationId: string; resendAfterSeconds: number }
    registrationId.value = sent.registrationId
    step.value = 'code'
    code.value = ''
    codeError.value = notice.value = undefined
    startTimer(sent.resendAfterSeconds)
    return
  }
  if (result.status === 400) {
    const errors = fieldErrors(result.body)
    if ('email' in errors) emailError.value = 'Это не похоже на адрес почты.'
    if ('password' in errors) passwordError.value = `Пароль — от ${passwordMinLength} до 128 символов.`
    if (!emailError.value && !passwordError.value) formError.value = 'Проверьте почту и пароль.'
    return
  }
  if (result.status === 409) {
    emailError.value = 'Этот адрес уже занят другим аккаунтом.'
    return
  }
  formError.value = result.status === 429 ? tooSoon(result.retryAfter) : unreachable
}

async function resend() {
  codeError.value = notice.value = undefined
  resending.value = true
  const result = await props.api.resend(registrationId.value)
  resending.value = false
  if (result.status === 202) {
    startTimer((result.body as { resendAfterSeconds: number }).resendAfterSeconds)
    notice.value = 'Отправили новый код. Прежний больше не действует.'
    return
  }
  if (result.status === 429) {
    startTimer(result.retryAfter ?? 60)
    return
  }
  if (result.status === 404) {
    backToForm('Регистрация устарела. Начните заново.')
    return
  }
  codeError.value = unreachable
}

async function confirm() {
  codeError.value = notice.value = undefined
  loading.value = true
  const result = await props.api.confirm(registrationId.value, code.value.trim())
  loading.value = false
  if (result.status === 204) {
    stopTimer()
    emit('confirmed')
    return
  }
  if (result.status === 400) {
    codeError.value = 'Неверный или просроченный код. Проверьте письмо или отправьте код снова.'
    return
  }
  if (result.status === 409) {
    backToForm('Этот адрес уже подтвердил другой аккаунт.')
    return
  }
  codeError.value = result.status === 429 ? tooSoon(result.retryAfter) : unreachable
}

function backToForm(message?: string) {
  stopTimer()
  step.value = 'form'
  formError.value = message
}
</script>

<template>
  <section
    class="registration"
    :class="{ 'registration--embedded': embedded }"
    data-testid="registration"
    :aria-labelledby="embedded ? undefined : 'registration-title'"
  >
    <h1 v-if="!embedded" id="registration-title" class="registration__title">Регистрация</h1>

    <form v-if="step === 'form'" class="registration__form" novalidate @submit.prevent="register">
      <p class="registration__lead">Почта нужна для входа и подтверждения: на неё придёт код.</p>
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
      <UiField
        v-model="password"
        label="Пароль"
        type="password"
        name="password"
        autocomplete="new-password"
        required
        :hint="`Не короче ${passwordMinLength} символов`"
        :error="passwordError"
      />
      <p v-if="formError" class="registration__error" role="alert">{{ formError }}</p>
      <UiButton type="submit" block :loading="loading" :disabled="!canSubmit">Зарегистрироваться</UiButton>
    </form>

    <form v-else class="registration__form" novalidate @submit.prevent="confirm">
      <p class="registration__lead">
        Отправили код на <strong>{{ email.trim() }}</strong>. Он действует 30 минут.
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
      <p v-if="notice" class="registration__notice" role="status">{{ notice }}</p>
      <UiButton type="submit" block :loading="loading" :disabled="!canConfirm">Подтвердить</UiButton>
      <div class="registration__resend">
        <p v-if="resendLeft > 0" class="registration__timer" data-testid="resend-timer" aria-live="polite">
          Отправить код снова можно через {{ formatCountdown(resendLeft) }}
        </p>
        <UiButton v-else variant="ghost" block :loading="resending" @click="resend"
          >Отправить код снова</UiButton
        >
        <UiButton variant="ghost" block @click="backToForm()">Изменить почту</UiButton>
      </div>
    </form>
  </section>
</template>

<style scoped>
.registration {
  max-width: var(--size-measure);
  margin: 0 auto;
  padding: var(--space-8) var(--space-4);
  box-sizing: border-box;
}

.registration--embedded {
  padding: 0;
}

.registration__title {
  margin: 0 0 var(--space-4);
  font-size: var(--font-size-2xl);
}

.registration__form {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}

.registration__lead,
.registration__timer {
  margin: 0;
  color: var(--color-muted);
}

.registration__timer {
  font-size: var(--font-size-sm);
  text-align: center;
}

.registration__error {
  margin: 0;
  color: var(--color-danger);
}

.registration__notice {
  margin: 0;
  color: var(--color-success);
}

.registration__resend {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}
</style>
