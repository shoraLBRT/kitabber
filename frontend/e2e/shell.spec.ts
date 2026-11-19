import { box, expect, expectNoHorizontalScroll, isTouch, makeScrollable, press, test } from './fixtures'

// Site shell in a real browser (E02-05 #15, E02-06 #16, docs/ui/README.md): column widths, hidden columns at the
// breakpoints 640 / 925 / 1240, sticky columns, the bottom bar, the floating "Написать" and the "Меню" screen.
// Every test runs in both profiles; tests about one width set it themselves.

test.describe('каркас сайта', () => {
  test('раскладка профиля: три колонки на десктопе, нижняя панель на телефоне', async ({ page }) => {
    await page.goto('/')
    const menu = page.locator('[data-column="menu"]')
    const feed = page.locator('[data-column="feed"]')
    const aside = page.locator('[data-column="aside"]')
    const tabBar = page.getByTestId('tab-bar')
    const writeButton = page.getByTestId('write-button')
    const header = page.getByTestId('shell-header')

    // The empty state of the feed (FR-UI): the feed itself is not connected yet.
    await expect(page.getByText('Здесь появятся посты')).toBeVisible()

    if (!isTouch()) {
      expect((await box(menu)).width).toBe(220)
      expect((await box(feed)).width).toBe(640)
      expect((await box(aside)).width).toBe(320)
      await expect(tabBar).toBeHidden()
      await expect(writeButton).toBeHidden()
      await expect(header.getByRole('link', { name: 'Написать' })).toBeVisible()
      await expect(header.getByRole('link', { name: 'Войти' })).toBeVisible()
    } else {
      await expect(menu).toBeHidden()
      await expect(aside).toBeHidden()
      await expect(tabBar).toBeVisible()
      // Header buttons move to the phone shell: no "Написать", bell, avatar or "Войти" in the header.
      await expect(header.getByRole('link', { name: 'Написать' })).toBeHidden()
      await expect(header.getByRole('link', { name: 'Войти' })).toBeHidden()
      await expect(header.getByRole('link', { name: 'Уведомления' })).toHaveCount(0)
      await expect(page.getByRole('navigation', { name: 'Основное меню' })).toBeHidden()

      const viewport = page.viewportSize()!
      const bar = await box(tabBar)
      expect(bar.y + bar.height).toBe(viewport.height)
      expect(bar.width).toBe(viewport.width)
      for (const tab of await tabBar.getByRole('link').all()) {
        expect((await box(tab)).height).toBeGreaterThanOrEqual(44)
      }

      await expect(writeButton).toBeVisible()
      const button = await box(writeButton)
      expect(bar.y - (button.y + button.height)).toBe(16)
    }
    await expectNoHorizontalScroll(page)
  })

  test('колонки прилипают при прокрутке', async ({ page }) => {
    await page.goto('/')
    await makeScrollable(page)
    if (!isTouch()) {
      const menu = page.locator('[data-column="menu"]')
      const aside = page.locator('[data-column="aside"]')
      const before = { menu: (await box(menu)).y, aside: (await box(aside)).y }
      await page.evaluate(() => window.scrollTo(0, 1500))
      await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(1500)
      // Sticky under the header: header height --space-16 plus the --space-6 gap.
      expect((await box(menu)).y).toBe(88)
      expect((await box(aside)).y).toBe(88)
      expect(before.menu).toBe(88)
      expect(before.aside).toBe(88)
    } else {
      await page.evaluate(() => window.scrollTo(0, 1500))
      await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(1500)
      const bar = await box(page.getByTestId('tab-bar'))
      expect(bar.y + bar.height).toBe(page.viewportSize()!.height)
    }
    // The header stays on top in both profiles.
    expect((await box(page.getByTestId('shell-header'))).y).toBe(0)
  })

  test('1100 px: правая колонка скрыта, левое меню на месте', async ({ page }) => {
    await page.setViewportSize({ width: 1100, height: 900 })
    await page.goto('/')
    await expect(page.locator('[data-column="aside"]')).toBeHidden()
    expect((await box(page.locator('[data-column="menu"]'))).width).toBe(220)
    expect((await box(page.locator('[data-column="feed"]'))).width).toBe(640)
    await expect(page.getByTestId('tab-bar')).toBeHidden()
    await expectNoHorizontalScroll(page)
  })

  test('800 px: нижняя панель без плавающей кнопки, «Написать» в шапке', async ({ page }) => {
    await page.setViewportSize({ width: 800, height: 900 })
    await page.goto('/')
    await expect(page.getByTestId('tab-bar')).toBeVisible()
    await expect(page.getByTestId('write-button')).toBeHidden()
    await expect(page.locator('[data-column="menu"]')).toBeHidden()
    await expect(page.locator('[data-column="aside"]')).toBeHidden()
    await expect(page.getByTestId('shell-header').getByRole('link', { name: 'Написать' })).toBeVisible()
    await expectNoHorizontalScroll(page)
  })

  test('390 px: «Меню» открывает экран меню', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 })
    await page.goto('/')
    const menuTab = page.getByTestId('tab-bar').getByRole('link', { name: 'Меню' })
    await press(menuTab)
    await expect(page).toHaveURL(/\/menu$/)
    await expect(menuTab).toHaveAttribute('aria-current', 'page')

    const screen = page.getByTestId('menu-screen')
    await expect(screen.getByRole('heading', { name: 'Меню' })).toBeVisible()
    await expect(screen.getByRole('link', { name: 'Войти' })).toBeVisible()
    await expect(screen.getByRole('heading', { name: 'Темы' })).toBeVisible()
    await expect(screen.getByTestId('theme-switch')).toBeVisible()
    await expect(screen.getByRole('link', { name: 'О проекте' })).toBeVisible()
    await expect(screen.getByRole('link', { name: 'Правила' })).toBeVisible()
    await expectNoHorizontalScroll(page)
  })

  test('390 px: повторное нажатие на текущую вкладку возвращает наверх', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 })
    await page.goto('/')
    await makeScrollable(page)
    await page.evaluate(() => window.scrollTo(0, 1200))
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(1200)

    const feedTab = page.getByTestId('tab-bar').getByRole('link', { name: 'Лента' })
    await expect(feedTab).toHaveAttribute('aria-current', 'page')
    await press(feedTab)
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0)
    await expect(page).toHaveURL(/\/$/)
  })

  test('безопасные зоны: viewport-fit=cover, шапка и панель отступают от краёв', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 })
    await page.goto('/')
    await expect(page.locator('meta[name="viewport"]')).toHaveAttribute('content', /viewport-fit=cover/)

    const header = page.getByTestId('shell-header')
    const tabBar = page.getByTestId('tab-bar')
    const plain = { header: (await box(header)).height, bar: (await box(tabBar)).height }

    // Chromium emulates the insets of a notched screen through the DevTools protocol.
    const cdp = await page.context().newCDPSession(page)
    await cdp.send('Emulation.setSafeAreaInsetsOverride', { insets: { top: 47, bottom: 34, left: 0, right: 0 } })
    await expect.poll(async () => (await box(header)).height).toBe(plain.header + 47)
    await expect.poll(async () => (await box(tabBar)).height).toBe(plain.bar + 34)
    const bar = await box(tabBar)
    expect(bar.y + bar.height).toBe(page.viewportSize()!.height)
    await expectNoHorizontalScroll(page)
  })
})
