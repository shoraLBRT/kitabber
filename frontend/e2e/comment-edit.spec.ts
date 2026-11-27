import type { Locator, Page } from '@playwright/test'
import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'

// Changing and deleting an own comment (E09-05, FR-COMMENT, ADR-0059) in both profiles against the fake API
// (e2e/fake-api.mjs, which answers a change without keeping it): the author's «…» menu on the person's own comments, a
// dropdown on a wide screen and a bottom sheet on a phone; «Редактировать» only while the time to change the text
// lasts, then the mark «изменено»; a deleted comment with replies stays as the stub «Комментарий удалён», one without
// replies goes. The states of the menu and the form are in tests/comments/CommentEdit.spec.ts.

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

/** The comment of the first level with this text, not one of its replies. */
const thread = (comments: Locator, text: string) =>
  comments.locator('[data-testid="comment"][aria-level="1"]').filter({ hasText: text }).first()

/** The «…» button of the comment itself, not of a reply under it. */
const menuOf = (comment: Locator) =>
  comment.locator(':scope > .comment__head').getByRole('button', { name: 'Меню комментария' })

/** Opens the comment's menu and gives its items: the menu items on a wide screen, the buttons of the sheet on a phone. */
async function openMenu(page: Page, comment: Locator) {
  await press(menuOf(comment))
  return isTouch()
    ? page
        .getByRole('dialog', { name: 'Меню комментария' })
        .getByTestId('comment-menu-sheet')
        .getByRole('button')
    : page.getByRole('menu', { name: 'Меню комментария' }).getByRole('menuitem')
}

async function deleteComment(page: Page, comment: Locator) {
  const items = await openMenu(page, comment)
  await press(items.filter({ hasText: 'Удалить' }))
  const dialog = page.getByRole('dialog', { name: 'Удалить комментарий?' })
  await expect(dialog).toBeVisible()
  await press(dialog.getByRole('button', { name: 'Удалить' }))
  await expect(dialog).toBeHidden()
}

// A stuck tap fails on its own, with its call log, instead of running into the test's timeout.
test.use({ actionTimeout: 10_000 })

test.describe('правка и удаление комментария', () => {
  // Someone else's comment has its own menu with «Заблокировать автора» (E11-06): e2e/black-list.spec.ts.
  test('у своего комментария после срока — только «Удалить»', async ({ page }) => {
    await signIn(page)
    const comments = await openPost(page, titled)
    const own = thread(comments, 'Первый уровень')
    const foreign = thread(comments, 'Вторая ветка')
    await expect(menuOf(own)).toBeVisible()
    await expect(foreign.locator(':scope > .comment__head').getByTestId('comment-edited')).toHaveText(
      '· изменено',
    )
    await expect(await openMenu(page, own)).toHaveText(['Скопировать ссылку', 'В закладки', 'Удалить'])
    await expectNoHorizontalScroll(page)
  })

  // Any reader copies a comment's link (E09-07): e2e/comment-link.spec.ts.
  test('у гостя в меню комментария — «Скопировать ссылку» и «В закладки»', async ({ page }) => {
    const comments = await openPost(page, titled)
    await expect(await openMenu(page, thread(comments, 'Вторая ветка'))).toHaveText([
      'Скопировать ссылку',
      'В закладки',
    ])
  })

  test('свой комментарий правится в срок и помечается «изменено»', async ({ page }) => {
    await signIn(page)
    const comments = await openPost(page, short)
    await comments.getByRole('textbox', { name: 'Текст комментария' }).fill('Опечтка')
    await press(comments.getByRole('button', { name: 'Отправить' }))
    const written = thread(comments, 'Опечтка')
    await expect(written).not.toHaveAttribute('aria-busy', 'true')
    // The server's id, so the comment is still found once its text changes.
    const sent = comments.locator(`[data-comment-id="${await written.getAttribute('data-comment-id')}"]`)

    await press((await openMenu(page, sent)).filter({ hasText: 'Редактировать' }))
    const field = sent.getByRole('textbox', { name: 'Текст комментария' })
    await expect(field).toBeFocused()
    await expect(field).toHaveValue('Опечтка')
    await field.fill('Опечатка')
    await press(sent.getByRole('button', { name: 'Сохранить' }))

    await expect(sent.getByRole('textbox')).toHaveCount(0)
    await expect(sent.getByTestId('comment-text')).toHaveText('Опечатка')
    await expect(sent.getByTestId('comment-edited')).toHaveText('· изменено')
    await expectNoHorizontalScroll(page)
  })

  test('удалённый комментарий с ответами остаётся заглушкой, без ответов — пропадает', async ({ page }) => {
    await signIn(page)
    const comments = await openPost(page, titled)
    const withReplies = thread(comments, 'Первый уровень')
    await deleteComment(page, withReplies)

    const stub = comments
      .locator('[data-testid="comment"][aria-level="1"]')
      .filter({ hasText: 'Второй уровень' })
      .first()
    await expect(stub.locator(':scope > [data-testid="comment-deleted"]')).toHaveText('Комментарий удалён')
    await expect(stub.locator(':scope > .comment__head')).toHaveCount(0)
    await expect(stub.getByText('Второй уровень')).toBeVisible()
    await expect(comments.getByText('Комментарий удалён')).toHaveCount(1)

    const leafText = page.getByText('Ответ во второй ветке', { exact: true })
    const leaf = comments.locator('[data-testid="comment"][aria-level="2"]').filter({ has: leafText })
    await deleteComment(page, leaf)
    await expect(comments.getByText('Ответ во второй ветке', { exact: true })).toHaveCount(0)
    await expect(comments.getByText('Ещё ответ во второй ветке', { exact: true })).toBeVisible()
    await expect(comments.getByText('Комментарий удалён')).toHaveCount(1)
    await expectNoHorizontalScroll(page)
  })
})
