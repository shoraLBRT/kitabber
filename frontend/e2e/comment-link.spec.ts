import type { Locator, Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// The permanent link of a comment (E09-07, #104, FR-COMMENT, ADR-0107) in both profiles against the fake API
// (e2e/fake-api.mjs): the post's address with `#comment-<id>` reads the branch that holds the comment, brings it into
// view and highlights it — in place on a wide screen, on the branch's own screen on a phone (ADR-0060); the time of a
// comment leads there and «Скопировать ссылку» in its menu copies the link.

const titled = '/u/anna/0195f3a0-0000-7000-8000-0000000000c1-privet-mir'
// The seven levels of the titled post's first thread, in the order of the fake: «Первый уровень» is 0xd00000.
const level = (n: number) => `0195f3a0-0000-7000-8000-${(0xd00000 + n).toString(16).padStart(12, '0')}`

const missing = '0195f3a0-0000-7000-8000-00000000ffff'

const commentOf = (page: Page, id: string) => page.locator(`[data-comment-id="${id}"]`)

async function expectHighlighted(page: Page, comment: Locator) {
  await expect(comment).toBeInViewport()
  await expect(comment).toHaveAttribute('data-highlighted', 'true')
  await expect(page.locator('[data-highlighted="true"]')).toHaveCount(1)
}

test.describe('ссылка на комментарий', () => {
  // «Седьмой уровень»: past the six levels a wide screen reads, and past the three of a phone.
  test('ссылка на глубокий комментарий подгружает его ветку и подсвечивает его', async ({ page }) => {
    const deepest = level(6)
    await page.goto(`${titled}#comment-${deepest}`)

    if (isTouch()) {
      await expect(page).toHaveURL(new RegExp(`${titled}/comments/${level(3)}#comment-${deepest}$`))
      await expect(page.getByTestId('branch')).toBeVisible()
    } else {
      await expect(page).toHaveURL(new RegExp(`${titled}#comment-${deepest}$`))
    }
    await expectHighlighted(page, commentOf(page, deepest))
    await expect(commentOf(page, deepest).getByTestId('comment-text').first()).toHaveText('Седьмой уровень')
    await expectNoHorizontalScroll(page)
  })

  test('время комментария ведёт на его ссылку', async ({ page }) => {
    await page.goto(titled)
    await waitForHydration(page)
    const comments = page.getByTestId('comments')
    const second = comments
      .locator('[data-testid="comment"][aria-level="1"]')
      .filter({ hasText: 'Вторая ветка' })
    const id = (await second.first().getAttribute('data-comment-id'))!
    const time = commentOf(page, id).locator(':scope > .comment__head').getByTestId('comment-permalink')
    await expect(time).toHaveAttribute('href', `${titled}#comment-${id}`)

    await press(time)

    await expect(page).toHaveURL(new RegExp(`${titled}#comment-${id}$`))
    await expectHighlighted(page, commentOf(page, id))
  })

  test('«Скопировать ссылку» в меню копирует ссылку на комментарий', async ({ page, context, baseURL }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write'])
    await page.goto(`${titled}?from=feed`)
    await waitForHydration(page)
    const id = level(1)
    await press(
      commentOf(page, id)
        .locator(':scope > .comment__head')
        .getByRole('button', { name: 'Меню комментария' }),
    )
    const item = isTouch()
      ? page
          .getByRole('dialog', { name: 'Меню комментария' })
          .getByRole('button', { name: 'Скопировать ссылку' })
      : page
          .getByRole('menu', { name: 'Меню комментария' })
          .getByRole('menuitem', { name: 'Скопировать ссылку' })
    await press(item)

    await expect(page.getByText('Ссылка скопирована')).toBeVisible()
    expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(
      new URL(`${titled}#comment-${id}`, baseURL).href,
    )
  })
})

// A comment that is gone answers 404 to the read of its place: the browser logs that.
test.describe('ссылка на комментарий, которого нет', () => {
  test.use({ missingResources: [`/api/v1/comments/${missing}/place`] })

  test('оставляет пост как есть', async ({ page }) => {
    await page.goto(`${titled}#comment-${missing}`)
    await expect(page.getByTestId('comment').first()).toBeVisible()
    await expect(page.locator('[data-highlighted="true"]')).toHaveCount(0)
    await expect(page).toHaveURL(new RegExp(`${titled}#comment-`))
  })
})
