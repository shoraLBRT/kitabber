import type { Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// The search results in the section «Рейтинг» (E14-03, FR-SEARCH, ADR-0124) in both profiles, against the fake API
// (e2e/fake-api.mjs, which keeps the person's subscriptions per client address): Enter in the header's search opens
// them; the blocks «Блоги», «Темы» and «Посты» with «Подписаться» in the rows; the filters of «Посты» in the address;
// «Показать ещё»; the states «загрузка», «пусто», «ошибка». The black list, the morphology and the offsets are tested by
// the backend (SearchResultsApiTests, OpenSearchIndexTests).

async function asOwnClient(page: Page): Promise<void> {
  const octet = () => Math.floor(Math.random() * 254) + 1
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': `10.${octet()}.${octet()}.${octet()}` })
}

async function signIn(page: Page): Promise<void> {
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

async function open(page: Page, path: string) {
  await page.goto(path)
  await waitForHydration(page)
  return page.getByTestId('search-results')
}

test.use({ actionTimeout: 10_000 })

test.describe('результаты поиска', () => {
  test.beforeEach(async ({ page }) => asOwnClient(page))

  test('Enter в поиске шапки открывает блоки «Блоги», «Темы» и «Посты»', async ({ page }) => {
    await page.goto('/')
    await waitForHydration(page)
    await press(page.getByTestId('shell-header').getByRole('button', { name: 'Поиск' }))
    const input = page.getByTestId('search').getByRole('combobox')
    await input.fill('книг')
    await expect(page.getByTestId('search').getByTestId('search-post').first()).toBeVisible()
    await input.press('Enter')
    await page.waitForURL((url) => url.pathname === '/rating' && url.searchParams.get('q') === 'книг')

    const results = page.getByTestId('search-results')
    await expect(page.getByTestId('rating-search-field')).toHaveValue('книг')
    const blogs = results.getByTestId('search-block-blogs')
    await expect(blogs.getByRole('heading', { name: 'Блоги' })).toBeVisible()
    await expect(blogs.getByTestId('subscription-card-name')).toHaveText(['Анна'])
    const topics = results.getByTestId('search-block-topics')
    await expect(topics.getByTestId('subscription-card-name')).toHaveText(['Книги'])
    await expect(topics.getByTestId('subscription-card-button')).toHaveText('Подписаться')
    const posts = results.getByTestId('search-block-posts').getByTestId('search-result-post')
    await expect(posts).toHaveCount(3)
    await expect(posts.first()).toContainText('Книги для долгой зимы')
    await expect(posts.first()).toContainText('Анна')
    await expectNoHorizontalScroll(page)
  })

  test('«Подписаться» в строке темы подписывает, гостя просит войти', async ({ page }) => {
    let results = await open(page, '/rating?q=книг')
    await press(results.getByTestId('search-block-topics').getByTestId('subscription-card-button'))
    await expect(page.getByRole('dialog', { name: 'Вход' })).toBeVisible()

    await signIn(page)
    results = await open(page, '/rating?q=книг')
    const button = results.getByTestId('search-block-topics').getByTestId('subscription-card-button')
    await expect(button).toHaveText('Подписаться')
    await press(button)
    await expect(button).toHaveText('Вы подписаны')
    await expect(button).toHaveAttribute('aria-pressed', 'true')
    // Her own blog has no button.
    await expect(
      results.getByTestId('search-block-blogs').getByTestId('subscription-card-button'),
    ).toHaveCount(0)

    results = await open(page, '/rating?q=книг')
    await expect(
      results.getByTestId('search-block-topics').getByTestId('subscription-card-button'),
    ).toHaveText('Вы подписаны')
  })

  test('фильтры постов: только заголовки, период и порядок остаются в адресе', async ({ page }) => {
    const results = await open(page, '/rating?q=книг')
    const block = results.getByTestId('search-block-posts')
    const posts = block.getByTestId('search-result-post')
    await expect(posts).toHaveCount(3)
    await expect(block.getByTestId('search-filters')).toBeHidden()
    await press(block.getByTestId('search-filters-toggle'))
    const filters = block.getByTestId('search-filters')
    await expect(filters).toBeVisible()

    await press(filters.getByTestId('search-filter-titles'))
    await page.waitForURL((url) => url.searchParams.get('titles') === '1')
    await expect(posts).toHaveCount(2)
    await expect(block).not.toContainText('Дочитал книги')

    await press(filters.getByTestId('search-filter-titles'))
    await press(filters.getByRole('button', { name: 'За неделю' }))
    await page.waitForURL(
      (url) => url.searchParams.get('period') === 'week' && !url.searchParams.has('titles'),
    )
    await expect(posts).toHaveCount(2)
    await expect(block).not.toContainText('Старые книги на чердаке')

    await press(filters.getByRole('button', { name: 'За всё время' }))
    await press(filters.getByRole('button', { name: 'По дате' }))
    await page.waitForURL(
      (url) => url.searchParams.get('order') === 'date' && !url.searchParams.has('period'),
    )
    await expect(filters.getByRole('button', { name: 'По дате' })).toHaveAttribute('aria-pressed', 'true')
    await expect(posts.first()).toContainText('Дочитал книги')
    await expect(posts.last()).toContainText('Старые книги на чердаке')

    // The filters stay with the address: opened again, it shows the same.
    await open(page, '/rating?q=книг&period=week&titles=1')
    await expect(page.getByTestId('search-filters')).toBeVisible()
    await expect(page.getByTestId('search-filter-titles')).toBeChecked()
    await expect(page.getByTestId('search-result-post')).toHaveText([/Книги для долгой зимы/])
    await expectNoHorizontalScroll(page)
  })

  test('«Показать ещё» дочитывает посты, без блогов остаётся только блок постов', async ({ page }) => {
    const results = await open(page, '/rating?q=пазл')
    await expect(results.getByTestId('search-block-blogs')).toHaveCount(0)
    await expect(results.getByTestId('search-block-topics')).toHaveCount(0)
    const posts = results.getByTestId('search-result-post')
    await expect(posts).toHaveCount(10)
    await press(results.getByTestId('search-posts-more'))
    await expect(posts).toHaveCount(12)
    await expect(results.getByTestId('search-posts-more')).toHaveCount(0)
  })

  test('пустой результат показывает заглушку, а «Отмена» уходит из поиска', async ({ page }) => {
    const results = await open(page, '/rating?q=щщщнетничего')
    await expect(results.getByTestId('search-results-empty')).toHaveText(
      'По запросу «щщщнетничего» ничего не нашлось.',
    )
    await expect(results.getByTestId('search-block-posts')).toHaveCount(0)
    await expectNoHorizontalScroll(page)

    await press(page.getByTestId('rating-search-cancel'))
    await page.waitForURL((url) => url.pathname === '/rating' && !url.searchParams.has('q'))
    await expect(page.getByTestId('search-results')).toHaveCount(0)
    await expect(page.getByTestId('rating-search')).toContainText('Введите запрос')
  })

  test('новый запрос в поле страницы ищет снова', async ({ page }) => {
    await open(page, '/rating?q=щщщнетничего')
    const field = page.getByTestId('rating-search-field')
    await field.fill('пазл')
    await field.press('Enter')
    await page.waitForURL((url) => url.searchParams.get('q') === 'пазл')
    await expect(page.getByTestId('search-result-post')).toHaveCount(10)
  })

  test('пока ищем, видны заглушки; сбой показывает ошибку с «Повторить»', async ({ page }) => {
    let release: () => void = () => {}
    const held = new Promise<void>((resolve) => (release = resolve))
    await page.route('**/bff/search-*', async (route) => {
      await held
      await route.continue()
    })
    const results = await open(page, '/rating?q=книг')
    await expect(results.getByTestId('search-results-loading')).toBeVisible()
    release()
    await expect(results.getByTestId('search-result-post')).toHaveCount(3)
    await page.unroute('**/bff/search-*')

    const failed = await open(page, '/rating?q=сбой поиска')
    await expect(failed.getByText('Не удалось выполнить поиск.')).toBeVisible()
    await expect(failed.getByTestId('search-results-retry')).toBeVisible()
  })
})
