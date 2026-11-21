<script setup lang="ts">
// The page's one sign-in window (E03-04, ADR-0032), opened through useAuthWindow / useRequireAccount. Once the
// provider has signed the person in (its session cookie), the action that opened the window is remembered for this
// page and the site signs in through /auth/sign-in, the usual authorization code flow, back to this same page.
import AuthWindow from './AuthWindow.vue'
import { registrationApi } from '../../api/registration'
import { signInApi } from '../../api/sign-in'
import type { AuthWindowMode } from '../../composables/useAuthWindow'

const { $api } = useNuxtApp()
const signIn = signInApi($api)
const registration = registrationApi($api)
const state = useAuthWindow()
const route = useRoute()

const open = computed({
  get: () => state.value.open,
  set: (value: boolean) => {
    state.value = value ? { ...state.value, open: true } : { open: false, mode: 'sign-in' }
  },
})
const mode = computed({
  get: () => state.value.mode,
  set: (value: AuthWindowMode) => {
    state.value = { ...state.value, mode: value }
  },
})

async function signedIn() {
  const path = route.fullPath
  rememberAuthAction(state.value.action, path)
  await navigateTo(`/auth/sign-in?returnTo=${encodeURIComponent(path)}`, { external: true })
}
</script>

<template>
  <AuthWindow
    v-model:open="open"
    v-model:mode="mode"
    :sign-in-api="signIn"
    :registration-api="registration"
    @signed-in="signedIn"
  />
</template>
