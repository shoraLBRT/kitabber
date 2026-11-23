// Whether the browser has the network (E02-07, ADR-0057). True on the server and until the browser says otherwise;
// plugins/network.client.ts keeps it current. The shell shows «Нет подключения» while it is false.
export function useOnline() {
  return useState<boolean>('network:online', () => true)
}
