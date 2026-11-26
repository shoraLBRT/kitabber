import type { Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// «Моя лента» (E08-04, FR-FEED, ADR-0089) at /my in both profiles against the fake API (e2e/fake-api.mjs, which keeps
// the person's subscriptions per client address): a guest is asked to sign in; a person with no subscriptions is
// offered topics and the fresh feed; after subscribing to a topic its posts are in the feed; loading and a failure
// with «Повторить». Which posts the feed holds — subscribed blogs, followed authors, hidden and blocked ones left out —
// is the backend's and is tested there (MyFeedApiTests).

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

/**
 * Goes to «Моя лента» inside the app, as a link of the left menu does, so the browser reads the feed itself (through
 * the site's server). The phone has no link to it yet: the feed tabs under the header are a separate issue.
 */
async function openInApp(page: Page): Promise<void> {
  await page.goto('/new')
  await waitForHydration(page)
  await page.evaluate(() => {
    type Mounted = { __vue_app__: { $nuxt: { $router: { push: (to: string) => Promise<unknown> } } } }
    return (document.querySelector('#__nuxt') as unknown as Mounted).__vue_app__.$nuxt.$router.push('/my')
  })
  await page.waitForURL((url) => url.pathname === '/my')
}

test.use({ actionTimeout: 10_000 })

test.describe('«Моя лента»', () => {
  test.beforeEach(async ({ page }) => asOwnClient(page))

  test('гостя просят войти, кнопка открывает окно входа', async ({ page }) => {
    await page.goto('/my')
    await waitForHydration(page)
    const guest = page.getByTestId('my-feed-guest')
    await expect(guest.getByRole('heading', { level: 1, name: 'Моя лента' })).toBeVisible()
    await expect(guest).toContainText('Войдите, и здесь появятся посты')
    await press(guest.getByRole('button', { name: 'Войти' }))
    await expect(page.getByRole('dialog', { name: 'Вход' })).toBeVisible()
    await expectNoHorizontalScroll(page)
    await expect(page).toHaveTitle(/Моя лента/)
  })

  test('без подписок лента пуста и предлагает темы и «Свежее»', async ({ page }) => {
    await signIn(page)
    await page.goto('/my')
    await waitForHydration(page)
    const empty = page.getByTestId('feed-empty')
    await expect(empty).toContainText('Здесь появятся посты блогов и тем, на которые вы подпишетесь.')
    await expect(empty.getByTestId('my-feed-topic').filter({ hasText: 'Книги' })).toHaveAttribute(
      'href',
      '/books',
    )
    await expect(empty.getByTestId('my-feed-fresh')).toHaveAttribute('href', '/new')
    await expectNoHorizontalScroll(page)

    await press(empty.getByTestId('my-feed-topic').filter({ hasText: 'Книги' }))
    await page.waitForURL((url) => url.pathname === '/books')
  })

  test('после подписки на тему её посты появляются в ленте', async ({ page }) => {
    await signIn(page)
    await page.goto('/books')
    await waitForHydration(page)
    const answered = page.waitForResponse(
      (response) => new URL(response.url()).pathname === '/bff/subscription',
    )
    await press(page.getByTestId('subscription').getByRole('button', { name: 'Подписаться' }))
    await answered

    await page.goto('/my')
    await waitForHydration(page)
    const items = page.getByTestId('feed-item')
    await expect(items).toHaveCount(1)
    await expect(items.first()).toContainText('Книги осени')
    await expect(page.getByText('Короткий пост без заголовка.')).toHaveCount(0)
    await expectNoHorizontalScroll(page)
  })

  test('пока лента читается, видна заглушка; сбой предлагает «Повторить»', async ({ page }) => {
    await signIn(page)
    let fail = true
    let release: () => void = () => {}
    const held = new Promise<void>((resolve) => (release = resolve))
    await page.route('**/bff/my-feed**', async (route) => {
      await held
      // The site's server answers a failed API so, and the page logs no error.
      if (fail) await route.fulfill({ json: { status: 'error' } })
      else await route.continue()
    })

    await openInApp(page)
    await expect(page.getByTestId('feed-loading')).toBeVisible()
    release()
    const error = page.getByTestId('feed-error')
    await expect(error).toContainText('Не удалось загрузить ленту.')

    fail = false
    await press(error.getByRole('button', { name: 'Повторить' }))
    await expect(page.getByTestId('feed-empty')).toBeVisible()
  })
})
