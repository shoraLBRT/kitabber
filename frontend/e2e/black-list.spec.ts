import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'
import type { Locator, Page } from '@playwright/test'

// The black list (E11-06) in both profiles, against the fake API (e2e/fake-api.mjs), which keeps the list per client
// address with Борис blocked, and in which Борис blocked the person back. «Приватность и блокировки» lists and
// unblocks; «Заблокировать» on Борис's blog, in the «…» menu of his post and of his comment asks first and blocks; the
// comment form under his post says he blocked the person. That a blocked account's posts leave the feeds, its comments
// come folded and it may not reply is tested by the backend (SharedFeedExclusionsApiTests, CommentBlockApiTests).
const borisPost = '/books/0195f3a0-0000-7000-8000-0000000000c3-knigi-oseni'
const annasPost = '/u/anna/0195f3a0-0000-7000-8000-0000000000c1-privet-mir'

function newClient(): string {
  const octet = () => Math.floor(Math.random() * 254) + 1
  return `10.${octet()}.${octet()}.${octet()}`
}

async function signIn(page: Page): Promise<void> {
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': newClient() })
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

async function openPrivacySettings(page: Page): Promise<void> {
  await signIn(page)
  await page.goto('/settings/privacy')
  await waitForHydration(page)
}

/** Opens a «…» menu and gives its items: the menu items on a wide screen, the buttons of the sheet on a phone. */
async function openMenu(page: Page, trigger: Locator, name: string, sheet: string) {
  await press(trigger)
  return isTouch()
    ? page.getByRole('dialog', { name }).getByTestId(sheet).getByRole('button')
    : page.getByRole('menu', { name }).getByRole('menuitem')
}

async function confirmBlock(page: Page): Promise<void> {
  const dialog = page.getByRole('dialog', { name: 'Заблокировать Борис?' })
  await expect(dialog).toBeVisible()
  await expect(dialog).toContainText('комментировать ваши посты он не сможет')
  await press(dialog.getByRole('button', { name: 'Заблокировать' }))
  await expect(dialog).toBeHidden()
  await expect(page.getByText('Борис в чёрном списке.')).toBeVisible()
}

test.describe('кнопки «Заблокировать»', () => {
  test.use({ actionTimeout: 10_000 })

  test('на чужом блоге: «Разблокировать» снимает блокировку, «Заблокировать» спрашивает и блокирует', async ({
    page,
  }) => {
    await signIn(page)
    await page.goto('/u/boris')
    await waitForHydration(page)
    const button = page.getByTestId('blog-block-button')
    await expect(button).toHaveText('Разблокировать')
    await expect(page.getByTestId('blog-blocked')).toContainText('Вы заблокировали этого человека')
    await expectNoHorizontalScroll(page)

    await press(button)
    await expect(page.getByText('Борис больше не в чёрном списке.')).toBeVisible()
    await expect(button).toHaveText('Заблокировать')
    await expect(page.getByTestId('blog-blocked')).toHaveCount(0)

    await press(button)
    await confirmBlock(page)
    await expect(button).toHaveText('Разблокировать')

    // The list is the account's: the settings show Борис again, and the own blog has no such button.
    await page.goto('/settings/privacy')
    await waitForHydration(page)
    await expect(page.getByTestId('blocked-account')).toHaveCount(1)
    await page.goto('/u/anna')
    await waitForHydration(page)
    await expect(page.getByTestId('blog-block-button')).toHaveCount(0)
  })

  test('гость не видит «Заблокировать» на блоге', async ({ page }) => {
    await page.goto('/u/boris')
    await waitForHydration(page)
    await expect(page.getByRole('heading', { level: 1, name: 'Борис' })).toBeVisible()
    await expect(page.getByTestId('blog-block-button')).toHaveCount(0)
  })

  test('в меню чужого поста: «Заблокировать автора» спрашивает и блокирует', async ({ page }) => {
    await signIn(page)
    await page.goto(borisPost)
    await waitForHydration(page)
    const trigger = page.getByTestId('post-header').getByRole('button', { name: 'Меню поста' })
    const items = await openMenu(page, trigger, 'Меню поста', 'post-menu-sheet')
    await expect(items).toHaveText(['Скопировать ссылку', 'Заблокировать автора'])
    await press(items.filter({ hasText: 'Заблокировать автора' }))
    await confirmBlock(page)
  })

  test('в меню чужого комментария: после блокировки его комментарии свёрнуты', async ({ page }) => {
    await signIn(page)
    await page.goto(annasPost)
    await waitForHydration(page)
    const comments = page.getByTestId('comments')
    const found = comments
      .locator('[data-testid="comment"][aria-level="1"]')
      .filter({ hasText: 'Вторая ветка' })
      .first()
    // By its id: once folded, the comment no longer shows its text.
    const foreign = comments.locator(`[data-comment-id="${await found.getAttribute('data-comment-id')}"]`)
    const trigger = foreign
      .locator(':scope > .comment__head')
      .getByRole('button', { name: 'Меню комментария' })
    const items = await openMenu(page, trigger, 'Меню комментария', 'comment-menu-sheet')
    await expect(items).toHaveText(['Скопировать ссылку', 'В закладки', 'Заблокировать автора'])
    await press(items.last())
    await confirmBlock(page)
    await expect(foreign.locator(':scope > [data-testid="comment-folded"]')).toContainText(
      'Комментарий скрыт: вы заблокировали автора.',
    )
    await expectNoHorizontalScroll(page)
  })

  test('форма комментария под постом того, кто заблокировал, говорит об этом', async ({ page }) => {
    await signIn(page)
    await page.goto(borisPost)
    await waitForHydration(page)
    const comments = page.getByTestId('comments')
    await press(comments.getByTestId('comment-form-open').first())
    const form = comments.getByTestId('comment-form').first()
    await form.getByRole('textbox').fill('Хочу ответить')
    await press(form.getByRole('button', { name: 'Отправить' }))
    await expect(form.getByTestId('comment-form-blocked')).toHaveText(
      'Автор заблокировал вас: отвечать ему и комментировать его посты нельзя.',
    )
    await expect(form.getByRole('textbox')).toHaveValue('Хочу ответить')
    await expect(form.getByRole('button', { name: 'Отправить' })).toBeDisabled()
  })
})

test.describe('чёрный список', () => {
  test('видно заблокированных, «Разблокировать» убирает из списка, после чего список пуст', async ({
    page,
  }) => {
    await openPrivacySettings(page)
    await expect(page.getByRole('heading', { level: 1, name: 'Приватность и блокировки' })).toBeVisible()
    const section = page.getByTestId('block-list')
    const rows = section.getByTestId('blocked-account')
    await expect(rows).toHaveCount(1)
    await expect(rows.first()).toContainText('Борис')
    await expect(rows.first()).toContainText('В списке с 25 ноября 2026')
    await expectNoHorizontalScroll(page)

    await press(section.getByRole('button', { name: 'Разблокировать Борис' }))
    await expect(section.getByRole('status')).toHaveText('Борис больше не в чёрном списке.')
    await expect(rows).toHaveCount(0)
    await expect(section.getByTestId('block-list-empty')).toBeVisible()

    // The list is the account's: it stays empty after a reload.
    await page.reload()
    await waitForHydration(page)
    await expect(page.getByTestId('block-list-empty')).toBeVisible()
  })
})

test.describe('чёрный список: сбой чтения', () => {
  // The failed read is the point of the test: the browser logs its 502.
  test.use({ missingResources: ['/bff/blocks'] })

  test('пока список читается — скелетон, при сбое — «Повторить»', async ({ page }) => {
    let fail = true
    let release: () => void = () => {}
    const held = new Promise<void>((resolve) => (release = resolve))
    await page.route('**/bff/blocks', async (route) => {
      if (fail) {
        fail = false
        await route.fulfill({ status: 502, body: 'null', contentType: 'application/json' })
        return
      }
      await held
      await route.continue()
    })
    await openPrivacySettings(page)
    const section = page.getByTestId('block-list')
    await expect(section.getByRole('alert')).toHaveText('Не удалось загрузить чёрный список.')

    await press(section.getByRole('button', { name: 'Повторить' }))
    await expect(section.getByTestId('block-list-loading')).toBeVisible()
    release()
    await expect(section.getByTestId('blocked-account')).toHaveCount(1)
  })
})
