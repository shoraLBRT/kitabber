import { expect, expectNoHorizontalScroll, press, test, waitForHydration } from './fixtures'

// The order of comments (E09-04, FR-COMMENT, ADR-0058) in both profiles against the fake API (e2e/fake-api.mjs): the
// titled post has three threads — «Первый уровень» (six replies), «Вторая ветка» (two) and the guest comment (none),
// written in that order. The server sorts; the choice is remembered for the next page.

const titled = '/u/anna/0195f3a0-0000-7000-8000-0000000000c1-privet-mir'
const short = '/u/anna/0195f3a0-0000-7000-8000-0000000000c2'
const popular = ['Первый уровень', 'Вторая ветка', 'Комментарий из обсуждения канала']
const newest = ['Комментарий из обсуждения канала', 'Вторая ветка', 'Первый уровень']

test.describe('порядок комментариев', () => {
  test('«Популярные» по умолчанию, «По дате» — сервер отдаёт новые ветки сверху, выбор запоминается', async ({
    page,
  }) => {
    const sorts: (string | null)[] = []
    page.on('request', (request) => {
      const url = new URL(request.url())
      if (/\/api\/v1\/posts\/[^/]+\/comments$/.test(url.pathname) && request.method() === 'GET')
        sorts.push(url.searchParams.get('sort'))
    })
    await page.goto(titled)
    await waitForHydration(page)
    const comments = page.getByTestId('comments')
    const threads = comments.getByTestId('comment-tree').locator(':scope > li > [data-testid="comment-text"]')
    const switcher = comments.getByRole('group', { name: 'Порядок комментариев' })
    const byDate = switcher.getByRole('button', { name: 'По дате' })
    const byPopular = switcher.getByRole('button', { name: 'Популярные' })

    await expect(byPopular).toHaveAttribute('aria-pressed', 'true')
    await expect(threads).toHaveText(popular)
    await expectNoHorizontalScroll(page)

    await press(byDate)
    await expect(byDate).toHaveAttribute('aria-pressed', 'true')
    await expect(byPopular).toHaveAttribute('aria-pressed', 'false')
    await expect(threads).toHaveText(newest)
    // The replies keep the order of the tree inside their thread.
    await expect(comments.getByText('Второй уровень')).toBeVisible()
    await expectNoHorizontalScroll(page)

    await page.reload()
    await waitForHydration(page)
    await expect(byDate).toHaveAttribute('aria-pressed', 'true')
    await expect(threads).toHaveText(newest)

    await press(byPopular)
    await expect(threads).toHaveText(popular)
    expect(sorts).toEqual(['popular', 'date', 'date', 'popular'])
  })

  test('у поста без комментариев переключателя нет', async ({ page }) => {
    await page.goto(short)
    await waitForHydration(page)
    await expect(page.getByTestId('comments-empty')).toBeVisible()
    await expect(page.getByTestId('comments-sort')).toHaveCount(0)
  })
})
