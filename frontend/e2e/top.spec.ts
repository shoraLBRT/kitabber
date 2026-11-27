import { expect, expectNoHorizontalScroll, press, test, waitForHydration } from './fixtures'

// The tops for a day, a week, a month, a year and all time in «Популярном» (E08-06, FR-FEED, ADR-0104) at
// /top/<period> in both profiles against the fake API (e2e/fake-api.mjs), which counts the periods back from a
// fixed moment: its posts are within the week but not within the day, «Книги осени» has the most likes. Which posts a
// period holds, the order by rating and the cache are the backend's and are tested there (TopFeedApiTests).

test.use({ actionTimeout: 10_000 })

test.describe('топ за период', () => {
  test('каждый период открывается по своему адресу и рендерится на сервере', async ({ request }) => {
    for (const period of ['day', 'week', 'month', 'year', 'all']) {
      const response = await request.get(`/top/${period}`)
      expect(response.status()).toBe(200)
      expect(await response.text()).toContain('data-testid="top-periods"')
    }

    const week = await (await request.get('/top/week')).text()
    expect(week).toContain('data-testid="feed-list"')
    expect(week).toContain('Книги осени')
  })

  test('незнакомый период — страница 404', async ({ request }) => {
    const response = await request.get('/top/century')
    expect(response.status()).toBe(404)
  })

  test('переключатель ведёт между периодами, текущий отмечен', async ({ page }) => {
    await page.goto('/top/week')
    await waitForHydration(page)
    await expect(page.getByRole('heading', { level: 1, name: 'Популярное' })).toBeVisible()
    const periods = page.getByRole('navigation', { name: 'Период' })
    await expect(periods.getByRole('link', { name: 'За неделю' })).toHaveAttribute('aria-current', 'page')
    // The most liked post comes first.
    await expect(page.getByTestId('feed-item').first()).toContainText('Книги осени')
    await expectNoHorizontalScroll(page)

    await press(periods.getByRole('link', { name: 'За всё время' }))
    await page.waitForURL((url) => url.pathname === '/top/all')
    await expect(periods.getByRole('link', { name: 'За всё время' })).toHaveAttribute('aria-current', 'page')
    await expect(periods.getByRole('link', { name: 'За неделю' })).not.toHaveAttribute('aria-current', 'page')
    await expect(page.getByTestId('feed-item').first()).toContainText('Книги осени')
    await expect(page).toHaveTitle(/Популярное · за всё время/)

    await page.goBack()
    await page.waitForURL((url) => url.pathname === '/top/week')
    await expect(periods.getByRole('link', { name: 'За неделю' })).toHaveAttribute('aria-current', 'page')
  })

  test('пустой период показывает заглушку со ссылкой на «Свежее»', async ({ page }) => {
    await page.goto('/top/day')
    await waitForHydration(page)
    await expect(page.getByTestId('feed-empty')).toContainText('За этот период постов пока нет')
    await expect(page.getByTestId('feed-item')).toHaveCount(0)
    await expectNoHorizontalScroll(page)

    await press(page.getByTestId('top-fresh'))
    await page.waitForURL((url) => url.pathname === '/new')
  })

  test.describe('сбой', () => {
    // The refused read of the month logs its 500 to the console; that one is expected.
    test.use({ missingResources: ['/api/v1/feeds/top/month'] })

    test('пока период читается, видна заглушка; сбой предлагает «Повторить»', async ({ page }) => {
      await page.goto('/top/week')
      await waitForHydration(page)
      let fail = true
      let release: () => void = () => {}
      const held = new Promise<void>((resolve) => (release = resolve))
      await page.route('**/api/v1/feeds/top/month**', async (route) => {
        await held
        if (fail) await route.fulfill({ status: 500, json: { status: 500, code: 'internal' } })
        else await route.continue()
      })

      await press(page.getByRole('navigation', { name: 'Период' }).getByRole('link', { name: 'За месяц' }))
      await page.waitForURL((url) => url.pathname === '/top/month')
      await expect(page.getByTestId('feed-loading')).toBeVisible()
      release()
      await expect(page.getByTestId('feed-error')).toBeVisible()

      fail = false
      await press(page.getByTestId('feed-error').getByRole('button', { name: 'Повторить' }))
      await expect(page.getByTestId('feed-item').first()).toContainText('Книги осени')
    })
  })
})
