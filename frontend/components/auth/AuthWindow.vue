<script setup lang="ts">
// The sign-in and registration window (E03-04, ADR-0032, FR-AUTH): a modal dialog on the desktop and a sheet over
// the whole screen on a phone (UiModal, narrower than 640 px), with the sign-in and the registration forms of E03-01
// and E03-02 and a link between them. The calls come in as props, so the window is tested without a server; once the
// provider has signed the person in, the window emits `signed-in` and its host finishes the sign-in.
import { computed } from 'vue'
import UiModal from '../ui/UiModal.vue'
import RegistrationForm from './RegistrationForm.vue'
import SignInForm from './SignInForm.vue'
import type { RegistrationApi } from '../../api/registration'
import type { SignInApi } from '../../api/sign-in'
import type { AuthWindowMode } from '../../composables/useAuthWindow'

defineProps<{ signInApi: SignInApi; registrationApi: RegistrationApi }>()
const emit = defineEmits<{ 'signed-in': [] }>()

const open = defineModel<boolean>('open', { default: false })
const mode = defineModel<AuthWindowMode>('mode', { default: 'sign-in' })

const title = computed(() => (mode.value === 'sign-in' ? 'Вход' : 'Регистрация'))
</script>

<template>
  <UiModal v-model:open="open" :title="title" fullscreen>
    <div class="auth-window" data-testid="auth-window">
      <template v-if="mode === 'sign-in'">
        <SignInForm :api="signInApi" embedded @signed-in="emit('signed-in')" />
        <p class="auth-window__switch">
          Нет аккаунта?
          <button type="button" class="auth-window__link" @click="mode = 'registration'">
            Зарегистрироваться
          </button>
        </p>
      </template>
      <template v-else>
        <RegistrationForm :api="registrationApi" embedded @confirmed="emit('signed-in')" />
        <p class="auth-window__switch">
          Уже есть аккаунт?
          <button type="button" class="auth-window__link" @click="mode = 'sign-in'">Войти</button>
        </p>
      </template>
    </div>
  </UiModal>
</template>

<style scoped>
.auth-window__switch {
  margin: var(--space-6) 0 0;
  color: var(--color-muted);
  text-align: center;
}

.auth-window__link {
  padding: 0;
  border: 0;
  background: none;
  color: var(--color-accent);
  font: inherit;
  text-decoration: underline;
  cursor: pointer;
}

.auth-window__link:hover {
  color: var(--color-accent-hover);
}
</style>
