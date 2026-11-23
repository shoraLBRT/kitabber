// «Нет интернета» (E02-07, FR-UI, docs/ui/README.md, ADR-0057): what the site reads again when the network comes back.
// Every lookup of the site answers a failure as data, `{ status: 'error' }` (api/*.ts), not as a thrown error; so the
// keys of `useAsyncData` holding such an answer are the content that did not load, and only those are read again —
// what did load stays as it is, with the reader's scroll position and the pages of a feed already read.

/** The keys of the loaded data whose answer is a failure. */
export function failedKeys(data: Record<string, unknown>): string[] {
  return Object.entries(data)
    .filter(([, value]) => isFailure(value))
    .map(([key]) => key)
}

function isFailure(value: unknown): boolean {
  return typeof value === 'object' && value !== null && (value as { status?: unknown }).status === 'error'
}

/** Follows the browser's online and offline events; returns the function that stops following them. */
export function watchNetwork(
  target: Pick<Window, 'addEventListener' | 'removeEventListener'>,
  onChange: (online: boolean) => void,
): () => void {
  const online = () => onChange(true)
  const offline = () => onChange(false)
  target.addEventListener('online', online)
  target.addEventListener('offline', offline)
  return () => {
    target.removeEventListener('online', online)
    target.removeEventListener('offline', offline)
  }
}
