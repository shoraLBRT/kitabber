import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// The comment tree under a post (E09-02, FR-COMMENT, ADR-0054) in both profiles against the fake API
// (e2e/fake-api.mjs): nested branches with their lines, folding a branch, the deep replies loaded by the button on a
// wide screen, six levels at a time, the empty and failed states and a post with 1000 comments. A phone opens a deep
// branch on its own screen instead: e2e/comment-branch.spec.ts.

const titled = '/u/anna/0195f3a0-0000-7000-8000-0000000000c1-privet-mir'
const short = '/u/anna/0195f3a0-0000-7000-8000-0000000000c2'
const big = '/books/0195f3a0-0000-7000-8000-0000000000c3-knigi-oseni'
// The reads of the tree carry the order too (E09-04): «Популярные» unless the reader chose another.
const reads = (kind: string) =>
  kind === 'comments'
    ? ['comments?levels=6&sort=popular', 'comments?levels=3&sort=popular']
    : [`${kind}?levels=6`, `${kind}?levels=3`]

test.describe('дерево комментариев', () => {
  test('ветки вложены, сворачиваются и разворачиваются', async ({ page }) => {
    await page.goto(titled)
    await waitForHydration(page)
    const comments = page.getByTestId('comments')
    await expect(comments.getByRole('heading', { level: 2 })).toHaveText('Комментарии 11')
    // The page fits the phone: nothing, the action panel included, makes the layout wider than the window (#361).
    expect(await page.evaluate(() => window.innerWidth)).toBe(page.viewportSize()!.width)
    const first = comments.getByRole('listitem').filter({ hasText: 'Первый уровень' }).first()
    await expect(first.getByTestId('comment-author').first()).toHaveText('Анна')
    await expect(first).toHaveAttribute('aria-level', '1')
    await expect(first.getByRole('listitem').first()).toHaveAttribute('aria-level', '2')
    await expect(comments.getByText('Второй уровень')).toBeVisible()
    await expect(comments.getByText('Комментарий из обсуждения канала')).toBeVisible()
    await expect(comments.getByRole('link', { name: 'Гость из канала' })).toHaveAttribute(
      'href',
      'https://t.me/example/1',
    )

    const fold = first.getByRole('button', { name: 'Свернуть ветку: 6 ответов' })
    await expect(fold).toHaveAttribute('aria-expanded', 'true')
    await press(fold)
    await expect(comments.getByText('Второй уровень')).toHaveCount(0)
    await expect(comments.getByText('Ответ во второй ветке', { exact: true })).toBeVisible()

    const unfold = first.getByRole('button', { name: 'Развернуть · 6 ответов' })
    await expect(unfold).toHaveAttribute('aria-expanded', 'false')
    await press(unfold)
    await expect(comments.getByText('Второй уровень')).toBeVisible()
    await expectNoHorizontalScroll(page)
  })

  test('глубокие ответы подгружаются по кнопке', async ({ page }) => {
    test.skip(isTouch(), 'на телефоне глубокая ветка открывается отдельным экраном')
    await page.goto(titled)
    await waitForHydration(page)
    const comments = page.getByTestId('comments')
    await expect(comments.getByText('Первый уровень')).toBeVisible()
    await expect(comments.getByText('Седьмой уровень')).toHaveCount(0)

    // A wide screen reads six levels at a time and needs the button once.
    const more = comments.getByRole('button', { name: 'Показать ещё' })
    for (const level of ['Шестой уровень', 'Седьмой уровень']) {
      if (await comments.getByText(level).isVisible()) continue
      await expect(more).toHaveText(/Показать ещё \d+ ответ/)
      await press(more)
      await expect(comments.getByText(level)).toBeVisible()
    }
    await expect(more).toHaveCount(0)
    await expect(
      comments.getByRole('listitem').filter({ hasText: 'Седьмой уровень' }).last(),
    ).toHaveAttribute('aria-level', '7')
    await expectNoHorizontalScroll(page)
  })

  test('без комментариев — приглашение написать первым', async ({ page }) => {
    await page.goto(short)
    await waitForHydration(page)
    await expect(page.getByTestId('comments-empty')).toHaveText('Комментариев пока нет — будьте первым')
  })

  test.describe('сбой загрузки', () => {
    test.use({ missingResources: [...reads('comments'), ...reads('branch')] })

    test('дерево не загрузилось — пост остаётся, комментарии можно повторить', async ({ page }) => {
      let failures = 1
      await page.route(/\/api\/v1\/posts\/[^/]+\/comments\?/, async (route) => {
        if (failures-- > 0) return route.fulfill({ status: 503, json: { status: 503 } })
        return route.continue()
      })
      await page.goto(titled)
      await waitForHydration(page)
      await expect(page.getByTestId('comments').getByRole('alert')).toHaveText(
        'Не удалось загрузить комментарии.',
      )
      await expect(page.getByTestId('post-title')).toBeVisible()

      await press(page.getByTestId('comments').getByRole('button', { name: 'Повторить' }))
      await expect(page.getByTestId('comments').getByText('Первый уровень')).toBeVisible()
    })

    test('ветка не загрузилась — кнопка предлагает повторить', async ({ page }) => {
      test.skip(isTouch(), 'на телефоне глубокая ветка открывается отдельным экраном')
      let failures = 1
      await page.route(/\/api\/v1\/comments\/[^/]+\/branch\?/, async (route) => {
        if (failures-- > 0) return route.fulfill({ status: 503, json: { status: 503 } })
        return route.continue()
      })
      await page.goto(titled)
      await waitForHydration(page)
      const comments = page.getByTestId('comments')
      await press(comments.getByRole('button', { name: 'Показать ещё' }))
      await expect(comments.getByRole('alert')).toHaveText('Не удалось загрузить ответы.')

      await press(comments.getByRole('button', { name: 'Повторить' }))
      await expect(comments.getByRole('alert')).toHaveCount(0)
      await expect(comments.getByText('Шестой уровень')).toBeVisible()
    })
  })

  test('пост с 1000 комментариев открывается без зависаний', async ({ page }) => {
    await page.goto(big)
    await waitForHydration(page)
    const comments = page.getByTestId('comments')
    await expect(comments.getByRole('heading', { level: 2 })).toHaveText('Комментарии 1000', {
      timeout: 10_000,
    })

    // The tree is drawn and the page answers at once: the longest task of the main thread stays short, a fold is quick.
    const shown = await comments.getByTestId('comment').count()
    expect(shown).toBeGreaterThanOrEqual(750)
    const longest = await page.evaluate(
      () =>
        new Promise<number>((resolve) => {
          let most = 0
          new PerformanceObserver((list) => {
            for (const entry of list.getEntries()) most = Math.max(most, entry.duration)
          }).observe({ type: 'longtask', buffered: true })
          setTimeout(() => resolve(most), 200)
        }),
    )
    expect(longest).toBeLessThan(2000)

    const started = Date.now()
    await press(comments.getByTestId('comment-collapse').first())
    await expect(comments.getByTestId('comment-expand')).toHaveCount(1)
    expect(Date.now() - started).toBeLessThan(1000)
    await expectNoHorizontalScroll(page)
  })
})
