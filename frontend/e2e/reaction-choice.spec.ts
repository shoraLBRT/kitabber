import type { Locator, Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// Choosing a reaction from the set (E10-02, FR-REACT, ADR-0101) in both profiles against the fake API (e2e/fake-api.mjs,
// which keeps the person's reactions per client address): on a desktop the set opens by hovering «Выбрать реакцию», on
// a phone by a long press on a chip; a reaction is put, changed to another one in one choice, kept over a reload and
// taken back, and the person has one reaction on the post at a time. The states of the set (loading, error, empty) and
// the timings are in tests/reactions/reactions.spec.ts.

const inTopic = '/books/0195f3a0-0000-7000-8000-0000000000c3-knigi-oseni'

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

/** Holds a finger on the element long enough for the set to open, as a phone does. */
async function longPress(target: Locator): Promise<void> {
  const box = (await target.boundingBox())!
  const at = { clientX: box.x + box.width / 2, clientY: box.y + box.height / 2, isPrimary: true }
  await target.dispatchEvent('pointerdown', { ...at, pointerType: 'touch' })
  await target.page().waitForTimeout(700)
  await target.dispatchEvent('pointerup', { ...at, pointerType: 'touch' })
}

/** Opens the set the way the device does: hovering on a desktop, a long press on a phone. */
async function openSet(row: Locator): Promise<Locator> {
  if (isTouch()) await longPress(row.getByTestId('post-like'))
  else await row.getByRole('button', { name: 'Выбрать реакцию' }).hover()
  const set = row.getByTestId('reaction-picker')
  await expect(set).toBeVisible()
  return set
}

test.use({ actionTimeout: 10_000 })

test.describe('выбор реакции', () => {
  test.beforeEach(async ({ page }) => asOwnClient(page))

  test('реакция выбирается из набора, меняется одним выбором и снимается', async ({ page }) => {
    await signIn(page)
    await page.goto(inTopic)
    await waitForHydration(page)
    const row = page.getByRole('toolbar', { name: 'Действия с постом' }).getByTestId('reaction-row')
    await expect(row.getByTestId('post-like-count')).toHaveText('3')

    let set = await openSet(row)
    await expect(set.getByRole('button')).toHaveCount(7)
    await expect(set.getByRole('button', { name: 'Тепло' })).toHaveAttribute('aria-pressed', 'false')
    await expectNoHorizontalScroll(page)
    await press(set.getByRole('button', { name: 'Тепло' }))
    await expect(set).toBeHidden()
    const heart = row.getByTestId('post-like-heart')
    await expect(heart).toHaveAttribute('aria-pressed', 'true')
    await expect(row.getByTestId('post-like-heart-count')).toHaveText('1')
    await expect(row.getByTestId('post-like-count')).toHaveText('3')

    // Another kind replaces the own one: one reaction per person.
    set = await openSet(row)
    await expect(set.getByRole('button', { name: 'Тепло' })).toHaveAttribute('aria-pressed', 'true')
    await press(set.getByRole('button', { name: 'Браво' }))
    const clap = row.getByTestId('post-like-clap')
    await expect(clap).toHaveAttribute('aria-pressed', 'true')
    await expect(heart).toHaveCount(0)
    await expect(row.getByTestId('post-like-count')).toHaveText('3')

    await page.reload()
    await waitForHydration(page)
    await expect(clap).toHaveAttribute('aria-pressed', 'true')
    await expect(row.getByTestId('post-like-clap-count')).toHaveText('1')

    // A press on the own reaction takes it back.
    await press(clap)
    await expect(clap).toHaveCount(0)
    await expect(row.getByTestId('post-like')).toHaveAttribute('aria-pressed', 'false')
  })

  test('«Нравится» меняется на другую реакцию, а не добавляется к ней', async ({ page }) => {
    await signIn(page)
    await page.goto(inTopic)
    await waitForHydration(page)
    const row = page.getByRole('toolbar', { name: 'Действия с постом' }).getByTestId('reaction-row')
    const like = row.getByTestId('post-like')
    await press(like)
    await expect(like).toHaveAttribute('aria-pressed', 'true')
    await expect(row.getByTestId('post-like-count')).toHaveText('4')

    const set = await openSet(row)
    await press(set.getByRole('button', { name: 'Полезно' }))
    await expect(row.getByTestId('post-like-idea')).toHaveAttribute('aria-pressed', 'true')
    await expect(like).toHaveAttribute('aria-pressed', 'false')
    await expect(row.getByTestId('post-like-count')).toHaveText('3')
  })
})
