import type { Page } from '@playwright/test'
import { expect, isTouch, press, test, waitForHydration } from './fixtures'

// The quick post (E05-29, FR-EDITOR «Быстрый пост», ADR-0055) in both profiles against the fake API (e2e/fake-api.mjs):
// above the feed on a desktop and behind the floating «Написать» on a phone, a short post with a photo is published
// without the full editor, an empty one is not, «Развернуть» carries the text, the photo and the topic into the full
// editor where undo and redo work, and a guest is invited to sign in. The form's states are tested by
// QuickPostForm.spec, the document it writes by quick-post.spec.

async function signIn(page: Page): Promise<void> {
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

/** The form on screen: inline above the feed at `/new` on a desktop, the sheet of «Написать» on a phone. */
async function openQuickPost(page: Page) {
  await page.goto('/new')
  await waitForHydration(page)
  if (isTouch()) {
    // On a phone the form above the feed is hidden; «Написать» opens it.
    await expect(page.getByTestId('quick-post').first()).toBeHidden()
    await press(page.getByTestId('write-button'))
    await expect(page.getByRole('dialog', { name: 'Новый пост' })).toBeVisible()
  }
  const form = page.locator('[data-testid="quick-post"]:visible')
  await expect(form).toHaveCount(1)
  return form
}

/** A 40×30 JPEG drawn by the page's canvas. */
async function picture(page: Page) {
  const base64 = await page.evaluate(() => {
    const canvas = document.createElement('canvas')
    canvas.width = 40
    canvas.height = 30
    const context = canvas.getContext('2d')!
    context.fillStyle = '#36c'
    context.fillRect(0, 0, 40, 30)
    return canvas.toDataURL('image/jpeg').split(',')[1]!
  })
  return { name: 'photo.jpg', mimeType: 'image/jpeg', buffer: Buffer.from(base64, 'base64') }
}

test.describe('быстрый пост', () => {
  test('гость видит приглашение войти вместо формы', async ({ page }) => {
    const form = await openQuickPost(page)
    await expect(form.getByTestId('quick-post-guest')).toContainText('Войдите, чтобы написать пост.')
    await expect(form.getByTestId('quick-post-text')).toHaveCount(0)
    await press(form.getByTestId('quick-post-sign-in'))
    await expect(page.getByRole('dialog', { name: 'Вход' })).toBeVisible()
  })

  test.describe('вошедший пользователь', () => {
    test.beforeEach(async ({ page }) => {
      await signIn(page)
    })

    test('пустой пост не публикуется, короткий публикуется без полного редактора', async ({ page }) => {
      const form = await openQuickPost(page)
      const publish = form.getByTestId('quick-post-publish')
      await expect(publish).toBeDisabled()
      await form.getByTestId('quick-post-text').fill('   ')
      await expect(publish).toBeDisabled()

      await form.getByTestId('quick-post-text').fill('Быстрый пост\nв две строки')
      await expect(publish).toBeEnabled()
      await press(publish)

      await page.waitForURL(/\/u\/anna\/[0-9a-f-]{36}$/)
      const post = page.getByTestId('post')
      await expect(post).toContainText('Быстрый пост')
      await expect(post).toContainText('в две строки')
      await expect(post.getByRole('heading', { level: 1 })).toHaveCount(0)
      await expect(page.getByTestId('editor-window')).toHaveCount(0)
    })

    test('фото прикрепляется и публикуется вместе с текстом', async ({ page }) => {
      const form = await openQuickPost(page)
      let release!: () => void
      const held = new Promise<void>((resolve) => (release = resolve))
      await page.route('**/bff/media', async (route) => {
        await held
        await route.continue()
      })

      await form.getByTestId('quick-post-text').fill('Закат')
      const chooser = page.waitForEvent('filechooser')
      await press(form.getByTestId('quick-post-attach'))
      await (await chooser).setFiles(await picture(page))
      const photo = form.getByTestId('quick-post-photo')
      await expect(photo).toHaveAttribute('data-state', 'uploading')
      await expect(form.getByTestId('quick-post-publish')).toBeDisabled()
      release()
      await expect(photo).toHaveAttribute('data-state', 'ready')

      await press(form.getByTestId('quick-post-publish'))
      await page.waitForURL(/\/u\/anna\/[0-9a-f-]{36}$/)
      await expect(page.getByTestId('post')).toContainText('Закат')
      const shown = page.getByTestId('post').getByTestId('post-block-image').locator('img')
      await expect(shown).toHaveAttribute('width', '40')
      await expect(shown).toHaveAttribute('height', '30')
    })

    test('«Развернуть» переносит текст, фото и тему в полный редактор, отмена и повтор там работают', async ({
      page,
    }) => {
      const form = await openQuickPost(page)
      await form.getByTestId('quick-post-text').fill('Мысль, которая выросла')
      await press(form.getByTestId('quick-post-topic').getByRole('button'))
      await press(page.getByRole('menuitem', { name: 'Книги' }))
      const chooser = page.waitForEvent('filechooser')
      await press(form.getByTestId('quick-post-attach'))
      await (await chooser).setFiles(await picture(page))
      await expect(form.getByTestId('quick-post-photo')).toHaveAttribute('data-state', 'ready')

      await press(form.getByTestId('quick-post-expand'))
      await page.waitForURL(/\/write$/)
      const editor = page.getByTestId('block-editor')
      await expect(editor).toHaveAttribute('data-state', 'ready')
      const paragraph = page.locator('.ce-paragraph').first()
      await expect(paragraph).toHaveText('Мысль, которая выросла')
      const image = page.getByTestId('image-block')
      await expect(image).toHaveAttribute('data-state', 'ready')
      expect(await image.locator('img').evaluate((element: HTMLImageElement) => element.naturalWidth)).toBe(
        40,
      )
      await expect(page.getByTestId('editor-topic')).toContainText('Книги')

      await press(paragraph)
      await page.keyboard.press('End')
      await page.keyboard.type(' в статью')
      await expect(paragraph).toHaveText('Мысль, которая выросла в статью')
      await expect(page.getByTestId('editor-undo')).toBeEnabled()
      await press(page.getByTestId('editor-undo'))
      await expect(paragraph).toHaveText('Мысль, которая выросла')
      await expect(image).toHaveAttribute('data-state', 'ready')
      await press(page.getByTestId('editor-redo'))
      await expect(page.locator('.ce-paragraph').first()).toHaveText('Мысль, которая выросла в статью')

      await press(page.getByTestId('editor-publish'))
      await page.waitForURL(/\/books\/[0-9a-f-]{36}$/)
      await expect(page.getByTestId('post')).toContainText('Мысль, которая выросла в статью')
      await expect(page.getByTestId('post').getByTestId('post-block-image')).toHaveCount(1)
    })
  })
})
