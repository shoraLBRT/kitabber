<script setup lang="ts">
// Sign-in page (E03-02): the provider sends a person here before it issues a code (Identity:SignInPath, ADR-0018).
// Once the provider has signed them in (its session cookie), they go back into the authorization request that sent
// them here, or through /auth/sign-in, the usual authorization code flow: there is no other way to a token.
// The external providers (#91) come back to the same place; a failed one comes back here with `externalError`.
import SignInForm from '~/components/auth/SignInForm.vue'
import { passwordResetApi } from '~/api/password'
import { afterSignIn, signInApi } from '~/api/sign-in'
import { externalErrorMessage, externalSignInApi } from '~/api/external-sign-in'
import { t } from '../i18n'

useHead({ title: t('pages.login') })

const { $api } = useNuxtApp()
const api = signInApi($api)
const resetApi = passwordResetApi($api)
const external = externalSignInApi($api, useRuntimeConfig().public.apiBase)
const origin = useRequestURL().origin
const route = useRoute()
const externalReturnUrl = computed(() => origin + afterSignIn(route.query.ReturnUrl))
const externalError = computed(() => externalErrorMessage(route.query.externalError))

async function signedIn() {
  await navigateTo(afterSignIn(route.query.ReturnUrl), { external: true })
}
</script>

<template>
  <SignInForm
    :api="api"
    :reset-api="resetApi"
    :external="external"
    :external-return-url="externalReturnUrl"
    :external-error="externalError"
    @signed-in="signedIn"
  />
</template>
