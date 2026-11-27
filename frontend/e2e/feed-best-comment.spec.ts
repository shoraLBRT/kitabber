import { expect, expectNoHorizontalScroll, isTouch, test, waitForHydration } from './fixtures'

// The best comment under a post's card in the feed (E08-15, FR-FEED, ADR-0062) in both profiles against the fake API
// (e2e/fake-api.mjs): «Привет, мир» has «Вторая ветка» (two likes) as its best comment, «Книги осени» the earliest of
// its unliked comments, the short post has none. The choice itself (rating, ties, deleted comments, one read per page)
// is the backend's and is tested there (FeedBestCommentApiTests).

const longText =
  `${'Очень длинный комментарий, который не помещается в несколько строк карточки. '.repeat(4)}`.trim()

test.describe('лучший комментарий в ленте', () => {
  test('под постом с комментариями — лучший, под постом без них — ничего', async ({ page }) => {
    await page.goto('/new')
    await waitForHydration(page)
    const items = page.getByTestId('feed-item')
    await expect(items).toHaveCount(3)

    const topic = items.nth(0).getByTestId('feed-best-comment')
    await expect(topic.getByTestId('feed-best-comment-author')).toHaveText('Борис')
    await expect(topic.getByTestId('feed-best-comment-text')).toHaveText('Комментарий 1.1')
    await expect(items.nth(1).getByTestId('feed-best-comment')).toHaveCount(0)
    const titled = items.nth(2).getByTestId('feed-best-comment')
    await expect(titled.getByTestId('feed-best-comment-author')).toHaveText('Борис')
    await expect(titled.getByTestId('feed-best-comment-text')).toHaveText('Вторая ветка')
    await expectNoHorizontalScroll(page)
  })

  test('длинный текст обрезается многоточием в несколько строк', async ({ page }) => {
    // The first page comes rendered by the server; the second is read by the browser, so the test can change it.
    await page.route('**/api/v1/feeds/fresh?cursor=Mg', async (route) => {
      const response = await route.fetch()
      const body = (await response.json()) as { items: { bestComment: { text: string } | null }[] }
      body.items[0]!.bestComment!.text = longText
      await route.fulfill({ response, json: body })
    })
    await page.goto('/new')
    await waitForHydration(page)

    const text = page.getByTestId('feed-item').nth(2).getByTestId('feed-best-comment-text')
    await expect(text).toHaveText(longText)
    const box = await text.evaluate((element) => {
      const style = getComputedStyle(element)
      return {
        clamped: element.scrollHeight > element.clientHeight,
        lines: style.getPropertyValue('-webkit-line-clamp'),
        overflow: style.overflow,
      }
    })
    expect(box).toEqual({ clamped: true, lines: '3', overflow: 'hidden' })
    await expectNoHorizontalScroll(page)
  })

  test('нажатие открывает комментарий на странице поста', async ({ page }) => {
    await page.goto('/new')
    await waitForHydration(page)
    const best = page.getByTestId('feed-item').nth(2).getByTestId('feed-best-comment')
    await expect(best).toBeVisible()

    await best.click()

    await expect(page).toHaveURL(
      /\/u\/anna\/0195f3a0-0000-7000-8000-0000000000c1-privet-mir#comment-[0-9a-f-]{36}$/,
    )
    const comment = page.getByTestId('comment').filter({ hasText: 'Вторая ветка' }).first()
    await expect(comment).toBeInViewport()
    expect(new URL(page.url()).hash).toBe(`#comment-${await comment.getAttribute('data-comment-id')}`)
  })

  // «Пятый уровень» of «Привет, мир»: within the six levels a wide screen reads, past the three of a phone, which opens
  // the screen of «Второй уровень» with it in its read (E09-07, ADR-0107).
  test('комментарий глубже прочитанного открывается экраном своей ветки', async ({ page }) => {
    const post = '/u/anna/0195f3a0-0000-7000-8000-0000000000c1-privet-mir'
    const id = '0195f3a0-0000-7000-8000-000000d00004'
    await page.goto(`${post}#comment-${id}`)

    if (isTouch()) {
      await expect(page).toHaveURL(
        new RegExp(`${post}/comments/0195f3a0-0000-7000-8000-000000d00001#comment-${id}$`),
      )
      await expect(page.getByTestId('branch')).toBeVisible()
    }
    const comment = page.locator(`[data-comment-id="${id}"]`)
    await expect(comment).toBeInViewport()
    await expect(comment).toHaveAttribute('data-highlighted', 'true')
  })
})
