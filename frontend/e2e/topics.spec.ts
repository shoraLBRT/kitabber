import type { Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// The menu's topics and the catalogue (E11-02, FR-TOPIC, FR-SUB, ADR-0088) in both profiles against the fake API
// (e2e/fake-api.mjs): a guest's menu shows the popular topics and «Показать все» leads to /topics; the catalogue lists
// the most followed first, is searched (the search stays in the address), and shows its loading, empty and error
// states; a person's menu shows the topics they follow. The real order and search are the backend's
// (TopicCatalogApiTests).

async function asOwnClient(page: Page): Promise<void> {
  const octet = () => Math.floor(Math.random() * 254) + 1
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': `10.${octet()}.${octet()}.${octet()}` })
}

/** The left menu on a desktop, the «Меню» screen on a phone. */
async function openMenu(page: Page) {
  await page.goto(isTouch() ? '/menu' : '/')
  await waitForHydration(page)
  return isTouch() ? page.getByTestId('menu-screen') : page.getByTestId('shell-nav')
}

const topicLinks = (menu: ReturnType<Page['getByTestId']>) =>
  menu.getByRole('region', { name: 'Темы' }).getByRole('listitem').getByRole('link')

async function openCatalog(page: Page, path = '/topics') {
  await page.goto(path)
  await waitForHydration(page)
  return page.getByTestId('topic-catalog')
}

test.describe('темы в меню и каталог', () => {
  test.beforeEach(async ({ page }) => asOwnClient(page))

  test('гость видит популярные темы в меню, а «Показать все» ведёт в каталог', async ({ page }) => {
    const menu = await openMenu(page)
    await expect(topicLinks(menu).first()).toHaveText('Книги')
    await expect(topicLinks(menu).first()).toHaveAttribute('href', '/books')
    await press(menu.getByRole('link', { name: 'Показать все' }))
    await page.waitForURL((url) => url.pathname === '/topics')
    await expect(page.getByRole('heading', { level: 1, name: 'Темы' })).toBeVisible()
    await expectNoHorizontalScroll(page)
  })

  test('каталог показывает самые популярные темы первыми и ищет по названию', async ({ page, request }) => {
    const html = await (await request.get('/topics')).text()
    expect(html).toContain('41 подписчик')

    const catalog = await openCatalog(page)
    const cards = catalog.getByTestId('topic-card')
    await expect(cards.first()).toContainText('Книги')
    await expect(cards.first()).toContainText('Что читать и как читать.')
    await expect(cards.first().getByTestId('topic-subscribers')).toHaveText('41 подписчик')

    await catalog.getByRole('searchbox', { name: 'Поиск по темам' }).fill('НАУ')
    await expect(cards).toHaveCount(1)
    await expect(cards.first()).toContainText('Наука')
    await page.waitForURL((url) => url.searchParams.get('q') === 'НАУ')
    await expectNoHorizontalScroll(page)

    await press(cards.first())
    await page.waitForURL((url) => url.pathname === '/science')
  })

  test('поиск из адреса, пустой результат и ошибка', async ({ page }) => {
    const catalog = await openCatalog(page, '/topics?q=science')
    await expect(catalog.getByRole('searchbox', { name: 'Поиск по темам' })).toHaveValue('science')
    await expect(catalog.getByTestId('topic-card')).toHaveCount(1)

    await catalog.getByRole('searchbox', { name: 'Поиск по темам' }).fill('кулинария')
    await expect(catalog.getByTestId('state')).toHaveText('По запросу «кулинария» тем не нашлось.')

    // The server render meets the failure, so the browser logs no failed request.
    const failed = await openCatalog(page, `/topics?q=${encodeURIComponent('сбой поиска')}`)
    await expect(failed.getByRole('alert')).toHaveText('Не удалось загрузить темы.')
    await expect(failed.getByRole('button', { name: 'Повторить' })).toBeVisible()
  })

  test('пока темы загружаются, видна заглушка', async ({ page }) => {
    const catalog = await openCatalog(page)
    let release = () => {}
    const held = new Promise<void>((resolve) => (release = resolve))
    await page.route('**/api/v1/topics/catalog*', async (route) => {
      await held
      await route.continue()
    })
    await catalog.getByRole('searchbox', { name: 'Поиск по темам' }).fill('книги')
    await expect(catalog.getByTestId('topic-catalog-loading')).toBeVisible()
    await expect(catalog.getByRole('status')).toHaveText('Загрузка тем')
    release()
    await expect(catalog.getByTestId('topic-card')).toHaveCount(1)
    await expect(catalog.getByTestId('topic-catalog-loading')).toHaveCount(0)
  })

  test('в меню человека — темы, на которые он подписан', async ({ page }) => {
    await page.goto(isTouch() ? '/menu' : '/')
    const login = isTouch()
      ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
      : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
    await press(login)
    await page.waitForURL((url) => url.pathname === '/')

    await page.goto('/science')
    await waitForHydration(page)
    const answered = page.waitForResponse(
      (response) => new URL(response.url()).pathname === '/bff/subscription',
    )
    await press(page.getByTestId('subscription').getByRole('button', { name: 'Подписаться' }))
    await answered

    const menu = await openMenu(page)
    await expect(topicLinks(menu)).toHaveText(['Наука'])
  })
})
