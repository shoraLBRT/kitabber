import type { Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, test, waitForHydration } from './fixtures'

// «Поделиться» of the post page (E07-07, #97, ADR-0103) in both profiles against the fake API (e2e/fake-api.mjs). The
// browser's Web Share API is set by each test: a stub that records what it was given, or none at all. The social
// networks are never reached: their share addresses are answered here.

const titled = '0195f3a0-0000-7000-8000-0000000000c1'
const canonical = `/u/anna/${titled}-privet-mir`

/** The post page reached by a link with a query and a hash, so the shared link has to drop them. */
const openPost = async (page: Page) => {
  await page.goto(`${canonical}?from=feed#comments`)
  await waitForHydration(page)
  return page.getByRole('toolbar', { name: 'Действия с постом' }).getByRole('button', { name: 'Поделиться' })
}

const withoutWebShare = (page: Page) =>
  page.addInitScript(() => {
    delete (Navigator.prototype as { share?: unknown }).share
  })

const withWebShare = (page: Page) =>
  page.addInitScript(() => {
    const shared: ShareData[] = []
    Object.assign(window, { shared })
    Object.defineProperty(Navigator.prototype, 'share', {
      configurable: true,
      value: (data: ShareData) => {
        shared.push(data)
        return Promise.resolve()
      },
    })
  })

const isPhone = (page: Page) => page.viewportSize()!.width < 640

test.describe('поделиться постом', () => {
  test('на телефоне открывается системное меню, на десктопе — список', async ({ page, baseURL }) => {
    await withWebShare(page)
    const share = await openPost(page)
    await share.click()
    const shared = () => page.evaluate(() => (window as unknown as { shared: ShareData[] }).shared)
    if (isPhone(page)) {
      await expect.poll(shared).toEqual([{ title: 'Привет, мир', url: new URL(canonical, baseURL).href }])
      await expect(page.getByRole('menu', { name: 'Поделиться' })).toHaveCount(0)
    } else {
      await expect(page.getByRole('menu', { name: 'Поделиться' })).toBeVisible()
      expect(await shared()).toEqual([])
    }
    await expectNoHorizontalScroll(page)
  })

  test('список копирует каноническую ссылку', async ({ page, context, baseURL }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write'])
    await withoutWebShare(page)
    const share = await openPost(page)
    await share.click()
    const menu = page.getByRole('menu', { name: 'Поделиться' })
    await expect(menu.getByRole('menuitem')).toHaveText(['Скопировать ссылку', 'Telegram', 'ВКонтакте', 'X'])
    // The list opens over the panel, inside the window.
    const box = (await menu.boundingBox())!
    expect(box.y).toBeGreaterThanOrEqual(0)
    expect(box.y + box.height).toBeLessThanOrEqual((await share.boundingBox())!.y)
    await expectNoHorizontalScroll(page)

    await menu.getByRole('menuitem', { name: 'Скопировать ссылку' }).click()
    await expect(page.getByText('Ссылка скопирована')).toBeVisible()
    expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(new URL(canonical, baseURL).href)
  })

  test('соцсеть открывается в новой вкладке с канонической ссылкой', async ({ page, context, baseURL }) => {
    await context.route('https://t.me/**', (route) =>
      route.fulfill({ contentType: 'text/html', body: '<p>ok</p>' }),
    )
    await withoutWebShare(page)
    const share = await openPost(page)
    await share.click()
    const opened = context.waitForEvent('page')
    await page.getByRole('menu', { name: 'Поделиться' }).getByRole('menuitem', { name: 'Telegram' }).click()
    const tab = await opened
    await tab.waitForLoadState()
    const url = new URL(tab.url())
    expect(`${url.origin}${url.pathname}`).toBe('https://t.me/share/url')
    expect(url.searchParams.get('url')).toBe(new URL(canonical, baseURL).href)
    expect(url.searchParams.get('text')).toBe('Привет, мир')
  })
})
