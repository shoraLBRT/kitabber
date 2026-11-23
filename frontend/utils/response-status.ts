// The HTTP status of a page rendered on the server (E02-07, FR-UI, ADR-0057). A page whose content is not there answers
// 404 and draws «не найден» in the central column; a page whose content failed to load answers 500 and draws the
// failure with «Повторить» there, so the shell stays usable and a search engine or a monitor sees the truth. The
// addresses no page takes and errors nobody caught are error.vue's, with the same codes.

/** The status for the answer of a page's lookup (api/*.ts), or `undefined` for 200. */
export function responseStatus(lookup: { status: string } | null | undefined): 404 | 500 | undefined {
  if (lookup?.status === 'not-found') return 404
  if (lookup?.status === 'error') return 500
  return undefined
}
