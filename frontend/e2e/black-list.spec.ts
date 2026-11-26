import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'
import type { Page } from '@playwright/test'

// The black list in «Приватность и блокировки» (E11-06) in both profiles, against the fake API (e2e/fake-api.mjs),
// which keeps the list per client address with Борис blocked. That a blocked account's posts leave the feeds, its
// comments come folded and it may not reply is tested by the backend (SharedFeedExclusionsApiTests,
// CommentBlockApiTests).
function newClient(): string {
  const octet = () => Math.floor(Math.random() * 254) + 1
  return `10.${octet()}.${octet()}.${octet()}`
}

async function openPrivacySettings(page: Page): Promise<void> {
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': newClient() })
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
  await page.goto('/settings/privacy')
  await waitForHydration(page)
}

test.describe('чёрный список', () => {
  test('видно заблокированных, «Разблокировать» убирает из списка, после чего список пуст', async ({
    page,
  }) => {
    await openPrivacySettings(page)
    await expect(page.getByRole('heading', { level: 1, name: 'Приватность и блокировки' })).toBeVisible()
    const section = page.getByTestId('block-list')
    const rows = section.getByTestId('blocked-account')
    await expect(rows).toHaveCount(1)
    await expect(rows.first()).toContainText('Борис')
    await expect(rows.first()).toContainText('В списке с 25 ноября 2026')
    await expectNoHorizontalScroll(page)

    await press(section.getByRole('button', { name: 'Разблокировать Борис' }))
    await expect(section.getByRole('status')).toHaveText('Борис больше не в чёрном списке.')
    await expect(rows).toHaveCount(0)
    await expect(section.getByTestId('block-list-empty')).toBeVisible()

    // The list is the account's: it stays empty after a reload.
    await page.reload()
    await waitForHydration(page)
    await expect(page.getByTestId('block-list-empty')).toBeVisible()
  })
})

test.describe('чёрный список: сбой чтения', () => {
  // The failed read is the point of the test: the browser logs its 502.
  test.use({ missingResources: ['/bff/blocks'] })

  test('пока список читается — скелетон, при сбое — «Повторить»', async ({ page }) => {
    let fail = true
    let release: () => void = () => {}
    const held = new Promise<void>((resolve) => (release = resolve))
    await page.route('**/bff/blocks', async (route) => {
      if (fail) {
        fail = false
        await route.fulfill({ status: 502, body: 'null', contentType: 'application/json' })
        return
      }
      await held
      await route.continue()
    })
    await openPrivacySettings(page)
    const section = page.getByTestId('block-list')
    await expect(section.getByRole('alert')).toHaveText('Не удалось загрузить чёрный список.')

    await press(section.getByRole('button', { name: 'Повторить' }))
    await expect(section.getByTestId('block-list-loading')).toBeVisible()
    release()
    await expect(section.getByTestId('blocked-account')).toHaveCount(1)
  })
})
