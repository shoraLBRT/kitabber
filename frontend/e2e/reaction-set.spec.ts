import { expect, expectNoHorizontalScroll, test } from './fixtures'

// The set of reactions (E02-12, ADR-0095) on its showcase /reactions against the fake API: every reaction of the
// directory drawn from its bundled picture (not the system's emoji font) in both themes, with the credit of the
// pictures' licence; and the loading, empty and error states of the set, read in the browser.

const titles = ['Нравится', 'Тепло', 'Улыбка', 'Браво', 'Полезно', 'Удивительно', 'Грустно']
const setPath = '**/api/v1/reactions/set'

// The error state answers 500 on purpose; the browser logs it as a failed load.
test.use({ missingResources: ['/api/v1/reactions/set'] })

for (const theme of ['light', 'dark'] as const) {
  test(`набор реакций рисуется своими картинками, тема ${theme}`, async ({ page }) => {
    await page.emulateMedia({ colorScheme: theme })
    await page.goto('/reactions')
    await page.evaluate((value) => (document.documentElement.dataset.theme = value), theme)

    await expect(page.getByRole('heading', { level: 1, name: 'Набор реакций' })).toBeVisible()
    const items = page.getByTestId('reaction-set-item')
    await expect(items).toHaveText(titles)
    const icons = page.getByTestId('reaction-icon')
    await expect(icons).toHaveCount(titles.length)
    // Every picture is a loaded SVG of the site at one size: the same on every system.
    const drawn = await icons.evaluateAll((images) =>
      (images as HTMLImageElement[]).map((img) => ({
        loaded: img.complete && img.naturalWidth > 0,
        svg:
          /\.svg($|\?)/.test(new URL(img.currentSrc || img.src).pathname) ||
          img.src.startsWith('data:image/svg'),
        sameOrigin: img.src.startsWith('data:') || new URL(img.src).origin === location.origin,
        width: Math.round(img.getBoundingClientRect().width),
      })),
    )
    for (const icon of drawn) expect(icon).toEqual({ loaded: true, svg: true, sameOrigin: true, width: 40 })
    await expect(page.getByTestId('reaction-credit')).toContainText('Fluent Emoji (Microsoft), лицензия MIT')
    await expectNoHorizontalScroll(page)
  })
}

test('пока набор загружается, видны заглушки', async ({ page }) => {
  let release = () => {}
  const answered = new Promise<void>((resolve) => (release = resolve))
  await page.route(setPath, async (route) => {
    await answered
    await route.continue()
  })
  await page.goto('/reactions')

  await expect(page.getByTestId('reaction-set-loading')).toBeVisible()
  await expect(page.getByTestId('reaction-set-loading').getByRole('status')).toHaveText(
    'Загружаем набор реакций…',
  )
  release()
  await expect(page.getByTestId('reaction-set-item')).toHaveCount(titles.length)
})

test('пустой справочник — сообщение вместо набора', async ({ page }) => {
  await page.route(setPath, (route) => route.fulfill({ json: [] }))
  await page.goto('/reactions')

  await expect(page.getByTestId('state')).toHaveText('Реакций пока нет.')
  await expect(page.getByTestId('reaction-set-item')).toHaveCount(0)
})

test('набор не загрузился — ошибка и повтор', async ({ page }) => {
  await page.route(setPath, (route) =>
    route.fulfill({ status: 500, json: { status: 500, code: 'internal' } }),
  )
  await page.goto('/reactions')

  await expect(page.getByTestId('state').getByRole('alert')).toContainText(
    'Не удалось загрузить набор реакций.',
  )
  await page.unroute(setPath)
  await page.getByRole('button', { name: 'Повторить' }).click()
  await expect(page.getByTestId('reaction-set-item')).toHaveText(titles)
})
