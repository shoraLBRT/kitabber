import { expect, isTouch, press, test, waitForHydration, box } from './fixtures'
import type { Browser, Locator, Page } from '@playwright/test'

// The cover of a blog (E04-04, FR-PROFILE, docs/ui/blog.md, ADR-0079) in both profiles against the fake API
// (e2e/fake-api.mjs): the owner uploads a picture, drags it with the mouse (desktop) or a finger (mobile, real touch
// events through the DevTools protocol), saves it, and the position is the same after a reload and on the other
// screen size; then removes it. The fake keeps the cover per client address, so every test sends its own
// `x-forwarded-for`. The real rules (whose image, the range of the position) are tested by the backend
// (Platform.Modules.Blogs.IntegrationTests, ProfileCoverApiTests).
async function asOwnClient(page: Page): Promise<string> {
  const octet = () => Math.floor(Math.random() * 254) + 1
  const client = `10.${octet()}.${octet()}.${octet()}`
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': client })
  return client
}

async function signIn(page: Page): Promise<void> {
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

// A square JPEG drawn by the browser itself: the fake media service takes JPEG only, and a square picture is taller
// than the header, so it can be moved.
async function squareJpeg(page: Page): Promise<Buffer> {
  const data = await page.evaluate(() => {
    const canvas = document.createElement('canvas')
    canvas.width = 300
    canvas.height = 300
    const context = canvas.getContext('2d')!
    context.fillStyle = '#336699'
    context.fillRect(0, 0, 300, 150)
    context.fillStyle = '#cc9933'
    context.fillRect(0, 150, 300, 150)
    return canvas.toDataURL('image/jpeg')
  })
  return Buffer.from(data.split(',')[1]!, 'base64')
}

// Drags the picture up by `distance` pixels: with the mouse on a desktop, with a finger on a phone.
async function dragUp(page: Page, handle: Locator, distance: number): Promise<void> {
  const rect = await box(handle)
  const x = rect.x + rect.width * 0.25
  const y = rect.y + rect.height * 0.8
  if (isTouch()) {
    const cdp = await page.context().newCDPSession(page)
    const touch = (type: 'touchStart' | 'touchMove' | 'touchEnd', at: number) =>
      cdp.send('Input.dispatchTouchEvent', {
        type,
        touchPoints: type === 'touchEnd' ? [] : [{ x, y: at }],
      })
    await touch('touchStart', y)
    for (let step = 1; step <= 5; step += 1) await touch('touchMove', y - (distance * step) / 5)
    await touch('touchEnd', y - distance)
    await cdp.detach()
  } else {
    await page.mouse.move(x, y)
    await page.mouse.down()
    await page.mouse.move(x, y - distance, { steps: 5 })
    await page.mouse.up()
  }
}

// The same blog on the other screen size, as a guest with the same client address.
async function otherScreen(browser: Browser, client: string): Promise<Page> {
  const context = await browser.newContext({
    viewport: isTouch() ? { width: 1440, height: 900 } : { width: 390, height: 844 },
    extraHTTPHeaders: { 'x-forwarded-for': client },
  })
  return context.newPage()
}

test.describe('обложка блога', () => {
  test('владелец загружает и перетаскивает обложку, положение одинаково везде, потом удаляет её', async ({
    page,
    browser,
  }) => {
    const client = await asOwnClient(page)
    await signIn(page)
    await page.goto('/u/anna')
    await waitForHydration(page)
    const cover = page.getByTestId('blog-cover')
    await expect(cover.getByTestId('blog-cover-image')).toHaveCount(0)

    await cover.getByTestId('blog-cover-file').setInputFiles({
      name: 'cover.jpg',
      mimeType: 'image/jpeg',
      buffer: await squareJpeg(page),
    })
    const handle = cover.getByRole('slider', { name: 'Положение обложки' })
    await expect(handle).toHaveAttribute('aria-valuenow', '50')
    await expect
      .poll(() => cover.getByTestId('blog-cover-image').evaluate((img: HTMLImageElement) => img.naturalWidth))
      .toBeGreaterThan(0)

    // A square in a 10 : 3 header hides 70 % of the header's width; dragging up by a fifth of that shows a band
    // 20 points lower down the picture.
    const frame = await box(cover.locator('.cover__box'))
    expect(frame.height / frame.width).toBeCloseTo(0.3, 2)
    await dragUp(page, handle, frame.width * 0.7 * 0.2)
    const moved = Number(await handle.getAttribute('aria-valuenow'))
    expect(moved).toBeGreaterThanOrEqual(68)
    expect(moved).toBeLessThanOrEqual(72)
    // Chromium drops the first tap after touches sent through the DevTools protocol (a bare page shows the same), so the
    // button is clicked here; the tap itself works on the page everywhere else.
    await cover.getByRole('button', { name: 'Сохранить' }).click()
    await expect(page.getByText('Обложка сохранена')).toBeVisible()
    await expect(handle).toHaveCount(0)
    const image = cover.getByTestId('blog-cover-image')
    await expect(image).toHaveAttribute('src', /\/__images\/.+-cover-1920\.webp$/)
    await expect(image).toHaveCSS('object-position', `50% ${moved}%`)

    // The position belongs to the blog: the same after a reload and on the other screen size.
    await page.reload()
    await waitForHydration(page)
    await expect(cover).toHaveAttribute('data-position', String(moved))
    const other = await otherScreen(browser, client)
    await other.goto('/u/anna')
    const otherCover = other.getByTestId('blog-cover')
    await expect(otherCover).toHaveAttribute('data-position', String(moved))
    await expect(otherCover.getByTestId('blog-cover-image')).toHaveCSS('object-position', `50% ${moved}%`)
    const otherFrame = await box(otherCover.locator('.cover__box'))
    expect(otherFrame.width).not.toBeCloseTo(frame.width, 0)
    expect(otherFrame.height / otherFrame.width).toBeCloseTo(0.3, 2)
    // A guest only sees the cover.
    await expect(otherCover.getByRole('group', { name: 'Обложка блога' })).toHaveCount(0)
    await other.context().close()

    await press(cover.getByRole('button', { name: 'Удалить обложку' }))
    await expect(page.getByText('Обложка удалена')).toBeVisible()
    await expect(cover.getByTestId('blog-cover-image')).toHaveCount(0)
    await expect(cover.getByRole('button', { name: 'Загрузить обложку' })).toBeVisible()
    await page.reload()
    await expect(page.getByTestId('blog-cover').getByTestId('blog-cover-image')).toHaveCount(0)
  })

  test('чужой блог без обложки: фон без картинки и без кнопок', async ({ page }) => {
    await asOwnClient(page)
    await page.goto('/u/boris')
    const cover = page.getByTestId('blog-cover')
    await expect(cover).toBeVisible()
    await expect(cover.getByTestId('blog-cover-image')).toHaveCount(0)
    await expect(cover.getByRole('button')).toHaveCount(0)
  })
})
