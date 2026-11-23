import type { Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// The comment form and the reply form (E09-03, FR-COMMENT, ADR-0056) in both profiles against the fake API
// (e2e/fake-api.mjs, which answers a comment without keeping it): a guest gets the sign-in window, a comment sent is in
// the tree at once and dimmed until the server keeps it, a failed send takes it out and gives the text back, and the
// text written waits in a draft across a reload. The form's states are in tests/comments/CommentForm.spec.ts.

const titled = '/u/anna/0195f3a0-0000-7000-8000-0000000000c1-privet-mir'
const short = '/u/anna/0195f3a0-0000-7000-8000-0000000000c2'

async function signIn(page: Page): Promise<void> {
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

async function openPost(page: Page, path: string) {
  await page.goto(path)
  await waitForHydration(page)
  const comments = page.getByTestId('comments')
  await expect(comments.getByTestId('comment-form')).toBeVisible()
  return comments
}

test.describe('форма комментария', () => {
  test('гость вместо формы видит окно входа', async ({ page }) => {
    const comments = await openPost(page, titled)
    await press(comments.getByRole('button', { name: 'Комментарий…' }))
    const dialog = page.getByRole('dialog', { name: 'Вход' })
    await expect(dialog).toBeVisible()
    await expect(comments.getByRole('textbox')).toHaveCount(0)
  })

  test('комментарий появляется сразу и подтверждается сервером', async ({ page }) => {
    await signIn(page)
    // The server answers once the test lets it, so the comment is seen before the answer.
    let answer = () => {}
    const held = new Promise<void>((resolve) => (answer = resolve))
    await page.route('**/bff/comments', async (route) => {
      await held
      return route.continue()
    })

    const comments = await openPost(page, short)
    // No comments yet: the form is open at once.
    const field = comments.getByRole('textbox', { name: 'Текст комментария' })
    await field.fill('Мой первый комментарий')
    await press(comments.getByRole('button', { name: 'Отправить' }))

    const sent = comments.getByTestId('comment').filter({ hasText: 'Мой первый комментарий' })
    await expect(sent).toHaveAttribute('aria-busy', 'true')
    await expect(sent.getByTestId('comment-sending')).toHaveText('Отправляется…')
    await expect(comments.getByRole('button', { name: 'Комментарий…' })).toBeVisible()
    await expect(comments.getByRole('heading', { level: 2 })).toHaveText('Комментарии 1')

    answer()
    await expect(sent).not.toHaveAttribute('aria-busy', 'true')
    await expect(sent.getByTestId('comment-author')).toHaveText('Анна')
    await expectNoHorizontalScroll(page)
  })

  test('ответ встаёт в ветку под комментарием', async ({ page }) => {
    await signIn(page)
    const comments = await openPost(page, titled)
    const second = comments.getByTestId('comment').filter({ hasText: 'Вторая ветка' }).first()
    await press(second.getByTestId('comment-reply').first())
    const field = second.getByRole('textbox', { name: 'Текст ответа' })
    await expect(field).toBeFocused()
    await field.fill('Ответ из формы')
    await press(second.getByRole('button', { name: 'Отправить' }))

    await expect(second.getByRole('textbox')).toHaveCount(0)
    const reply = second.getByRole('listitem').filter({ hasText: 'Ответ из формы' })
    await expect(reply).toHaveAttribute('aria-level', '2')
    await expect(reply).not.toHaveAttribute('aria-busy', 'true')
    await expect(comments.getByRole('heading', { level: 2 })).toHaveText('Комментарии 12')
    await expectNoHorizontalScroll(page)
  })

  test('при сбое комментарий откатывается, текст возвращается в форму', async ({ page }) => {
    await signIn(page)
    const comments = await openPost(page, titled)
    await press(comments.getByRole('button', { name: 'Комментарий…' }))
    // The fake API fails a text «сбой» as the server would; the site's server answers the form with the failure.
    await comments.getByRole('textbox', { name: 'Текст комментария' }).fill('сбой')
    await press(comments.getByRole('button', { name: 'Отправить' }))

    await expect(comments.getByTestId('comment-form-error')).toHaveText(
      'Не удалось отправить комментарий. Текст сохранён — попробуйте ещё раз.',
    )
    await expect(comments.getByRole('textbox', { name: 'Текст комментария' })).toHaveValue('сбой')
    await expect(comments.getByTestId('comment-text').filter({ hasText: /^сбой$/ })).toHaveCount(0)
    await expect(comments.getByRole('heading', { level: 2 })).toHaveText('Комментарии 11')
  })

  test('черновик переживает перезагрузку страницы', async ({ page }) => {
    await signIn(page)
    let comments = await openPost(page, titled)
    await press(comments.getByRole('button', { name: 'Комментарий…' }))
    await comments.getByRole('textbox', { name: 'Текст комментария' }).fill('Допишу позже')

    await page.reload()
    await waitForHydration(page)
    comments = page.getByTestId('comments')
    await expect(comments.getByRole('textbox', { name: 'Текст комментария' })).toHaveValue('Допишу позже')
  })
})
