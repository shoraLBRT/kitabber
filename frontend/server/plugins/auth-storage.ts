// Where the site keeps its sessions (ADR-0024): Redis when NUXT_SESSION_REDIS_URL is set (its password apart, in
// NUXT_SESSION_REDIS_PASSWORD, so it needs no escaping in the address) — the stands, so every
// instance of the site shares the sessions and a restart or a deploy signs nobody out — otherwise the process memory
// (development, the e2e run), where a restart ends every session on the site (not at the provider).
import redisDriver from 'unstorage/drivers/redis'
import { sessionStorageMount } from '../utils/auth-session'

export default defineNitroPlugin(() => {
  const { sessionRedisUrl: url, sessionRedisPassword: password } = useRuntimeConfig()
  if (typeof url === 'string' && url.length > 0) {
    useStorage().mount(
      sessionStorageMount,
      redisDriver({
        url,
        ...(typeof password === 'string' && password.length > 0 ? { password } : {}),
        base: 'site:auth-sessions',
      }),
    )
  } else if (!import.meta.dev) {
    console.warn('NUXT_SESSION_REDIS_URL is not set: sessions are kept in memory and end with the process')
  }
})
