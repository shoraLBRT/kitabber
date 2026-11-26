import type { Locator, Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// The comment tree on a phone (E09-06, FR-COMMENT, docs/ui/comments.md) against the fake API (e2e/fake-api.mjs): the
// nesting keeps the text readable at 390 px, a branch deeper than one read opens on its own screen
// `<post>/comments/<id>`, and that screen in both profiles — the branch from its comment down, a branch that is gone,
// loading and a failure with «Повторить».

const titled = '/u/anna/0195f3a0-0000-7000-8000-0000000000c1-privet-mir'
// The seven levels of the titled post's first thread, in the order of the fake: «Первый уровень» is 0xd00000.
const level = (n: number) => `0195f3a0-0000-7000-8000-${(0xd00000 + n).toString(16).padStart(12, '0')}`
const branchOf = (n: number) => `${titled}/comments/${level(n)}`
const missing = '0195f3a0-0000-7000-8000-00000000ffff'

// The readable width of docs/ui/comments.md: on a phone no text is narrower than the column of the comments less three
// branch indents (`--space-4`, 16 px each) — about 300 px of a 390 px screen. Reading the replies in place, level after
// level, would narrow the seventh level by six indents.
const indent = 16
const readableIndents = 3

async function expectReadable(page: Page, comments: Locator) {
  const column = (await comments.boundingBox())!.width
  const widths = await comments
    .getByTestId('comment-text')
    .evaluateAll((texts) => texts.map((text) => text.getBoundingClientRect().width))
  expect(widths.length).toBeGreaterThan(0)
  for (const width of widths) expect(width).toBeGreaterThanOrEqual(column - readableIndents * indent)
  expect(Math.min(...widths)).toBeGreaterThanOrEqual(300)
  await expectNoHorizontalScroll(page)
}

test.describe('дерево на телефоне', () => {
  test.beforeEach(() => {
    test.skip(
      !isTouch(),
      'отдельный экран ветки открывается на телефоне; на десктопе — «Показать ещё» на месте',
    )
  })

  test('вложенность не сужает текст, глубокая ветка открывается отдельным экраном', async ({ page }) => {
    await page.goto(titled)
    await waitForHydration(page)
    const comments = page.getByTestId('comments')
    await expect(comments.getByText('Третий уровень')).toBeVisible()
    await expect(comments.getByText('Четвёртый уровень')).toHaveCount(0)
    await expect(comments.getByRole('button', { name: /Показать ещё/ })).toHaveCount(0)
    await expectReadable(page, comments)

    const open = comments.getByRole('link', { name: 'Открыть ветку: 4 ответа' })
    await expect(open).toHaveText('Открыть ветку (4) →')
    await press(open)
    await expect(page).toHaveURL(branchOf(2))
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Ветка комментариев')

    // The branch starts at its comment, at the left edge of the column, and keeps its level in the thread.
    const branch = page.getByTestId('comments')
    const root = branch.getByRole('listitem').filter({ hasText: 'Третий уровень' }).first()
    await expect(root).toHaveAttribute('aria-level', '3')
    const [rootBox, columnBox] = await Promise.all([root.boundingBox(), branch.boundingBox()])
    expect(rootBox!.x).toBe(columnBox!.x)
    await expect(branch.getByText('Шестой уровень')).toBeVisible()
    await expect(branch.getByText('Седьмой уровень')).toHaveCount(0)
    await expect(branch.getByText('Первый уровень')).toHaveCount(0)
    await expectReadable(page, branch)

    // A branch deeper still opens the next screen the same way.
    await press(branch.getByRole('link', { name: 'Открыть ветку: 1 ответ' }))
    await expect(page).toHaveURL(branchOf(5))
    await expect(page.getByTestId('comments').getByText('Седьмой уровень')).toBeVisible()
    await expect(
      page.getByTestId('comments').getByRole('listitem').filter({ hasText: 'Седьмой уровень' }).last(),
    ).toHaveAttribute('aria-level', '7')
    await expectReadable(page, page.getByTestId('comments'))
  })
})

test.describe('экран ветки', () => {
  test('показывает ветку от её комментария и ведёт обратно к посту', async ({ page }) => {
    await page.goto(branchOf(1))
    await waitForHydration(page)
    const branch = page.getByTestId('comments')
    await expect(branch.getByText('Второй уровень')).toBeVisible()
    await expect(branch.getByRole('listitem').first()).toHaveAttribute('aria-level', '2')
    await expect(branch.getByText('Первый уровень')).toHaveCount(0)
    await expect(branch.getByText('Вторая ветка')).toHaveCount(0)
    // One branch: no order of threads and no form under the post; a reply goes under its comment.
    await expect(page.getByTestId('comments-sort')).toHaveCount(0)
    await expect(page.getByTestId('comment-form')).toHaveCount(0)
    await expectNoHorizontalScroll(page)

    const back = page.getByTestId('branch-back')
    await expect(back).toHaveAttribute('href', `${titled}#comments`)
    await press(back)
    await expect(page).toHaveURL(`${titled}#comments`)
    await expect(page.getByTestId('post-title')).toBeVisible()
  })

  test.describe('ветки нет или она не загрузилась', () => {
    test.use({
      missingResources: [
        `${missing}/branch?levels=6`,
        `${missing}/branch?levels=3`,
        'branch?levels=6',
        'branch?levels=3',
      ],
    })

    test('удалённая ветка или неверный адрес — сообщение', async ({ page }) => {
      await page.goto(`${titled}/comments/${missing}`)
      await waitForHydration(page)
      await expect(page.getByTestId('comments-not-found')).toHaveText(
        'Ветка не найдена: комментарий удалён или адрес набран с ошибкой.',
      )
      await expect(page.getByTestId('branch-back')).toBeVisible()
    })

    test('ветка другого поста не найдена под этим', async ({ page }) => {
      await page.goto(`/u/anna/0195f3a0-0000-7000-8000-0000000000c2/comments/${level(1)}`)
      await waitForHydration(page)
      await expect(page.getByTestId('comments-not-found')).toBeVisible()
    })

    test('пока ветка читается — скелетон, при сбое — «Повторить»', async ({ page }) => {
      let release: () => void = () => {}
      const held = new Promise<void>((resolve) => (release = resolve))
      let failures = 1
      await page.route(/\/api\/v1\/comments\/[^/]+\/branch\?/, async (route) => {
        await held
        if (failures-- > 0) return route.fulfill({ status: 503, json: { status: 503 } })
        return route.continue()
      })
      await page.goto(branchOf(1))
      await waitForHydration(page)
      await expect(page.getByTestId('comments-loading')).toHaveAttribute('aria-busy', 'true')
      release()
      const branch = page.getByTestId('comments')
      await expect(branch.getByRole('alert')).toHaveText('Не удалось загрузить комментарии.')

      await press(branch.getByRole('button', { name: 'Повторить' }))
      await expect(branch.getByText('Второй уровень')).toBeVisible()
    })
  })
})
