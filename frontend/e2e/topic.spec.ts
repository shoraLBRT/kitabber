import { expect, expectNoHorizontalScroll, press, test, waitForHydration } from './fixtures'

// The topic page (E11-01, FR-TOPIC, docs/ui/topic.md, ADR-0082) in both profiles against the fake API
// (e2e/fake-api.mjs): the server renders the header and the topic's posts for a guest, the owner is named with a link to
// their blog, an empty topic offers «Написать в тему» with the topic chosen in the editor, a failing feed offers
// «Повторить», and an address with no topic is the 404 page. That a published post lands in its topic's feed, and only
// there, is tested by the backend (BlogFeedApiTests, PublishApiTests).

const missing = '/nosuchtopic'

test.describe('страница темы', () => {
  test.use({ missingResources: [missing] })

  test('сервер отдаёт гостю шапку темы и её посты', async ({ page, request }) => {
    const answer = await request.get('/books')
    expect(answer.status()).toBe(200)
    const html = await answer.text()
    expect(html).toContain('<h1 class="topic__title"')
    expect(html).toContain('Что читать и как читать.')
    expect(html).toContain('Книги осени')
    expect(html).not.toContain('Привет, мир')

    await page.goto('/books')
    await waitForHydration(page)
    const topic = page.getByTestId('topic')
    await expect(topic.getByRole('heading', { level: 1, name: 'Книги' })).toBeVisible()
    await expect(topic.getByTestId('topic-owner')).toContainText('Владелец темы: Борис')
    const items = topic.getByTestId('feed-item')
    await expect(items).toHaveCount(1)
    await expect(items.first()).toContainText('Книги осени')
    await expect(page).toHaveTitle(/Книги/)
    await expectNoHorizontalScroll(page)

    await press(topic.getByTestId('topic-owner').getByRole('link', { name: 'Борис' }))
    await page.waitForURL((url) => url.pathname === '/u/boris')
  })

  test('другой регистр адреса и адрес блога ведут на адрес темы', async ({ request }) => {
    const upper = await request.get('/BOOKS', { maxRedirects: 0 })
    expect(upper.status()).toBe(301)
    expect(upper.headers().location).toBe('/books')

    const asBlog = await request.get('/u/books', { maxRedirects: 0 })
    expect(asBlog.status()).toBe(301)
    expect(asBlog.headers().location).toBe('/books')

    const personal = await request.get('/anna', { maxRedirects: 0 })
    expect(personal.status()).toBe(301)
    expect(personal.headers().location).toBe('/u/anna')
  })

  test('пустая тема предлагает написать в неё', async ({ page }) => {
    await page.goto('/science')
    await waitForHydration(page)
    const empty = page.getByTestId('feed-empty')
    await expect(empty).toContainText('В этой теме пока нет постов')
    await expectNoHorizontalScroll(page)

    await press(empty.getByRole('link', { name: 'Написать в тему' }))
    await page.waitForURL((url) => url.pathname === '/write')
    await expect(page.getByTestId('editor-topic')).toContainText('Наука')
  })

  test('сбой ленты темы — ошибка с «Повторить», шапка на месте', async ({ page }) => {
    await page.goto('/outage')
    await waitForHydration(page)
    await expect(page.getByRole('heading', { level: 1, name: 'Сбой' })).toBeVisible()
    const error = page.getByTestId('feed-error')
    await expect(error).toContainText('Не удалось загрузить ленту')
    await expect(error.getByRole('button', { name: 'Повторить' })).toBeVisible()
  })

  test('темы нет — страница 404', async ({ page, request }) => {
    expect((await request.get(missing)).status()).toBe(404)

    await page.goto(missing)
    await waitForHydration(page)
    await expect(page.getByTestId('not-found-page')).toBeVisible()
  })
})
