// The provider sends the person back here with a code (E18-02). The code is exchanged on the server with the PKCE
// verifier; the tokens stay on the server, the browser gets only the session cookie and goes back to its page.
export default defineEventHandler(async (event) => {
  setResponseHeader(event, 'cache-control', 'no-store')
  const flow = await takeFlow(event)
  if (!flow) throw createError({ statusCode: 400, message: 'Вход не удался: начните вход заново' })
  const query = getRequestURL(event).search
  if (getQuery(event).error) {
    throw createError({ statusCode: 400, message: 'Вход не удался: провайдер отказал' })
  }
  const settings = oidcSettings()
  let stored
  try {
    stored = await finishSignIn(await providerConfiguration(settings), settings, flow, query, Date.now())
  } catch {
    // A wrong state, nonce, issuer or verifier, or a used code: never a session.
    throw createError({ statusCode: 400, message: 'Вход не удался: начните вход заново' })
  }
  await startSession(event, stored)
  return sendRedirect(event, flow.returnTo, 302)
})
