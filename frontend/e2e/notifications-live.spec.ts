import { randomUUID } from 'node:crypto'
import type { APIRequestContext, Locator, Page } from '@playwright/test'
import { expect, isTouch, press, test, waitForHydration } from './fixtures'

// The notifications in real time (E12-04, FR-NOTIFY, ADR-0123) in both profiles against the fake API
// (e2e/fake-api.mjs): a new notification raises the counter without a reload, and rows read in one tab lower the
// counter of the person's other tab. The fake API tells the tabs of a client address `notifications.changed`, as the
// workers host and the API do; who gets the event is the backend's and is tested there (NotificationGenerationTests,
// NotificationApiTests).
const api = 'http://127.0.0.1:3102'

interface Client {
  address: string
  id: string
}

function newClient(): Client {
  const octet = () => Math.floor(Math.random() * 254) + 1
  return { address: `10.${octet()}.${octet()}.${octet()}`, id: randomUUID() }
}

async function open(page: Page, client: Client, path: string): Promise<void> {
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': client.address, 'x-e2e-client': client.id })
  await page.goto(path)
}

async function signIn(page: Page, client: Client): Promise<void> {
  await open(page, client, isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
  await waitForHydration(page)
}

// An event sent before the channel is up is lost, so the test waits for the tabs' connections.
async function connected(request: APIRequestContext, client: Client, tabs: number): Promise<void> {
  await expect
    .poll(async () => {
      const state = await (await request.get(`${api}/__realtime?client=${client.id}`)).json()
      return (state.connections as { open: boolean; handshake: boolean }[]).filter(
        (c) => c.open && c.handshake,
      ).length
    })
    .toBe(tabs)
}

function badge(page: Page): Locator {
  return isTouch()
    ? page.getByTestId('tab-bar').locator('[data-nav="notifications"]').getByTestId('tab-badge')
    : page.getByTestId('notifications-badge')
}

test.use({ actionTimeout: 10_000 })

test.describe('уведомления в реальном времени', () => {
  test('новое уведомление увеличивает счётчик без перезагрузки', async ({ page, request }) => {
    const client = newClient()
    await signIn(page, client)
    await expect(badge(page)).toHaveText('3')
    await connected(request, client, 1)

    await request.post(`${api}/__notification-arrival`, { headers: { 'x-forwarded-for': client.address } })

    await expect(badge(page)).toHaveText('4')
    await expect(page.getByTestId('notifications-arrived')).toHaveText('Новое уведомление. Непрочитанных: 4')
    if (!isTouch())
      await expect(page.getByTestId('notifications-bell')).toHaveAccessibleName(/4 непрочитанных/u)
  })

  test('прочитанное в одной вкладке гасит счётчик в другой', async ({ page, context, request }) => {
    const client = newClient()
    await signIn(page, client)
    const other = await context.newPage()
    await open(other, client, '/')
    await waitForHydration(other)
    await expect(badge(page)).toHaveText('3')
    await expect(badge(other)).toHaveText('3')
    await connected(request, client, 2)

    // The list shown in the first tab marks its rows read; «Прочитать все» marks the rest. The list opens without a
    // reload (the bell's panel, the phone's tab), so no connection is cut halfway.
    let list: Locator
    if (isTouch()) {
      await press(page.getByTestId('tab-bar').locator('[data-nav="notifications"]'))
      await page.waitForURL((url) => url.pathname === '/notifications')
      list = page.getByTestId('notifications-page').getByTestId('notification-list')
    } else {
      await press(page.getByTestId('notifications-bell'))
      list = page.getByTestId('notifications-panel').getByTestId('notification-list')
    }
    await expect(list.getByTestId('notification')).toHaveCount(3)
    await expect(badge(other)).toHaveText('1')
    await press(list.getByTestId('notifications-read-all'))

    await expect(badge(page)).toHaveCount(0)
    await expect(badge(other)).toHaveCount(0)
    // A fall is not announced.
    await expect(other.getByTestId('notifications-arrived')).toHaveText('')
  })
})
