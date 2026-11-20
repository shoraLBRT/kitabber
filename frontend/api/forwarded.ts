// The client address for API calls made during server rendering (E01-10, ADR-0016). The API counts rate limits by
// account or by client IP; without this header every anonymous page rendered by Nuxt would count as one client,
// the Nuxt container. The API believes X-Forwarded-For only from its trusted proxy networks (Http:TrustedProxies).

/** The headers that carry the incoming `x-forwarded-for` on to the API; empty when the page request had none. */
export function forwardedHeaders(incoming: string | null | undefined): Record<string, string> {
  const value = incoming?.trim()
  return value ? { 'x-forwarded-for': value } : {}
}
