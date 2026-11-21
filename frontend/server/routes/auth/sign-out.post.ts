// Signs the person out (E03-03, ADR-0024): the refresh token is revoked at the provider, the session leaves the
// site's storage and its cookie is removed, then the browser goes to the provider to end the session there too
// (identity.session) and comes back to the home page. A POST from a form of the site: with the SameSite=Lax cookie
// another site cannot sign the person out. Without a session it only ends the provider's one.
export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const stored = await endSession(event)
  const settings = oidcSettings()
  let target = '/'
  try {
    const configuration = await providerConfiguration(settings)
    if (stored) await revokeSession(configuration, stored)
    target = endSessionUrl(configuration, settings, stored?.idToken)
  } catch {
    // The provider is unreachable: the site's session is over all the same.
  }
  return sendRedirect(event, target, 303)
})
