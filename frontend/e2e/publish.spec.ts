import type { Page } from '@playwright/test'
import { expect, isTouch, press, test, waitForHydration } from './fixtures'

// Publishing (E05-09, FR-EDITOR, ADR-0046) in both profiles against the fake API (e2e/fake-api.mjs): an empty post
// cannot be published, the title is optional, a topic or «Без темы» is chosen, and the published post opens on its
// page. The server's own checks are tested by the backend (PublishApiTests); the editor's states by PostEditor.spec.

async function signIn(page: Page): Promise<void> {
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

async function openEditor(page: Page): Promise<void> {
  await page.goto('/write')
  await waitForHydration(page)
  await expect(page.getByTestId('block-editor')).toHaveAttribute('data-state', 'ready')
}

async function typeText(page: Page, text: string): Promise<void> {
  await press(page.locator('.ce-paragraph').first())
  await page.keyboard.type(text)
}

const publishButton = (page: Page) => page.getByTestId('editor-publish')

test.describe('публикация', () => {
  test.beforeEach(async ({ page }) => {
    await signIn(page)
    await openEditor(page)
  })

  test('пустой пост опубликовать нельзя', async ({ page }) => {
    await expect(publishButton(page)).toBeDisabled()
    await page.getByTestId('editor-title').fill('Только заголовок')
    await expect(publishButton(page)).toBeDisabled()

    await typeText(page, '   ')
    await expect(publishButton(page)).toBeDisabled()
    await expect(page).toHaveURL(/\/write$/)
  })

  test('короткий пост без заголовка и без темы публикуется и открывается', async ({ page }) => {
    await expect(page.getByTestId('editor-topic')).toContainText('Без темы')
    await typeText(page, 'Короткий пост из редактора.')
    await expect(publishButton(page)).toBeEnabled()
    await press(publishButton(page))

    await page.waitForURL(/\/u\/anna\/[0-9a-f-]{36}$/)
    const post = page.getByTestId('post')
    await expect(post).toContainText('Короткий пост из редактора.')
    await expect(post.getByRole('heading', { level: 1 })).toHaveCount(0)
    await expect(post.getByTestId('post-author')).toHaveAttribute('href', '/u/anna')
  })

  test('пост с заголовком в выбранной теме открывается на странице темы', async ({ page }) => {
    await page.getByTestId('editor-title').fill('Осенние книги')
    await typeText(page, 'Что я прочитала.')
    await press(page.getByTestId('editor-topic').getByRole('button'))
    await press(page.getByRole('menuitem', { name: 'Книги' }))
    await expect(page.getByTestId('editor-topic')).toContainText('Книги')
    await press(publishButton(page))

    await page.waitForURL(/\/books\/[0-9a-f-]{36}$/)
    const post = page.getByTestId('post')
    await expect(post.getByRole('heading', { level: 1, name: 'Осенние книги' })).toBeVisible()
    await expect(post).toContainText('Что я прочитала.')
  })

  test('сбой публикации — тост, окно и текст остаются', async ({ page }) => {
    await typeText(page, 'сбой')
    await press(publishButton(page))
    await expect(page.getByTestId('toasts-assertive')).toContainText('Не удалось опубликовать пост')
    await expect(page).toHaveURL(/\/write$/)
    await expect(page.locator('.ce-paragraph').first()).toHaveText('сбой')
  })
})
