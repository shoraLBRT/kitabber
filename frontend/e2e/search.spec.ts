import { box, expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'
import type { Page } from '@playwright/test'

// The header's search (E14-02, ADR-0097) in both profiles, against the fake API (e2e/fake-api.mjs), which suggests its
// blogs and topics by name or nickname and one post by its title. On a desktop the field opens over a darkened page at
// the width of the feed; on a phone it is a screen of its own with «Отмена». That the reader's black list applies and
// that the last word is matched as the start of a word is tested by the backend (SearchSuggestionsApiTests,
// OpenSearchIndexTests); that one account never sees another's history, by tests/shell.

async function openSearch(page: Page) {
  await page.goto('/')
  await waitForHydration(page)
  await press(page.getByTestId('shell-header').getByRole('button', { name: 'Поиск' }))
  const search = page.getByTestId('search')
  await expect(search.getByRole('combobox', { name: 'Поиск по блогам, темам и постам' })).toBeFocused()
  return search
}

test.describe('поиск в шапке', () => {
  test('подсказки после паузы ввода, стрелки выбирают строку, Enter её открывает', async ({ page }) => {
    const search = await openSearch(page)
    const panel = search.getByRole('dialog')
    if (isTouch()) {
      // A screen of its own: the whole window, with «Отмена».
      const rect = await box(panel)
      expect(rect.width).toBe(390)
      await expect(search.getByRole('button', { name: 'Отмена' })).toBeVisible()
    } else {
      // The width of the feed over a darkened page.
      await expect(search.getByTestId('search-backdrop')).toBeVisible()
      const rect = await box(panel)
      expect(rect.width).toBeLessThanOrEqual(680)
      await expect(search.getByRole('button', { name: 'Отмена' })).toBeHidden()
    }
    await expect(search.getByTestId('search-history-empty')).toBeVisible()

    const input = search.getByRole('combobox')
    await input.fill('кни')
    await expect(search.getByTestId('search-blog')).toHaveText([/Книги.*Тема/])
    await expect(search.getByTestId('search-post')).toContainText('Книги для долгой зимы')
    await expectNoHorizontalScroll(page)

    await input.press('ArrowDown')
    await expect(search.getByRole('option').first()).toHaveAttribute('aria-selected', 'true')
    await input.press('Enter')
    await page.waitForURL((url) => url.pathname === '/books')
    await expect(page.getByTestId('search')).toHaveCount(0)
  })

  test('история запросов остаётся в браузере и очищается', async ({ page }) => {
    let search = await openSearch(page)
    const input = search.getByRole('combobox')
    await input.fill('борис')
    await expect(search.getByTestId('search-blog')).toContainText('Борис')
    await press(search.getByTestId('search-blog').getByRole('link'))
    await page.waitForURL((url) => url.pathname === '/u/boris')

    await page.reload()
    search = await openSearch(page)
    const rows = search.getByTestId('search-history-row')
    await expect(rows).toHaveText(['борис'])
    // A query of the history fills the field and is searched again.
    await press(rows.first().getByRole('button').first())
    await expect(search.getByRole('combobox')).toHaveValue('борис')
    await expect(search.getByTestId('search-blog')).toContainText('Борис')

    await search.getByRole('combobox').fill('')
    await press(search.getByTestId('search-history-clear'))
    await expect(search.getByTestId('search-history-empty')).toBeVisible()
    await page.reload()
    search = await openSearch(page)
    await expect(search.getByTestId('search-history-empty')).toBeVisible()
  })

  test('ничего не нашлось; Esc или «Отмена» закрывают поиск', async ({ page }) => {
    const search = await openSearch(page)
    await search.getByRole('combobox').fill('щщщщ')
    await expect(search.getByTestId('search-empty')).toHaveText('Ничего не нашлось по запросу «щщщщ».')

    if (isTouch()) await press(search.getByRole('button', { name: 'Отмена' }))
    else await search.getByRole('combobox').press('Escape')
    await expect(page.getByTestId('search')).toHaveCount(0)
    await expect(page.getByTestId('shell-header').getByRole('button', { name: 'Поиск' })).toBeFocused()
  })
})

test.describe('поиск в шапке: сбой подсказок', () => {
  // The failed answer is the point of the test: the browser logs its 502.
  test.use({ missingResources: ['/bff/search-suggestions?q=books'] })

  test('пока подсказки грузятся — скелетон, при сбое — «Повторить»', async ({ page }) => {
    let fail = true
    let release: () => void = () => {}
    const held = new Promise<void>((resolve) => (release = resolve))
    await page.route('**/bff/search-suggestions?**', async (route) => {
      if (fail) {
        fail = false
        await route.fulfill({ status: 502, body: 'null', contentType: 'application/json' })
        return
      }
      await held
      await route.continue()
    })
    const search = await openSearch(page)
    await search.getByRole('combobox').fill('books')
    await expect(search.getByRole('alert')).toHaveText('Не удалось получить подсказки.')

    await press(search.getByRole('button', { name: 'Повторить' }))
    await expect(search.getByTestId('search-loading')).toBeVisible()
    release()
    await expect(search.getByTestId('search-blog')).toHaveCount(1)
  })
})
