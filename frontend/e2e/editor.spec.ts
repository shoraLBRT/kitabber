import type { Locator, Page } from '@playwright/test'
import { box, expect, isTouch, press, test, waitForHydration } from './fixtures'

// Block editor core (E05-01, #35): the window on a desktop and the full screen on a phone (docs/spec.md section 5),
// undo and redo across blocks, and the document restored from its saved form (undo and redo render saved snapshots).

const editor = (page: Page) => page.getByTestId('block-editor')
const paragraphs = (page: Page) => page.locator('.ce-paragraph')

async function open(page: Page) {
  await page.goto('/write')
  await waitForHydration(page)
  await expect(editor(page)).toHaveAttribute('data-state', 'ready')
}

/** Waits until the editor has recorded `step` history steps (Editor.js reports changes in batches). */
async function atStep(page: Page, step: number) {
  await expect(editor(page)).toHaveAttribute('data-step', String(step))
}

async function undo(page: Page) {
  if (isTouch()) await press(page.getByTestId('editor-undo'))
  else await page.keyboard.press('ControlOrMeta+z')
}

async function redo(page: Page) {
  if (isTouch()) await press(page.getByTestId('editor-redo'))
  else await page.keyboard.press('ControlOrMeta+Shift+z')
}

async function texts(locator: Locator) {
  return locator.evaluateAll((elements) => elements.map((element) => element.textContent ?? ''))
}

test('opens as an 800×700 window on a desktop and on the whole screen on a phone', async ({ page }) => {
  await open(page)
  const panel = await box(page.getByTestId('editor-panel'))
  const viewport = page.viewportSize()!

  if (isTouch()) {
    expect(panel).toEqual({ x: 0, y: 0, width: viewport.width, height: viewport.height })
    // The bottom bar and the floating "Написать" are under the editor: the topmost element at their place is it.
    const covered = await page.evaluate(() =>
      ['write-button', 'tab-bar'].map((id) => {
        const element = document.querySelector(`[data-testid="${id}"]`)
        if (!element) return true
        const rect = element.getBoundingClientRect()
        const top = document.elementFromPoint(rect.x + rect.width / 2, rect.y + rect.height / 2)
        return !!top?.closest('[data-testid="editor-window"]')
      }),
    )
    expect(covered).toEqual([true, true])
    await expect(page.getByTestId('editor-expand')).toBeHidden()
  } else {
    expect(panel.width).toBe(800)
    expect(panel.height).toBe(700)
    expect(panel.x).toBe((viewport.width - 800) / 2)
    expect(panel.y).toBe((viewport.height - 700) / 2)

    await press(page.getByTestId('editor-expand'))
    expect(await box(page.getByTestId('editor-panel'))).toEqual({
      x: 0,
      y: 0,
      width: viewport.width,
      height: viewport.height,
    })
  }
  await expect(page.getByRole('dialog', { name: 'Новый пост' })).toBeVisible()
})

test('starts empty with the placeholder and nothing to undo', async ({ page }) => {
  await open(page)

  await expect(paragraphs(page)).toHaveCount(1)
  await expect(paragraphs(page).first()).toHaveAttribute('data-placeholder-active', 'Начните писать…')
  await expect(page.getByTestId('editor-undo')).toBeDisabled()
  await expect(page.getByTestId('editor-redo')).toBeDisabled()
})

test('undoes and redoes across blocks', async ({ page }) => {
  await open(page)
  await press(paragraphs(page).first())

  await page.keyboard.type('Первый абзац')
  await atStep(page, 1)
  await page.keyboard.press('Enter')
  await atStep(page, 2)
  await page.keyboard.type('Второй абзац')
  await atStep(page, 3)
  await expect(paragraphs(page)).toHaveCount(2)

  await undo(page)
  await atStep(page, 2)
  expect(await texts(paragraphs(page))).toEqual(['Первый абзац', ''])
  await undo(page)
  await atStep(page, 1)
  expect(await texts(paragraphs(page))).toEqual(['Первый абзац'])
  await undo(page)
  await atStep(page, 0)
  expect(await texts(paragraphs(page))).toEqual([''])
  await expect(page.getByTestId('editor-undo')).toBeDisabled()

  await redo(page)
  await redo(page)
  await redo(page)
  await atStep(page, 3)
  expect(await texts(paragraphs(page))).toEqual(['Первый абзац', 'Второй абзац'])
  await expect(page.getByTestId('editor-redo')).toBeDisabled()

  // The caret is back in the restored block: typing goes on there.
  await undo(page)
  await atStep(page, 2)
  await page.keyboard.type('снова')
  await atStep(page, 3)
  expect(await texts(paragraphs(page))).toEqual(['Первый абзац', 'снова'])
})

test('restores formatted text from the saved document without loss', async ({ page }) => {
  await open(page)
  await press(paragraphs(page).first())
  await page.keyboard.type('Обычный ')
  await page.keyboard.press('ControlOrMeta+b')
  await page.keyboard.type('жирный')
  await page.keyboard.press('ControlOrMeta+b')
  await page.keyboard.type(' текст')
  await atStep(page, 1)
  await page.keyboard.press('Enter')
  await atStep(page, 2)
  await page.keyboard.type('Второй')
  await atStep(page, 3)
  const written = await paragraphs(page).evaluateAll((elements) =>
    elements.map((element) => element.innerHTML),
  )
  expect(written[0]).toContain('<b>жирный</b>')

  // Undo to the start and redo to the end: each step is rendered from the document in the schema format.
  for (let step = 2; step >= 0; step--) {
    await undo(page)
    await atStep(page, step)
  }
  for (let step = 1; step <= 3; step++) {
    await redo(page)
    await atStep(page, step)
  }

  expect(
    await paragraphs(page).evaluateAll((elements) => elements.map((element) => element.innerHTML)),
  ).toEqual(written)
})

test('opens from "Написать" and closes with "✕" back to where it was opened from', async ({ page }) => {
  await page.goto('/')
  await waitForHydration(page)

  await press(isTouch() ? page.getByTestId('write-button') : page.locator('[data-nav="write"]'))
  await expect(editor(page)).toHaveAttribute('data-state', 'ready')
  await press(page.getByTestId('editor-close'))

  await expect(page).toHaveURL(/\/$/)
  await expect(page.getByTestId('editor-window')).toHaveCount(0)
})

test('closes on Escape', async ({ page }) => {
  await open(page)

  await page.keyboard.press('Escape')

  await expect(page).toHaveURL(/\/$/)
  await expect(page.getByTestId('editor-window')).toHaveCount(0)
})
