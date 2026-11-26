// The key of an account's search history in the browser (ADR-0097): HMAC-SHA-256 of the account id on the site's
// session password, its first 16 bytes in base64url. Stable for the account, different for another, and the page never
// learns the account_id (FR-ID).
import { createHmac } from 'node:crypto'

export function searchHistoryKeyOf(accountId: string, secret: string): string {
  return createHmac('sha256', secret)
    .update(accountId.toLowerCase())
    .digest()
    .subarray(0, 16)
    .toString('base64url')
}
