import type { StoredSession } from './utils/oidc'

declare module 'h3' {
  interface H3EventContext {
    /** The signed-in person's session on the server, or null (server/middleware/auth.ts). */
    auth?: StoredSession | null
  }
}

export {}
