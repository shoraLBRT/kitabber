import type { Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// The notifications (E12-02, FR-NOTIFY, ADR-0093) in both profiles against the fake API (e2e/fake-api.mjs), which keeps
// five notifications from Борис per client address, three of them unread, three to a page. On a desktop the bell in
// the header carries the counter and opens the panel; on a phone the «Уведомления» tab carries it and leads to the
// page. Opening the list marks the shown rows read, and the next page comes by the cursor. Which notifications a person
// gets and who may read them is the backend's and is tested there (NotificationApiTests).

async function signIn(page: Page): Promise<void> {
  const octet = () => Math.floor(Math.random() * 254) + 1
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': `10.${octet()}.${octet()}.${octet()}` })
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
  await waitForHydration(page)
}

test.use({ actionTimeout: 10_000 })

test.describe('уведомления', () => {
  test('счётчик непрочитанных, открытие помечает показанные прочитанными, лента подгружается по курсору', async ({
    page,
  }) => {
    await signIn(page)

    if (isTouch()) {
      const tab = page.getByTestId('tab-bar').locator('[data-nav="notifications"]')
      await expect(tab.getByTestId('tab-badge')).toHaveText('3')
      await press(tab)
      await page.waitForURL((url) => url.pathname === '/notifications')
      const list = page.getByTestId('notifications-page').getByTestId('notification-list')
      await expect(list.getByTestId('notification')).toHaveCount(3)
      await expect(tab.getByTestId('tab-badge')).toHaveText('1')
      await press(list.getByTestId('notifications-more'))
      await expect(list.getByTestId('notification')).toHaveCount(5)
      await expect(tab.getByTestId('tab-badge')).toHaveCount(0)
      await expectNoHorizontalScroll(page)
      return
    }

    const bell = page.getByTestId('notifications-bell')
    await expect(bell).toHaveAccessibleName('Уведомления, 3 непрочитанных')
    await expect(page.getByTestId('notifications-badge')).toHaveText('3')

    await press(bell)
    const panel = page.getByTestId('notifications-panel')
    const rows = panel.getByTestId('notification')
    await expect(rows).toHaveCount(3)
    await expect(rows.first()).toContainText('Борис ответил на ваш комментарий')
    await expect(rows.nth(1)).toContainText('Борис подписался на ваш блог')
    await expect(bell).toHaveAccessibleName('Уведомления, 1 непрочитанное')
    await expectNoHorizontalScroll(page)

    await page.keyboard.press('Escape')
    await expect(panel).toHaveCount(0)

    // The shown ones stay read: the panel opened again leaves one, on the next page.
    await press(bell)
    await expect(panel.getByTestId('notification').first()).toHaveAttribute('data-read', 'true')
    await press(panel.getByRole('link', { name: 'Все уведомления →' }))
    await page.waitForURL((url) => url.pathname === '/notifications')
    await expect(panel).toHaveCount(0)
    const list = page.getByTestId('notifications-page').getByTestId('notification-list')
    await expect(list.getByTestId('notification')).toHaveCount(3)
    await press(list.getByTestId('notifications-more'))
    await expect(list.getByTestId('notification')).toHaveCount(5)
    await expect(list.getByTestId('notification').nth(3)).toContainText('прокомментировал ваш пост')
    await expect(bell).toHaveAccessibleName('Уведомления')
    await expect(page.getByTestId('notifications-badge')).toHaveCount(0)
  })

  test('пустая лента говорит, что уведомлений пока нет', async ({ page }) => {
    await page.route('**/bff/notifications', (route) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: '{"items":[],"nextCursor":null}' }),
    )
    await signIn(page)
    await page.goto('/notifications')
    await waitForHydration(page)
    await expect(page.getByRole('heading', { level: 1, name: 'Уведомления' })).toBeVisible()
    await expect(page.getByTestId('notifications-empty')).toContainText('Уведомлений пока нет.')
    await expect(page).toHaveTitle(/Уведомления/)
    await expectNoHorizontalScroll(page)
  })
})

test.describe('уведомления: сбой чтения', () => {
  // The failed read is the point of the test: the browser logs its 502.
  test.use({ missingResources: ['/bff/notifications'] })

  test('пока лента читается — скелетон, при сбое — «Повторить»', async ({ page }) => {
    let fail = true
    let release: () => void = () => {}
    const held = new Promise<void>((resolve) => (release = resolve))
    await page.route('**/bff/notifications', async (route) => {
      if (fail) {
        fail = false
        await route.fulfill({ status: 502, body: 'null', contentType: 'application/json' })
        return
      }
      await held
      await route.continue()
    })
    await signIn(page)
    await page.goto('/notifications')
    await waitForHydration(page)
    const list = page.getByTestId('notification-list')
    await expect(list.getByRole('alert')).toHaveText('Не удалось загрузить уведомления')

    await press(list.getByRole('button', { name: 'Повторить' }))
    await expect(list.getByTestId('notifications-loading')).toBeVisible()
    release()
    await expect(list.getByTestId('notification')).toHaveCount(3)
  })
})
