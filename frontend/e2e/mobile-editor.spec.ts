import type { Page } from '@playwright/test'
import { box, expect, isTouch, press, test, waitForHydration } from './fixtures'

// The editor on a phone (E05-10, #42, ADR-0047): the panel above the keyboard with «+», «Ж» and «К», which keeps the
// focus in the text, and the screen that shrinks to what the keyboard leaves. Chromium opens no on-screen keyboard,
// so the test shrinks the visual viewport as iOS Safari and Android Chrome do when it opens. On a desktop there is no
// such panel: the block toolbar of Editor.js and the floating panel do the same there (e2e/editor.spec.ts).

const editor = (page: Page) => page.getByTestId('block-editor')
const bar = (page: Page) => page.getByTestId('editor-keyboard-bar')
const paragraphs = (page: Page) => page.locator('.ce-paragraph')

async function open(page: Page) {
  await page.goto('/write')
  await waitForHydration(page)
  await expect(editor(page)).toHaveAttribute('data-state', 'ready')
}

/** Opens (height) or closes (null) the fake keyboard: the visual viewport is that tall, the window is not. */
async function keyboard(page: Page, height: number | null) {
  await page.evaluate((visible) => {
    const viewport = window.visualViewport!
    if (visible === null) delete (viewport as unknown as Record<string, unknown>).height
    else Object.defineProperty(viewport, 'height', { configurable: true, get: () => visible })
    viewport.dispatchEvent(new Event('resize'))
  }, height)
}

/** Whether the focus is in a block of the editor, so the keyboard of a phone stays open. */
const focusInText = (page: Page) =>
  page.evaluate(() => !!document.activeElement?.closest('[data-testid="block-editor-holder"]'))

async function typeAndSelectLastWord(page: Page, text: string) {
  await press(paragraphs(page).first())
  await page.keyboard.type(text)
  const word = text.split(' ').at(-1)!.length
  await paragraphs(page)
    .first()
    .evaluate((element, length) => {
      const node = element.firstChild!
      const range = document.createRange()
      range.setStart(node, node.textContent!.length - length)
      range.setEnd(node, node.textContent!.length)
      window.getSelection()!.removeAllRanges()
      window.getSelection()!.addRange(range)
    }, word)
}

test('keeps the panel right above the keyboard on a phone and has none on a desktop', async ({ page }) => {
  await open(page)
  if (!isTouch()) {
    await expect(bar(page)).toHaveCount(0)
    await expect(page.getByTestId('editor-publish')).toBeVisible()
    return
  }

  const viewport = page.viewportSize()!
  // No keyboard: the panel is over «Опубликовать» at the bottom of the screen, buttons not smaller than 44 px.
  await expect(bar(page)).toBeVisible()
  const publish = await box(page.getByTestId('editor-publish'))
  expect((await box(bar(page))).y + (await box(bar(page))).height).toBeLessThanOrEqual(publish.y + 1)
  for (const id of ['keyboard-bar-add', 'keyboard-bar-bold', 'keyboard-bar-italic']) {
    const button = await box(page.getByTestId(id))
    expect(Math.min(button.width, button.height)).toBeGreaterThanOrEqual(44)
  }

  await press(paragraphs(page).first())
  await page.keyboard.type('Текст')
  const visible = viewport.height - 336
  await keyboard(page, visible)

  await expect(page.getByTestId('editor-window')).toHaveAttribute('data-keyboard', 'open')
  await expect(page.getByTestId('editor-publish')).toBeHidden()
  // The panel's bottom edge is the keyboard's top edge, and nothing of the screen is under the keyboard.
  await expect
    .poll(async () => Math.round((await box(bar(page))).y + (await box(bar(page))).height))
    .toBe(visible)
  const panel = await box(page.getByTestId('editor-panel'))
  expect(Math.round(panel.y + panel.height)).toBe(visible)
  expect(await focusInText(page)).toBe(true)

  await keyboard(page, null)
  await expect(page.getByTestId('editor-window')).toHaveAttribute('data-keyboard', 'closed')
  await expect(page.getByTestId('editor-publish')).toBeVisible()
})

test('formats with «Ж» and «К» and opens the block menu with «+» without leaving the text', async ({
  page,
}) => {
  await open(page)
  if (!isTouch()) {
    await expect(bar(page)).toHaveCount(0)
    return
  }

  await typeAndSelectLastWord(page, 'Первое слово')
  await keyboard(page, page.viewportSize()!.height - 336)

  await press(page.getByTestId('keyboard-bar-bold'))
  await expect.poll(() => paragraphs(page).first().innerHTML()).toBe('Первое <b>слово</b>')
  expect(await focusInText(page)).toBe(true)
  await expect(page.getByTestId('keyboard-bar-bold')).toHaveAttribute('aria-pressed', 'true')

  await press(page.getByTestId('keyboard-bar-italic'))
  await expect.poll(() => paragraphs(page).first().innerHTML()).toBe('Первое <b><i>слово</i></b>')
  expect(await focusInText(page)).toBe(true)

  // The caret goes to the end and «+» opens the block menu as the bottom sheet; the text keeps the focus.
  await page.keyboard.press('End')
  await press(page.getByTestId('keyboard-bar-add'))
  await expect(page.locator('.ce-toolbox .ce-popover--opened > .ce-popover__container')).toBeVisible()
  expect(await focusInText(page)).toBe(true)
  await press(page.locator('.ce-toolbox .ce-popover-item__title:visible').filter({ hasText: /^Заголовок$/ }))
  await expect(page.locator('.block-editor__holder h2.ce-header')).toHaveCount(1)
  expect(await focusInText(page)).toBe(true)
})
