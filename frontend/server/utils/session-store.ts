// The site's sessions in server storage (ADR-0024): Redis on the stands, so every instance of the site sees every
// session and a restart signs nobody out; memory in development and the e2e run. The store renews the access token
// when it is about to expire — once per session at a time in this process; another instance that renewed it first is
// noticed by reading the session again — and forgets a session the provider no longer accepts.
// It knows no request, so the unit tests reach it; server/utils/auth-session.ts binds it to the cookie.
import type { Storage } from 'unstorage'
import { needsRefresh, type StoredSession } from './oidc'

export type Refresh = (stored: StoredSession) => Promise<StoredSession | null>

export interface ReadResult {
  session: StoredSession
  /** The tokens were renewed in this read: the cookie's lifetime starts again. */
  renewed: boolean
}

export class SessionStore {
  private readonly renewing = new Map<string, Promise<StoredSession | null>>()

  constructor(
    private readonly storage: () => Storage<StoredSession>,
    private readonly refresh: Refresh,
    private readonly now: () => number = Date.now,
  ) {}

  async save(id: string, stored: StoredSession): Promise<void> {
    const ttl = Math.max(1, Math.ceil((stored.expiresAt - this.now()) / 1000))
    await this.storage().setItem(id, stored, { ttl })
  }

  async remove(id: string): Promise<void> {
    await this.storage().removeItem(id)
  }

  /** The session, renewed if its access token is about to expire; null when there is none or it has ended. */
  async read(id: string): Promise<ReadResult | null> {
    const stored = await this.live(id)
    if (!stored) return null
    if (!needsRefresh(stored, this.now())) return { session: stored, renewed: false }
    return this.renew(id, stored)
  }

  /**
   * Renews the tokens now, even if they look valid: the API has just refused `refused`. If the session already holds
   * another access token, renewed meanwhile, that one is given instead.
   */
  async renewNow(id: string, refused: string): Promise<ReadResult | null> {
    const stored = await this.live(id)
    return stored ? this.renew(id, { ...stored, accessToken: refused }, true) : null
  }

  private async live(id: string): Promise<StoredSession | null> {
    const stored = await this.storage().getItem(id)
    if (stored && stored.expiresAt > this.now()) return stored
    if (stored) await this.remove(id)
    return null
  }

  private async renew(id: string, stored: StoredSession, force = false): Promise<ReadResult | null> {
    let pending = this.renewing.get(id)
    if (!pending) {
      pending = this.renewOnce(id, stored, force).finally(() => this.renewing.delete(id))
      this.renewing.set(id, pending)
    }
    try {
      const session = await pending
      return session ? { session, renewed: true } : null
    } catch {
      // The provider is unreachable: the session stays; the old access token serves while it is valid.
      return stored.accessExpiresAt > this.now() ? { session: stored, renewed: false } : null
    }
  }

  private async renewOnce(id: string, stored: StoredSession, force: boolean): Promise<StoredSession | null> {
    // Another instance may have renewed it meanwhile: its tokens are the current ones.
    const current = await this.live(id)
    // Signed out meanwhile (another tab, another instance): nothing to renew.
    if (!current) return null
    if (!force && !needsRefresh(current, this.now())) return current
    if (force && current.accessToken !== stored.accessToken) return current
    const renewed = await this.refresh(current)
    if (!renewed) {
      await this.remove(id)
      return null
    }
    await this.save(id, renewed)
    return renewed
  }
}
