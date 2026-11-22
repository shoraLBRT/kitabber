import type { Page } from '@playwright/test'
import { expect, isTouch, press, test, waitForHydration } from './fixtures'

// The «Фото» block (E05-06, ADR-0049) in both profiles against the fake API (e2e/fake-api.mjs, which takes JPEG and
// refuses anything else as the media service refuses an unknown format): the picture uploads with an indicator and
// shows in the block, a failed upload leaves the rest of the post as it was, the caption has a length limit, and the
// published post shows the picture with its sources and the caption. On a phone the panel above the keyboard adds a
// picture from the gallery. The pictures are drawn by the browser's canvas: there are no image files in the app.

async function signIn(page: Page): Promise<void> {
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

async function openEditor(page: Page): Promise<void> {
  await page.goto('/write')
  await waitForHydration(page)
  await expect(page.getByTestId('block-editor')).toHaveAttribute('data-state', 'ready')
}

/** A 40×30 picture in `type`, drawn by the page's canvas. */
async function picture(page: Page, type: 'image/jpeg' | 'image/png') {
  const base64 = await page.evaluate((format) => {
    const canvas = document.createElement('canvas')
    canvas.width = 40
    canvas.height = 30
    const context = canvas.getContext('2d')!
    context.fillStyle = '#c33'
    context.fillRect(0, 0, 40, 30)
    return canvas.toDataURL(format).split(',')[1]!
  }, type)
  return {
    name: type === 'image/jpeg' ? 'photo.jpg' : 'photo.png',
    mimeType: type,
    buffer: Buffer.from(base64, 'base64'),
  }
}

/** Writes a line of text, adds a «Фото» block from the block menu and gives the picker that opens `file`. */
async function addPhoto(page: Page, file: Awaited<ReturnType<typeof picture>>) {
  await press(page.locator('.ce-paragraph').first())
  await page.keyboard.type('Текст до фото')
  await page.keyboard.press('Enter')
  const chooser = page.waitForEvent('filechooser')
  await press(page.locator('.ce-toolbar__plus'))
  await press(page.locator('.ce-popover-item__title:visible').filter({ hasText: /^Фото$/ }))
  await (await chooser).setFiles(file)
}

const block = (page: Page) => page.getByTestId('image-block')

test.describe('блок «Фото»', () => {
  test.beforeEach(async ({ page }) => {
    await signIn(page)
    await openEditor(page)
  })

  test('фото загружается с индикатором, появляется в блоке и публикуется с подписью', async ({ page }) => {
    // The upload waits until the indicator has been seen.
    let release!: () => void
    const held = new Promise<void>((resolve) => (release = resolve))
    await page.route('**/bff/media', async (route) => {
      await held
      await route.continue()
    })

    await addPhoto(page, await picture(page, 'image/jpeg'))
    await expect(block(page)).toHaveAttribute('data-state', 'uploading')
    await expect(block(page).getByRole('progressbar', { name: 'Загружаем фото…' })).toBeVisible()
    await expect(page.getByTestId('editor-publish')).toBeEnabled()
    release()

    await expect(block(page)).toHaveAttribute('data-state', 'ready')
    const preview = block(page).locator('img')
    await expect(preview).toHaveAttribute('width', '40')
    await expect(preview).toHaveAttribute('height', '30')
    expect(await preview.evaluate((image: HTMLImageElement) => image.naturalWidth)).toBe(40)

    await press(block(page).locator('.ce-image__caption'))
    await page.keyboard.type('Закат над морем')
    await press(page.getByTestId('editor-publish'))

    await page.waitForURL(/\/u\/anna\/[0-9a-f-]{36}$/)
    const figure = page.getByTestId('post').getByTestId('post-block-image')
    await expect(figure.locator('figcaption')).toHaveText('Закат над морем')
    const shown = figure.locator('img')
    await expect(shown).toHaveAttribute('width', '40')
    await expect(shown).toHaveAttribute('height', '30')
    await expect(shown).toHaveAttribute('srcset', /-320\.webp 320w, .*-1920\.webp 1920w$/)
    await expect(page.getByTestId('post')).toContainText('Текст до фото')
  })

  test('ошибка загрузки не ломает документ: остальной текст публикуется', async ({ page }) => {
    await addPhoto(page, await picture(page, 'image/png'))

    await expect(block(page)).toHaveAttribute('data-state', 'error')
    await expect(block(page).getByRole('alert')).toHaveText(
      'Такой формат не подходит: нужны JPEG, PNG, GIF или WebP.',
    )
    await expect(block(page).getByRole('button', { name: 'Повторить' })).toBeVisible()
    await expect(block(page).getByRole('button', { name: 'Выбрать другой файл' })).toBeVisible()
    await expect(page.locator('.ce-paragraph').first()).toHaveText('Текст до фото')

    await press(page.getByTestId('editor-publish'))
    await page.waitForURL(/\/u\/anna\/[0-9a-f-]{36}$/)
    await expect(page.getByTestId('post')).toContainText('Текст до фото')
    await expect(page.getByTestId('post-block-image')).toHaveCount(0)
    await expect(page.getByTestId('post-block-unknown')).toHaveCount(0)
  })

  test('подпись ограничена по длине', async ({ page }) => {
    await addPhoto(page, await picture(page, 'image/jpeg'))
    await expect(block(page)).toHaveAttribute('data-state', 'ready')

    const caption = block(page).locator('.ce-image__caption')
    await press(caption)
    await page.keyboard.insertText('а'.repeat(295))
    await page.keyboard.type('бббббввввв')
    await expect(caption).toHaveText('а'.repeat(295) + 'ббббб')
  })

  test('на телефоне фото из галереи добавляется кнопкой панели', async ({ page }) => {
    test.skip(!isTouch(), 'the panel above the keyboard is only on a phone')
    await press(page.locator('.ce-paragraph').first())
    await page.keyboard.type('Текст')

    await page.getByTestId('keyboard-bar-gallery-input').setInputFiles(await picture(page, 'image/jpeg'))
    await expect(block(page)).toHaveAttribute('data-state', 'ready')
    await expect(page.locator('.ce-paragraph').first()).toHaveText('Текст')
    await expect(page.getByTestId('keyboard-bar-camera-input')).toHaveAttribute('capture', 'environment')
  })
})
