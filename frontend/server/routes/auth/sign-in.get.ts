// Starts the sign-in (E18-02): a new PKCE verifier, state and nonce, remembered in a sealed cookie for ten minutes,
// then the redirect to the provider's authorization endpoint. `returnTo` is the page to come back to.
export default defineEventHandler(async (event) => {
  const settings = oidcSettings()
  const configuration = await providerConfiguration(settings)
  const { url, flow } = await startSignIn(configuration, settings, getQuery(event).returnTo)
  await rememberFlow(event, flow)
  setResponseHeader(event, 'cache-control', 'no-store')
  return sendRedirect(event, url.href, 302)
})
