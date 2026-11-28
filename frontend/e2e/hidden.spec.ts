import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'
import type { Page } from '@playwright/test'

// Hiding from the feeds (E11-07) in both profiles, against the fake API (e2e/fake-api.mjs), which keeps the hidden
// posts and topics per client address, empty at first. «Скрыть» in the «…» menu of someone else's post and «Скрыть из
// лент» on a topic's page hide; «Приватность и блокировки» lists them and brings them back. That a hidden post or topic
// leaves the feeds, and that a hidden topic stays on its own page, is tested by the backend (HiddenApiTests,
// SharedFeedExclusionsApiTests).
const borisPost = '/books/0195f3a0-0000-7000-8000-0000000000c3-knigi-oseni'

function newClient(): string {
  const octet = () => Math.floor(Math.random() * 254) + 1
  return `10.${octet()}.${octet()}.${octet()}`
}

async function signIn(page: Page): Promise<void> {
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': newClient() })
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

async function openPrivacySettings(page: Page): Promise<void> {
  await page.goto('/settings/privacy')
  await waitForHydration(page)
}

test.describe('скрытие из лент', () => {
  test.use({ actionTimeout: 10_000 })

  test('«Скрыть» в меню чужого поста, затем «Вернуть в ленты» в настройках', async ({ page }) => {
    await signIn(page)
    await page.goto(borisPost)
    await waitForHydration(page)
    await press(page.getByTestId('post-header').getByRole('button', { name: 'Меню поста' }))
    const items = isTouch()
      ? page.getByRole('dialog', { name: 'Меню поста' }).getByTestId('post-menu-sheet').getByRole('button')
      : page.getByRole('menu', { name: 'Меню поста' }).getByRole('menuitem')
    await press(items.filter({ hasText: 'Скрыть' }))
    await expect(page.getByText('Пост скрыт из ваших лент. Вернуть его можно в настройках.')).toBeVisible()

    await openPrivacySettings(page)
    const section = page.getByTestId('hidden-list')
    const rows = section.getByTestId('hidden-post')
    await expect(rows).toHaveCount(1)
    await expect(rows.first()).toContainText('Книги осени')
    await expect(section.getByTestId('hidden-topics-empty')).toBeVisible()
    await expectNoHorizontalScroll(page)

    await press(section.getByRole('button', { name: 'Вернуть в ленты: Книги осени' }))
    await expect(section.getByRole('status')).toHaveText('«Книги осени» снова в ваших лентах.')
    await expect(section.getByTestId('hidden-posts-empty')).toBeVisible()

    // The list is the account's: it stays empty after a reload.
    await page.reload()
    await waitForHydration(page)
    await expect(page.getByTestId('hidden-posts-empty')).toBeVisible()
  })

  test('«Скрыть из лент» на странице темы держится после перезагрузки и видно в настройках', async ({
    page,
  }) => {
    await signIn(page)
    await page.goto('/books')
    await waitForHydration(page)
    const button = page.getByTestId('topic-hide').getByRole('button')
    await expect(button).toHaveText('Скрыть из лент')
    await press(button)
    await expect(button).toHaveText('Показать в лентах')
    await expect(page.getByTestId('topic-hidden-note')).toBeVisible()
    await expectNoHorizontalScroll(page)

    await page.reload()
    await waitForHydration(page)
    await expect(page.getByTestId('topic-hide').getByRole('button')).toHaveText('Показать в лентах')

    await openPrivacySettings(page)
    const rows = page.getByTestId('hidden-list').getByTestId('hidden-topic')
    await expect(rows).toHaveCount(1)
    await expect(rows.first()).toContainText('Книги')
    await expect(page.getByTestId('hidden-list').getByTestId('hidden-posts-empty')).toBeVisible()
  })

  test('гость не видит «Скрыть» ни на теме, ни в меню поста', async ({ page }) => {
    await page.goto('/books')
    await waitForHydration(page)
    await expect(page.getByTestId('topic')).toBeVisible()
    await expect(page.getByTestId('topic-hide')).toHaveCount(0)

    await page.goto(borisPost)
    await waitForHydration(page)
    await press(page.getByTestId('post-header').getByRole('button', { name: 'Меню поста' }))
    const items = isTouch()
      ? page.getByRole('dialog', { name: 'Меню поста' }).getByTestId('post-menu-sheet').getByRole('button')
      : page.getByRole('menu', { name: 'Меню поста' }).getByRole('menuitem')
    await expect(items).toHaveText(['Скопировать ссылку'])
  })
})

test.describe('скрытое: сбой чтения', () => {
  // The failed read is the point of the test: the browser logs its 502.
  test.use({ missingResources: ['/bff/hidden'] })

  test('пока список читается — скелетон, при сбое — «Повторить»', async ({ page }) => {
    let fail = true
    let release: () => void = () => {}
    const held = new Promise<void>((resolve) => (release = resolve))
    await page.route('**/bff/hidden', async (route) => {
      if (fail) {
        fail = false
        await route.fulfill({ status: 502, body: 'null', contentType: 'application/json' })
        return
      }
      await held
      await route.continue()
    })
    await signIn(page)
    await openPrivacySettings(page)
    const section = page.getByTestId('hidden-list')
    await expect(section.getByRole('alert')).toHaveText('Не удалось загрузить скрытое.')

    await press(section.getByRole('button', { name: 'Повторить' }))
    await expect(section.getByTestId('hidden-list-loading')).toBeVisible()
    release()
    await expect(section.getByTestId('hidden-topics-empty')).toBeVisible()
  })
})
