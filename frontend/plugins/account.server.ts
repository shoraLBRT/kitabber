// Passes the server-side session to the page as signed in or not (composables/useAccount.ts).
export default defineNuxtPlugin(() => {
  const event = useRequestEvent()
  useAccount().value = { signedIn: Boolean(event?.context.auth) }
})
