<script setup lang="ts">
// Registration page (E03-01). After the code is accepted the provider has signed the person in (its session cookie),
// so /auth/sign-in runs the usual authorization code flow without asking again and brings them back signed in.
import RegistrationForm from '~/components/auth/RegistrationForm.vue'
import { registrationApi } from '~/utils/registration'

useHead({ title: 'Регистрация' })

const { $api } = useNuxtApp()
const api = registrationApi($api)

async function signIn() {
  await navigateTo('/auth/sign-in?returnTo=/', { external: true })
}
</script>

<template>
  <RegistrationForm :api="api" @confirmed="signIn" />
</template>
