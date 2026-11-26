import { randomUUID } from 'node:crypto'
import type { APIRequestContext, Page } from '@playwright/test'
import { expect, isTouch, press, test } from './fixtures'

// The realtime channel in the browser (E01-11, ADR-0019) in both profiles, against the fake API (e2e/fake-api.mjs):
// a signed-in page opens the hub with a ticket from the site's server, the browser never sees the access token, and
// after the connection breaks the page reconnects on its own with a new ticket — silently, with no error logged.
const api = 'http://127.0.0.1:3102'

interface Connection {
  ticket: string
  open: boolean
  handshake: boolean
}

async function connections(request: APIRequestContext, client: string): Promise<Connection[]> {
  const state = await (await request.get(`${api}/__realtime?client=${client}`)).json()
  return state.connections as Connection[]
}

async function signIn(page: Page): Promise<void> {
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

test.describe('канал реального времени', () => {
  test('гость не подключается', async ({ page, request }) => {
    const client = randomUUID()
    await page.setExtraHTTPHeaders({ 'x-e2e-client': client })
    await page.goto('/')
    await page.waitForTimeout(500)
    expect(await connections(request, client)).toEqual([])
  })

  test('вошедший подключается по билету и переподключается после обрыва', async ({ page, request }) => {
    const client = randomUUID()
    await page.setExtraHTTPHeaders({ 'x-e2e-client': client })
    await signIn(page)

    await expect
      .poll(async () => (await connections(request, client)).filter((c) => c.open && c.handshake).length)
      .toBe(1)
    const [first] = await connections(request, client)

    // Nothing a script can read holds the ticket's source, the access token; the ticket itself is short-lived. The
    // only cookie a script sees is the cookie choice (E02-13, ADR-0066), which holds no secret.
    const cookies = await page.evaluate(() => document.cookie)
    expect(cookies).toBe('cookie_consent=v1.analytics-0')

    await request.post(`${api}/__realtime/drop?client=${client}`)

    await expect
      .poll(async () => (await connections(request, client)).filter((c) => c.open && c.handshake).length, {
        timeout: 15_000,
      })
      .toBe(1)
    const reconnected = (await connections(request, client)).find((c) => c.open)!
    expect(reconnected.ticket).not.toBe(first!.ticket)

    // The page still works on the new connection: an event reaches it without an error.
    await request.post(`${api}/__realtime/push?client=${client}`, {
      data: { topic: 'account:x', type: 'notification.created', data: null },
    })
  })
})
