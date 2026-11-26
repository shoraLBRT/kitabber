<script setup lang="ts">
// Sign-in page (E03-02): the provider sends a person here before it issues a code (Identity:SignInPath, ADR-0018).
// Once the provider has signed them in (its session cookie), they go back into the authorization request that sent
// them here, or through /auth/sign-in, the usual authorization code flow: there is no other way to a token.
import SignInForm from '~/components/auth/SignInForm.vue'
import { passwordResetApi } from '~/api/password'
import { afterSignIn, signInApi } from '~/api/sign-in'
import { t } from '../i18n'

useHead({ title: t('pages.login') })

const { $api } = useNuxtApp()
const api = signInApi($api)
const resetApi = passwordResetApi($api)
const route = useRoute()

async function signedIn() {
  await navigateTo(afterSignIn(route.query.ReturnUrl), { external: true })
}
</script>

<template>
  <SignInForm :api="api" :reset-api="resetApi" @signed-in="signedIn" />
</template>
