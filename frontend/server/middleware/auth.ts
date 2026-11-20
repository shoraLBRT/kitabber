// Every request learns whether the person is signed in (E18-02): event.context.auth holds the server-side session,
// so pages render the signed-in shell and the API client sends the access token from the server (plugins/api.ts).
export default defineEventHandler(async (event) => {
  event.context.auth = await readSession(event)
})
