import type { Page, Route } from '@playwright/test'
import { box, expect, test, waitForHydration } from './fixtures'

// Acceptance of E06-04 (#47, ADR-0040) on the pictures of the /ui showcase, in a real browser and both profiles: a
// picture off the screen is not fetched until it comes near, its box is reserved before it arrives so nothing moves,
// the browser takes the width for the screen's density, and an empty or broken picture keeps its box.
test.use({ missingResources: ['/missing-avatar.png', '/missing-image.svg'] })

const pictures = '**/showcase/landscape-*.svg'

/** The showcase with its picture section pushed far below the first screen, beyond the browser's lazy-load margin. */
async function openWithPicturesFarBelow(page: Page) {
  await page.route('**/ui', async (route) => {
    const response = await route.fetch()
    const html = (await response.text()).replace(
      '</head>',
      '<style>[data-testid="showcase-UiImage"] { margin-top: 8000px; }</style></head>',
    )
    await route.fulfill({ response, body: html })
  })
  await page.goto('/ui')
  await waitForHydration(page)
}

test('картинка вне экрана не загружается, пока до неё не дошла прокрутка', async ({ page }) => {
  const fetched: string[] = []
  page.on('request', (request) => {
    if (request.url().includes('/showcase/landscape-')) fetched.push(request.url())
  })
  await openWithPicturesFarBelow(page)
  const image = page.getByTestId('showcase-image')

  await page.waitForLoadState('networkidle')
  expect(fetched).toEqual([])
  await expect(image).toHaveAttribute('data-state', 'loading')

  await image.scrollIntoViewIfNeeded()
  await expect(image).toHaveAttribute('data-state', 'loaded')
  expect(fetched).toHaveLength(1)
})

test('место под картинку занято до её загрузки, и макет не сдвигается', async ({ page }) => {
  // Hold the picture back until the box has been measured.
  let release: () => void = () => {}
  const held = new Promise<void>((resolve) => (release = resolve))
  await page.route(pictures, async (route: Route) => {
    await held
    await route.continue()
  })
  await openWithPicturesFarBelow(page)
  const image = page.getByTestId('showcase-image')
  const caption = page.getByTestId('showcase-UiImage').getByText('Загружено', { exact: true })
  await image.scrollIntoViewIfNeeded()
  await expect(image).toHaveAttribute('data-state', 'loading')

  const before = await box(image)
  const captionBefore = await box(caption)
  // 320 by 200, or the same proportions in a narrower column.
  expect(before.width).toBeGreaterThan(0)
  expect(before.height).toBeCloseTo((before.width * 200) / 320, 0)

  release()
  await expect(image).toHaveAttribute('data-state', 'loaded')
  const after = await box(image)
  expect(after).toEqual(before)
  expect(await box(caption)).toEqual(captionBefore)
})

test('браузер берёт ширину картинки под плотность экрана', async ({ page }) => {
  await page.goto('/ui')
  const image = page.getByTestId('showcase-image')
  await image.scrollIntoViewIfNeeded()
  await expect(image).toHaveAttribute('data-state', 'loaded')

  // A 320 px box: the 320 px file at one device pixel per CSS pixel (desktop), the 1280 px one at three (the phone
  // profile needs 960, the next width up).
  const density = await page.evaluate(() => window.devicePixelRatio)
  const expected = density === 1 ? 320 : 1280
  expect(await image.evaluate((img: HTMLImageElement) => img.currentSrc)).toMatch(
    new RegExp(`/showcase/landscape-${expected}\\.svg$`),
  )
})

test('пустая и сломанная картинки оставляют заглушку того же размера', async ({ page }) => {
  await page.goto('/ui')
  const loaded = page.getByTestId('showcase-image')
  const empty = page.getByTestId('showcase-image-empty')
  const broken = page.getByTestId('showcase-image-error')
  await broken.scrollIntoViewIfNeeded()

  await expect(empty).toHaveAttribute('data-state', 'empty')
  await expect(empty).toHaveAccessibleName('Нет изображения')
  await expect(broken).toHaveAttribute('data-state', 'error')
  await expect(broken).toHaveAccessibleName('Горы у озера')
  await expect(broken).toContainText('Не удалось загрузить')
  const size = await box(loaded)
  for (const placeholder of [empty, broken]) {
    const rect = await box(placeholder)
    expect(rect.width).toBeCloseTo(size.width, 0)
    expect(rect.height).toBeCloseTo(size.height, 0)
  }
})
