<script setup lang="ts">
// Changing the password in the settings (E03-05, FR-AUTH, ADR-0036): the current password and the new one. The
// sessions stay, and the owner gets a letter about the change. An account without a password (signed up by a code)
// sets one by the reset from the sign-in form, which the hint says. The call comes in as `api` (api/password.ts), so
// the form is tested without a server.
import { computed, ref } from 'vue'
import UiButton from '../ui/UiButton.vue'
import UiField from '../ui/UiField.vue'
import { passwordMinLength } from '../../api/registration'
import type { PasswordChangeApi } from '../../api/password'

const props = defineProps<{ api: PasswordChangeApi }>()

const current = ref('')
const next = ref('')
const loading = ref(false)
const currentError = ref<string>()
const nextError = ref<string>()
const formError = ref<string>()
const done = ref(false)

const canSave = computed(() => current.value !== '' && next.value !== '')

async function save() {
  currentError.value = nextError.value = formError.value = undefined
  done.value = false
  if (next.value.trim().length < passwordMinLength) {
    nextError.value = `Не короче ${passwordMinLength} символов.`
    return
  }
  loading.value = true
  const result = await props.api.change(current.value, next.value)
  loading.value = false
  if (result.status === 204) {
    current.value = next.value = ''
    done.value = true
    return
  }
  const problem = result.body as { code?: string; errors?: Record<string, string[]> } | undefined
  if (result.status === 400 && problem?.code === 'current_password_invalid') {
    currentError.value = 'Неверный пароль.'
    return
  }
  if (result.status === 400 && problem?.errors?.newPassword) {
    nextError.value = `Не короче ${passwordMinLength} и не длиннее 128 символов.`
    return
  }
  if (result.status === 401) {
    formError.value = 'Сессия закончилась. Войдите снова.'
    return
  }
  formError.value =
    result.status === 429
      ? 'Слишком много попыток. Попробуйте позже.'
      : 'Не удалось сохранить пароль. Проверьте подключение и попробуйте ещё раз.'
}
</script>

<template>
  <form class="password-change" data-testid="password-change" novalidate @submit.prevent="save">
    <h2 class="password-change__title">Пароль</h2>
    <UiField
      v-model="current"
      label="Текущий пароль"
      type="password"
      name="current-password"
      autocomplete="current-password"
      required
      hint="Нет пароля? Задайте его через «Забыли пароль?» на странице входа."
      :error="currentError"
    />
    <UiField
      v-model="next"
      label="Новый пароль"
      type="password"
      name="new-password"
      autocomplete="new-password"
      required
      :hint="`Не короче ${passwordMinLength} символов`"
      :error="nextError"
    />
    <p v-if="formError" class="password-change__error" role="alert">{{ formError }}</p>
    <p v-if="done" class="password-change__notice" role="status">
      Пароль изменён. Мы отправили письмо об этом на почту аккаунта.
    </p>
    <div class="password-change__actions">
      <UiButton type="submit" :loading="loading" :disabled="!canSave">Сменить пароль</UiButton>
    </div>
  </form>
</template>

<style scoped>
.password-change {
  display: flex;
  flex-direction: column;
  gap: var(--space-5);
}

.password-change__title {
  margin: 0;
  font-size: var(--font-size-lg);
}

.password-change__error {
  margin: 0;
  color: var(--color-danger);
}

.password-change__notice {
  margin: 0;
  color: var(--color-success);
}

.password-change__actions {
  display: flex;
  justify-content: flex-end;
}
</style>
