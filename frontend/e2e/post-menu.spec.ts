import type { Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// The «…» menu of the post page (E07-05, FR-POST, ADR-0053) in both profiles against the fake API (e2e/fake-api.mjs):
// a dropdown menu on a wide screen, a bottom sheet on a phone. What it holds depends on who reads; the items whose
// feature is not built (edit, report, hide) stay hidden behind their flags, which are off here; deleting asks first.
// The items by flag and the failure states of the menu itself are in tests/post/PostMenu.spec.ts.

const annas = '/u/anna/0195f3a0-0000-7000-8000-0000000000c1-privet-mir'
const boriss = '/books/0195f3a0-0000-7000-8000-0000000000c3-knigi-oseni'

async function signIn(page: Page): Promise<void> {
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

async function openPost(page: Page, address: string): Promise<void> {
  await page.goto(address)
  await waitForHydration(page)
  await expect(page.getByTestId('post')).toBeVisible()
}

/** Opens the menu and gives its items: the menu items on a wide screen, the buttons of the sheet on a phone. */
async function openMenu(page: Page) {
  await press(page.getByTestId('post-header').getByRole('button', { name: 'Меню поста' }))
  return isTouch()
    ? page.getByRole('dialog', { name: 'Меню поста' }).getByTestId('post-menu-sheet').getByRole('button')
    : page.getByRole('menu', { name: 'Меню поста' }).getByRole('menuitem')
}

const confirmDialog = (page: Page) => page.getByRole('dialog', { name: 'Удалить пост?' })

// A stuck tap fails on its own, with its call log, instead of running into the test's timeout.
test.use({ actionTimeout: 10_000 })

test.describe('меню поста', () => {
  test('гость видит меню читателя и копирует ссылку', async ({ page, context, baseURL }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write'])
    await openPost(page, annas)
    const items = await openMenu(page)
    await expect(items).toHaveText(['Скопировать ссылку'])
    await expectNoHorizontalScroll(page)
    await press(items.first())
    await expect(page.getByText('Ссылка скопирована')).toBeVisible()
    expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(new URL(annas, baseURL).href)
  })

  test('на чужом посте вошедший видит меню читателя', async ({ page }) => {
    await signIn(page)
    await openPost(page, boriss)
    await expect(await openMenu(page)).toHaveText(['Скопировать ссылку'])
  })

  test('автор удаляет свой пост после подтверждения', async ({ page }) => {
    await signIn(page)
    await page.goto('/write')
    await waitForHydration(page)
    await expect(page.getByTestId('block-editor')).toHaveAttribute('data-state', 'ready')
    await press(page.locator('.ce-paragraph').first())
    await page.keyboard.type('Пост, который удалят.')
    await press(page.getByTestId('editor-publish'))
    await page.waitForURL(/\/u\/anna\/[0-9a-f-]{36}$/)
    const address = new URL(page.url()).pathname
    await waitForHydration(page)

    const items = await openMenu(page)
    await expect(items).toHaveText(['Удалить'])
    await press(items.first())
    await expect(confirmDialog(page)).toBeVisible()
    await press(confirmDialog(page).getByRole('button', { name: 'Отмена' }))
    await expect(confirmDialog(page)).toBeHidden()
    await expect(page).toHaveURL(new RegExp(`${address}$`))

    await press((await openMenu(page)).first())
    await press(confirmDialog(page).getByRole('button', { name: 'Удалить' }))
    await page.waitForURL((url) => url.pathname === '/u/anna')
    await expect(page.getByText('Пост удалён')).toBeVisible()

    // The address no longer opens the post (asked aside, so the page logs no failed load).
    const gone = await page.request.get(address)
    expect(gone.status()).toBe(404)
    expect(await gone.text()).toContain('Пост не найден')
  })
})

test.describe('меню поста: сбой', () => {
  // The refusal of the site's server is the failure under test: the browser logs it as a failed load.
  test.use({ missingResources: ['/bff/post?id=0195f3a0-0000-7000-8000-0000000000c1'] })

  test('сбой удаления — тост, окно остаётся', async ({ page }) => {
    // The seeded post is anna's, so she gets the author's menu; the fake keeps it for the other tests and refuses.
    await signIn(page)
    await openPost(page, annas)
    await press((await openMenu(page)).first())
    await press(confirmDialog(page).getByRole('button', { name: 'Удалить' }))
    await expect(page.getByText('Не удалось удалить пост')).toBeVisible()
    await expect(confirmDialog(page)).toBeVisible()
    await expect(page).toHaveURL(new RegExp(`${annas}$`))
  })
})
