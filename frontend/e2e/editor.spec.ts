import type { Locator, Page } from '@playwright/test'
import { googleDocsListHtml, webHtml, wordHtml, wordListHtml } from '../tests/editor/paste-samples'
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

// Text block with formatting and the floating panel (E05-02, #37): the panel above the selection, checked links with
// the rel of the server, and HTML pasted from Word and web pages cleaned to the allowed markup.

const toolbar = (page: Page) => page.locator('.ce-inline-toolbar')
const tool = (page: Page, name: string) => toolbar(page).locator(`[data-item-name="${name}"]`)
const linkRel = 'nofollow ugc noopener noreferrer'

/**
 * Writes a first paragraph and then `text` in a second one, and selects its last `selected` characters with the
 * keyboard: three history steps. The selection is not on the first line, which may have no room above it.
 */
async function typeAndSelect(page: Page, text: string, selected: number) {
  await press(paragraphs(page).first())
  await page.keyboard.type('Вступление')
  await atStep(page, 1)
  await page.keyboard.press('Enter')
  await atStep(page, 2)
  await page.keyboard.type(text)
  await atStep(page, 3)
  for (let i = 0; i < selected; i++) await page.keyboard.press('Shift+ArrowLeft')
  await expect(tool(page, 'bold')).toBeVisible()
}

/** The selection and the panel of buttons on the screen. */
async function placement(page: Page) {
  return page.evaluate(() => {
    const selection = window.getSelection()!.getRangeAt(0).getBoundingClientRect()
    const panel = document
      .querySelector('.ce-inline-toolbar .ce-popover--inline > .ce-popover__container')!
      .getBoundingClientRect()
    return {
      selection: { top: selection.top, bottom: selection.bottom },
      panel: { top: panel.top, bottom: panel.bottom },
    }
  })
}

async function html(page: Page, index = 0) {
  return paragraphs(page)
    .nth(index)
    .evaluate((element) => element.innerHTML)
}

async function htmls(page: Page) {
  return paragraphs(page).evaluateAll((elements) => elements.map((element) => element.innerHTML))
}

test('shows the formatting panel above the selection and formats with it', async ({ page }) => {
  await open(page)
  await typeAndSelect(page, 'Первое слово', 5)

  const { selection, panel } = await placement(page)
  expect(panel.bottom).toBeLessThanOrEqual(selection.top)
  // In sight, under the window's header: the topmost element at the panel's middle is the panel.
  const visible = await page.evaluate(({ top, bottom }) => {
    const container = document.querySelector(
      '.ce-inline-toolbar .ce-popover--inline > .ce-popover__container',
    )!
    const rect = container.getBoundingClientRect()
    const hit = document.elementFromPoint(rect.left + rect.width / 2, (top + bottom) / 2)
    return !!hit && container.contains(hit)
  }, panel)
  expect(visible).toBe(true)
  await expect(toolbar(page)).toHaveAttribute('data-placement', 'top')
  for (const name of ['bold', 'italic', 'marker', 'link']) await expect(tool(page, name)).toBeVisible()

  await press(tool(page, 'bold'))
  await expect.poll(() => html(page, 1)).toBe('Первое <b>слово</b>')
  await press(tool(page, 'italic'))
  await expect.poll(() => html(page, 1)).toBe('Первое <b><i>слово</i></b>')
  await press(tool(page, 'marker'))
  await expect.poll(() => html(page, 1)).toBe('Первое <b><i><mark>слово</mark></i></b>')
})

test('checks the link address and gives the link rel="nofollow ugc"', async ({ page }) => {
  await open(page)
  await typeAndSelect(page, 'Читайте здесь', 5)

  await press(tool(page, 'link'))
  const input = page.getByTestId('link-input')
  await expect(input).toBeVisible()
  await input.fill('javascript:alert(1)')
  await input.press('Enter')
  await expect(page.getByTestId('link-error')).toHaveText(
    'Это не ссылка. Подойдут адреса http://, https:// и mailto:',
  )
  expect(await html(page, 1)).not.toContain('<a')

  await input.fill('example.test/page')
  await expect(page.getByTestId('link-error')).toBeHidden()
  await input.press('Enter')
  const linked = `Читайте <a href="https://example.test/page" rel="${linkRel}">здесь</a>`
  await expect.poll(() => html(page, 1)).toBe(linked)

  // The document keeps the link as the server does: rendered back from it by undo and redo, it is the same.
  await atStep(page, 4)
  await undo(page)
  await atStep(page, 3)
  await redo(page)
  await atStep(page, 4)
  expect(await html(page, 1)).toBe(linked)
})

/** Pastes `data` (and its plain text) into the first paragraph as the browser does from the clipboard. */
async function paste(page: Page, data: string) {
  await press(paragraphs(page).first())
  await paragraphs(page)
    .first()
    .evaluate((element, source) => {
      const transfer = new DataTransfer()
      transfer.setData('text/html', source)
      transfer.setData(
        'text/plain',
        new DOMParser().parseFromString(source, 'text/html').body.textContent ?? '',
      )
      element.dispatchEvent(
        new ClipboardEvent('paste', { clipboardData: transfer, bubbles: true, cancelable: true }),
      )
    }, data)
}

test('cleans text pasted from Word to the allowed markup', async ({ page }) => {
  await open(page)
  await paste(page, wordHtml)

  await expect(paragraphs(page)).toHaveCount(3)
  await expect
    .poll(() => htmls(page))
    .toEqual([
      '<b>Заголовок отчёта</b>',
      'Обычный текст, <i>курсив</i> и <b>жирный стилем</b>, <mark>выделение</mark>.',
      `Ссылка <a href="https://example.test/report?id=1&amp;x=2" rel="${linkRel}">на отчёт</a> и опасная.`,
    ])
})

test('cleans text pasted from a web page to the allowed markup', async ({ page }) => {
  await open(page)
  await paste(page, webHtml)

  await expect(paragraphs(page)).toHaveCount(2)
  await expect
    .poll(() => htmls(page))
    .toEqual([
      'Заголовок <i>статьи</i>',
      `Текст с <b>важным</b> и <a href="https://example.test/" rel="${linkRel}">ссылкой</a>, относительной и цветом.`,
    ])
  await expect(page.locator('.cdx-list__item')).toHaveCount(2)
  await expect
    .poll(() => page.locator('.cdx-list__item').evaluateAll((items) => items.map((item) => item.innerHTML)))
    .toEqual(['Пункт один', 'Пункт <mark>два</mark>'])
  await expect(
    page.locator('.codex-editor script, .codex-editor iframe, .codex-editor [onclick]'),
  ).toHaveCount(0)
})

/** The blocks of the editor in order: a text block by its HTML, a list by its kind and its items with their levels. */
async function blocks(page: Page) {
  return page.locator('.ce-block').evaluateAll((elements) =>
    elements.map((element) => {
      const list = element.querySelector<HTMLElement>('.cdx-list')
      if (!list) return element.querySelector('.ce-paragraph')?.innerHTML ?? ''
      const items = [...list.querySelectorAll<HTMLElement>('.cdx-list__item')]
      return `${list.dataset.style}: ${items.map((item) => `${item.dataset.level}:${item.innerHTML}`).join(' | ')}`
    }),
  )
}

test('turns lists pasted from Word into list blocks', async ({ page }) => {
  await open(page)
  await paste(page, wordListHtml)

  await expect
    .poll(() => blocks(page))
    .toEqual([
      'unordered: 0:Первый <b>пункт</b> | 1:Вложенный | 0:Второй',
      'Между списками',
      'ordered: 0:Шаг один | 0:Шаг <i>два</i>',
    ])
})

test('turns a list pasted from Google Docs into a list block', async ({ page }) => {
  await open(page)
  await paste(page, googleDocsListHtml)

  await expect.poll(() => blocks(page)).toEqual(['ordered: 0:Раз | 1:<b>Вложенный</b> | 0:Два'])
})

// Heading blocks (E05-03, #38): «Заголовок» (H2) and «Подзаголовок» (H3), converted from and to text by the
// «Превратить в» menu of the formatting panel, with an anchor from the block id (ADR-0031).

const headers = (page: Page) => page.locator('.ce-header')

/** Selects the last `count` characters before the caret and converts the block with the panel's menu. */
async function convert(page: Page, count: number, to: string) {
  for (let i = 0; i < count; i++) await page.keyboard.press('Shift+ArrowLeft')
  await press(tool(page, 'convert-to'))
  await press(page.locator('.ce-popover-item__title:visible').filter({ hasText: new RegExp(`^${to}$`) }))
}

async function anchor(page: Page) {
  const heading = headers(page).first()
  const id = await heading.evaluate((element) => element.closest<HTMLElement>('.ce-block')!.dataset.id!)
  await expect(heading).toHaveAttribute('id', `h-${id}`)
  return `h-${id}`
}

async function step(page: Page) {
  return Number(await editor(page).getAttribute('data-step'))
}

test('converts text to a heading and a subheading and back keeping its content', async ({ page }) => {
  await open(page)
  await press(paragraphs(page).first())
  await page.keyboard.type('Глава ')
  await page.keyboard.press('ControlOrMeta+b')
  await page.keyboard.type('первая')
  await page.keyboard.press('ControlOrMeta+b')
  await atStep(page, 1)

  await convert(page, 12, 'Заголовок')
  await expect(page.locator('h2.ce-header')).toHaveJSProperty('innerHTML', 'Глава <b>первая</b>')
  await expect(paragraphs(page)).toHaveCount(0)

  await convert(page, 12, 'Подзаголовок')
  await expect(page.locator('h3.ce-header')).toHaveJSProperty('innerHTML', 'Глава <b>первая</b>')
  await expect(headers(page)).toHaveCount(1)

  await convert(page, 12, 'Текст')
  await expect(headers(page)).toHaveCount(0)
  // The text and its inline markup stay: the text block cleans converted text by the tags of its tools (ADR-0044).
  await expect.poll(() => html(page)).toBe('Глава <b>первая</b>')
})

test('keeps italic, the marker and a link through text → heading → text', async ({ page }) => {
  await open(page)
  await typeAndSelect(page, 'здесь сейчас', 12)
  await page.keyboard.press('Home')
  for (let i = 0; i < 5; i++) await page.keyboard.press('Shift+ArrowRight')
  await press(tool(page, 'italic'))
  await expect.poll(() => html(page, 1)).toContain('<i>')
  await press(tool(page, 'marker'))
  await expect.poll(() => html(page, 1)).toContain('<mark>')
  await press(tool(page, 'link'))
  const input = page.getByTestId('link-input')
  await expect(input).toBeVisible()
  await input.fill('example.test/page')
  await input.press('Enter')
  await expect.poll(() => html(page, 1)).toContain(`rel="${linkRel}"`)
  await expect.poll(() => step(page)).toBeGreaterThan(3)
  // The browser decides how the tags nest and may leave a `style` on the marker; conversion cleans it off.
  const formatted = (await html(page, 1)).replace(/ style="[^"]*"/g, '')
  for (const tag of ['<i>', '<mark>', `<a href="https://example.test/page" rel="${linkRel}">`, '> сейчас'])
    expect(formatted).toContain(tag)

  // The plain word at the end is selected: a selection in a link opens the link field instead of the menu.
  await press(paragraphs(page).nth(1))
  await page.keyboard.press('End')
  await convert(page, 6, 'Заголовок')
  await expect(page.locator('h2.ce-header')).toHaveJSProperty('innerHTML', formatted)
  await press(headers(page).first())
  await page.keyboard.press('End')
  await convert(page, 6, 'Текст')
  await expect(headers(page)).toHaveCount(0)
  await expect.poll(() => html(page, 1)).toBe(formatted)
})

test('gives a heading an anchor that stays while its text changes and after undo and redo', async ({
  page,
}) => {
  await open(page)
  await press(paragraphs(page).first())
  await page.keyboard.type('Введение')
  await atStep(page, 1)
  await convert(page, 8, 'Заголовок')
  await expect(headers(page)).toHaveCount(1)
  await expect.poll(() => step(page)).toBeGreaterThan(1)
  const id = await anchor(page)

  const converted = await step(page)
  await page.keyboard.press('End')
  await page.keyboard.type(' в тему')
  await expect.poll(() => step(page)).toBeGreaterThan(converted)
  await expect(headers(page).first()).toHaveText('Введение в тему')
  expect(await anchor(page)).toBe(id)

  const typed = await step(page)
  await undo(page)
  await atStep(page, typed - 1)
  await expect(headers(page)).toHaveCount(1)
  await expect(headers(page).first()).not.toHaveText('Введение в тему')
  expect(await anchor(page)).toBe(id)
  await redo(page)
  await atStep(page, typed)
  await expect(headers(page).first()).toHaveText('Введение в тему')
  expect(await anchor(page)).toBe(id)
})

// List blocks (E05-04, #39): bulleted and numbered, one level of nesting by Tab and Shift+Tab, Enter adds an item and
// an empty item ends the list, the style switches without losing the items or their levels (ADR-0033).

const lists = (page: Page) => page.locator('.cdx-list')
const items = (page: Page) => page.locator('.cdx-list__item')

async function levels(page: Page) {
  return items(page).evaluateAll((elements) =>
    elements.map((element) => (element as HTMLElement).dataset.level),
  )
}

async function itemHtmls(page: Page) {
  return items(page).evaluateAll((elements) => elements.map((element) => element.innerHTML))
}

/** Writes «Первый» and turns it into a list of the given kind with the panel's menu; the caret ends the item. */
async function startList(page: Page, kind: string) {
  await open(page)
  await press(paragraphs(page).first())
  await page.keyboard.type('Первый')
  await atStep(page, 1)
  await convert(page, 6, kind)
  await expect(items(page)).toHaveCount(1)
  await press(items(page).first())
  await page.keyboard.press('End')
}

test('adds items on Enter, nests them on Tab and lifts them on Shift+Tab', async ({ page }) => {
  await startList(page, 'Маркированный список')
  await expect(lists(page).first()).toHaveJSProperty('tagName', 'UL')

  await page.keyboard.press('Enter')
  await page.keyboard.type('Вложенный')
  await page.keyboard.press('Tab')
  await page.keyboard.press('Enter')
  await page.keyboard.type('Ещё вложенный')
  await page.keyboard.press('Enter')
  await page.keyboard.type('Второй')
  await page.keyboard.press('Shift+Tab')

  await expect(items(page)).toHaveText(['Первый', 'Вложенный', 'Ещё вложенный', 'Второй'])
  expect(await levels(page)).toEqual(['0', '1', '1', '0'])
  // Tab stays in the list: the first item has no parent, and one level is the deepest.
  await press(items(page).first())
  await page.keyboard.press('Tab')
  await press(items(page).nth(1))
  await page.keyboard.press('Tab')
  expect(await levels(page)).toEqual(['0', '1', '1', '0'])
  await expect(items(page).nth(1)).toBeFocused()

  // Enter inside an item splits it at the caret.
  await press(items(page).nth(3))
  await page.keyboard.press('End')
  for (let i = 0; i < 4; i++) await page.keyboard.press('ArrowLeft')
  await page.keyboard.press('Enter')
  await expect(items(page)).toHaveText(['Первый', 'Вложенный', 'Ещё вложенный', 'Вт', 'орой'])
  await expect(items(page).nth(4)).toBeFocused()
})

test('ends the list on Enter in an empty item and goes on with text', async ({ page }) => {
  await startList(page, 'Нумерованный список')
  await page.keyboard.press('Enter')
  await page.keyboard.type('Вложенный')
  await page.keyboard.press('Tab')
  await page.keyboard.press('Enter')

  // Enter in an empty nested item lifts it; Enter in an empty top item ends the list.
  await page.keyboard.press('Enter')
  expect(await levels(page)).toEqual(['0', '1', '0'])
  await page.keyboard.press('Enter')
  await expect(items(page)).toHaveText(['Первый', 'Вложенный'])
  await page.keyboard.type('После списка')
  await expect(paragraphs(page)).toHaveText(['После списка'])

  const order = await page
    .locator('.ce-block')
    .evaluateAll((blocks) => blocks.map((block) => (block.querySelector('.cdx-list') ? 'list' : 'text')))
  expect(order).toEqual(['list', 'text'])
})

test('switches between bullets and numbers without losing the text or the nesting', async ({ page }) => {
  await startList(page, 'Маркированный список')
  await page.keyboard.press('Enter')
  await page.keyboard.type('Вложенный ')
  await page.keyboard.press('ControlOrMeta+b')
  await page.keyboard.type('жирный')
  await page.keyboard.press('ControlOrMeta+b')
  await page.keyboard.press('Tab')
  const written = await itemHtmls(page)
  expect(written[1]).toContain('<b>жирный</b>')

  await convert(page, 6, 'Нумерованный список')
  await expect(lists(page)).toHaveCount(1)
  await expect(lists(page).first()).toHaveJSProperty('tagName', 'OL')
  expect(await itemHtmls(page)).toEqual(written)
  expect(await levels(page)).toEqual(['0', '1'])

  await press(items(page).nth(1))
  await page.keyboard.press('End')
  await convert(page, 6, 'Маркированный список')
  await expect(lists(page).first()).toHaveJSProperty('tagName', 'UL')
  expect(await itemHtmls(page)).toEqual(written)
  expect(await levels(page)).toEqual(['0', '1'])
})

test('keeps the list and its nesting through undo and redo', async ({ page }) => {
  await startList(page, 'Нумерованный список')
  await page.keyboard.press('Enter')
  await page.keyboard.type('Вложенный')
  await page.keyboard.press('Tab')
  await expect.poll(() => levels(page)).toEqual(['0', '1'])
  await expect.poll(() => step(page)).toBeGreaterThan(1)
  const done = await step(page)

  await undo(page)
  await atStep(page, done - 1)
  await redo(page)
  await atStep(page, done)
  await expect(items(page)).toHaveText(['Первый', 'Вложенный'])
  expect(await levels(page)).toEqual(['0', '1'])
  await expect(lists(page).first()).toHaveJSProperty('tagName', 'OL')
})

// Quote and delimiter blocks (E05-05, #40): the quote keeps its text and the caption of its source in two fields; the
// delimiter has no content, is not editable and goes with one key.

const quotes = (page: Page) => page.locator('.cdx-quote')
const quoteText = (page: Page) => page.locator('.cdx-quote__text')
const quoteCaption = (page: Page) => page.locator('.cdx-quote__caption')
const delimiters = (page: Page) => page.locator('.ce-delimiter')
const selectedDelimiter = (page: Page) => page.locator('.ce-block--selected .ce-delimiter')

/** Opens the block menu with «+» of the empty block under the caret and adds the block named `name`. */
async function addBlock(page: Page, name: string) {
  await press(page.locator('.ce-toolbar__plus'))
  await press(page.locator('.ce-popover-item__title:visible').filter({ hasText: new RegExp(`^${name}$`) }))
}

/**
 * Undoes or redoes until `reached` passes. Editor.js reports typing in batches, so the number of steps to a state
 * varies; each step is still rendered from the saved document.
 */
async function stepUntil(page: Page, action: (page: Page) => Promise<void>, reached: () => Promise<void>) {
  await expect(async () => {
    await action(page)
    await reached()
  }).toPass({ intervals: [0], timeout: 15_000 })
}

test('keeps the text and the caption of a quote apart, also through undo and redo', async ({ page }) => {
  await open(page)
  await press(paragraphs(page).first())
  await addBlock(page, 'Цитата')
  await expect(quotes(page)).toHaveCount(1)
  await expect(paragraphs(page)).toHaveCount(0)
  await expect(quoteText(page)).toHaveAttribute('data-placeholder', 'Цитата')
  await expect(quoteCaption(page)).toHaveAttribute('data-placeholder', 'Подпись, источник')

  await press(quoteText(page))
  await page.keyboard.type('Быть или не быть')
  // Enter in the text goes to the caption.
  await page.keyboard.press('Enter')
  await expect(quoteCaption(page)).toBeFocused()
  await page.keyboard.type('Гамлет')
  await expect(quoteText(page)).toHaveText('Быть или не быть')
  await expect(quoteCaption(page)).toHaveText('Гамлет')

  // Undo to before the quote and redo to the end render the saved document: each field comes back on its own.
  await stepUntil(page, undo, () => expect(quotes(page)).toHaveCount(0, { timeout: 500 }))
  await stepUntil(page, redo, () => expect(quoteCaption(page)).toHaveText('Гамлет', { timeout: 500 }))
  await expect(quoteText(page)).toHaveText('Быть или не быть')

  // Enter in the caption goes on with text after the quote.
  await press(quoteCaption(page))
  await page.keyboard.press('End')
  await page.keyboard.press('Enter')
  await page.keyboard.type('Дальше')
  await expect(paragraphs(page)).toHaveText(['Дальше'])
  await expect(quotes(page)).toHaveCount(1)
  await expect(quoteCaption(page)).toHaveText('Гамлет')
})

test('converts text to a quote and the quote back to text with its caption', async ({ page }) => {
  await open(page)
  await press(paragraphs(page).first())
  await page.keyboard.type('Слова')
  await atStep(page, 1)
  await convert(page, 5, 'Цитата')
  await expect(quoteText(page)).toHaveText('Слова')
  await expect(quoteCaption(page)).toHaveText('')

  await press(quoteText(page))
  await page.keyboard.press('End')
  await page.keyboard.press('Enter')
  await page.keyboard.type('Автор')
  await convert(page, 5, 'Текст')
  await expect(quotes(page)).toHaveCount(0)
  await expect.poll(() => htmls(page)).toEqual(['Слова — Автор'])
})

/** Writes «Первый», adds a delimiter after it and writes «Второй» in the text block below the delimiter. */
async function startWithDelimiter(page: Page) {
  await open(page)
  await press(paragraphs(page).first())
  await page.keyboard.type('Первый')
  await page.keyboard.press('Enter')
  await expect(paragraphs(page).nth(1)).toBeFocused()
  await addBlock(page, 'Разделитель')
  await expect(delimiters(page)).toHaveCount(1)
  // The caret goes on below the line.
  await expect(paragraphs(page).nth(1)).toBeFocused()
  await page.keyboard.type('Второй')
  await expect(paragraphs(page)).toHaveText(['Первый', 'Второй'])
  await settled(page)
}

/**
 * Waits until the history stops moving: Editor.js reports changes in batches some time after them, and the keys of
 * a test should come after the last one.
 */
async function settled(page: Page) {
  let last = -1
  await expect
    .poll(
      async () => {
        const now = await step(page)
        const same = now === last
        last = now
        return same && now > 0
      },
      { intervals: [800] },
    )
    .toBe(true)
}

test('adds a delimiter that is not editable and keeps it through undo and redo', async ({ page }) => {
  await startWithDelimiter(page)
  const delimiter = delimiters(page).first()
  await expect(delimiter).toHaveAttribute('contenteditable', 'false')
  await expect(delimiter).toHaveRole('separator')
  await expect(delimiter).toBeEmpty()
  await expect(delimiter.locator('[contenteditable="true"]')).toHaveCount(0)

  // Undo to before the delimiter and redo to the end render it from the saved document (`data: {}`).
  await stepUntil(page, undo, () => expect(delimiters(page)).toHaveCount(0, { timeout: 500 }))
  await stepUntil(page, redo, () =>
    expect(paragraphs(page)).toHaveText(['Первый', 'Второй'], { timeout: 500 }),
  )
  await expect(delimiters(page)).toHaveCount(1)
  await expect(delimiters(page).first()).toHaveAttribute('contenteditable', 'false')
})

test('removes the delimiter with Backspace at the start of the next block', async ({ page }) => {
  await startWithDelimiter(page)
  await page.keyboard.press('Home')
  await page.keyboard.press('Backspace')
  await expect(delimiters(page)).toHaveCount(0)
  await expect(paragraphs(page)).toHaveText(['Первый', 'Второй'])
  await expect(paragraphs(page).nth(1)).toBeFocused()
})

test('removes the delimiter with Delete at the end of the block before', async ({ page }) => {
  await startWithDelimiter(page)
  await press(paragraphs(page).first())
  await page.keyboard.press('End')
  await page.keyboard.press('Delete')
  await expect(delimiters(page)).toHaveCount(0)
  await expect(paragraphs(page)).toHaveText(['Первый', 'Второй'])
  await expect(paragraphs(page).first()).toBeFocused()
})

test('removes a selected delimiter with one key and leaves no empty block', async ({ page }) => {
  await startWithDelimiter(page)

  // A click selects it; Backspace removes it and puts the caret at the end of the block before.
  await press(delimiters(page).first())
  await expect(selectedDelimiter(page)).toHaveCount(1)
  await page.keyboard.press('Backspace')
  await expect(delimiters(page)).toHaveCount(0)
  await expect(paragraphs(page)).toHaveText(['Первый', 'Второй'])
  await page.keyboard.type('!')
  await expect(paragraphs(page)).toHaveText(['Первый!', 'Второй'])

  // The arrow from the start of the next block selects it too; Delete removes it.
  await page.keyboard.press('Backspace')
  await page.keyboard.press('Enter')
  await addBlock(page, 'Разделитель')
  await expect(delimiters(page)).toHaveCount(1)
  await expect(paragraphs(page).nth(1)).toBeFocused()
  await settled(page)
  await page.keyboard.press('ArrowLeft')
  await expect(selectedDelimiter(page)).toHaveCount(1)
  await page.keyboard.press('Delete')
  await expect(delimiters(page)).toHaveCount(0)
  await expect(paragraphs(page)).toHaveText(['Первый', 'Второй'])
  await expect(paragraphs(page).nth(1)).toBeFocused()
})

// The block menu with its search and moving blocks (E05-07, #41): «+» opens the menu, typing filters it and the arrows
// with Enter pick a block; on a phone the menu is a bottom sheet. A block is dragged by «⋮⋮» or moved by
// Ctrl+Shift+↑/↓, and the move is undone like any change.

const menuItems = (page: Page) => page.locator('.ce-toolbox .ce-popover-item:visible')
const menuSearch = (page: Page) => page.locator('.ce-toolbox .cdx-search-field__input')
const menuPanel = (page: Page) => page.locator('.ce-toolbox .ce-popover--opened > .ce-popover__container')

async function startWithThree(page: Page) {
  await open(page)
  await press(paragraphs(page).first())
  // Each Enter waits for the history to settle: a change Editor.js reports during the key can lose it.
  await page.keyboard.type('Первый')
  await settled(page)
  await page.keyboard.press('Enter')
  await expect(paragraphs(page).nth(1)).toBeFocused()
  await page.keyboard.type('Второй')
  await settled(page)
  await page.keyboard.press('Enter')
  await expect(paragraphs(page).nth(2)).toBeFocused()
  await page.keyboard.type('Третий')
  await expect(paragraphs(page)).toHaveText(['Первый', 'Второй', 'Третий'])
  await settled(page)
}

test('filters the block menu by typing and adds the block picked with the keyboard', async ({ page }) => {
  await open(page)
  await press(paragraphs(page).first())
  await press(page.locator('.ce-toolbar__plus'))
  await expect(menuSearch(page)).toHaveAttribute('placeholder', 'Поиск')
  // On a desktop the search takes the focus; on a phone it waits for a tap, so the sheet does not move the caret.
  if (isTouch()) {
    await expect(menuSearch(page)).not.toBeFocused()
    await press(menuSearch(page))
  }
  await expect(menuSearch(page)).toBeFocused()
  await expect(menuItems(page)).toHaveCount(7)

  await page.keyboard.type('спис')
  await expect(menuItems(page)).toHaveText(['Маркированный список', 'Нумерованный список'])
  await page.keyboard.type('ок-нет')
  await expect(menuItems(page)).toHaveCount(0)
  await expect(page.locator('.ce-toolbox .ce-popover__nothing-found-message')).toHaveText('Ничего не найдено')
  await expect(page.locator('.ce-toolbox .ce-popover__nothing-found-message')).toBeVisible()
  for (let left = 6; left > 0; left--) await page.keyboard.press('Backspace')
  await expect(menuItems(page)).toHaveCount(2)
  await expect(page.locator('.ce-toolbox .ce-popover__nothing-found-message')).toBeHidden()

  if (isTouch()) {
    // On a phone Enter adds the first block found.
    await menuSearch(page).fill('нумер')
    await expect(menuItems(page)).toHaveText(['Нумерованный список'])
  } else {
    // The arrows go through the found blocks, Enter adds the focused one.
    await page.keyboard.press('ArrowDown')
    await page.keyboard.press('ArrowDown')
    await expect(page.locator('.ce-toolbox .ce-popover-item--focused')).toHaveText('Нумерованный список')
  }
  await page.keyboard.press('Enter')
  await expect(page.locator('.cdx-list--ordered')).toHaveCount(1)
  await expect(menuPanel(page)).toHaveCount(0)
  await expect(paragraphs(page)).toHaveCount(0)

  // Enter in the empty item ends the list; the menu opens again with all its blocks.
  await page.keyboard.press('Enter')
  await expect(paragraphs(page)).toHaveCount(1)
  await press(paragraphs(page).first())
  await press(page.locator('.ce-toolbar__plus'))
  await expect(menuItems(page)).toHaveCount(7)
})

test('opens the block menu by "/" in an empty block and closes it on Escape', async ({ page }) => {
  await open(page)
  await press(paragraphs(page).first())
  await page.keyboard.press('/')
  await expect(menuPanel(page)).toBeVisible()
  if (!isTouch()) {
    await page.keyboard.type('цит')
    await expect(menuItems(page)).toHaveText(['Цитата'])
  }
  await page.keyboard.press('Escape')
  await expect(menuPanel(page)).toHaveCount(0)
  await expect(page.getByTestId('editor-window')).toBeVisible()
})

test('shows the block menu under «+» on a desktop and as a bottom sheet on a phone', async ({ page }) => {
  await open(page)
  await press(paragraphs(page).first())
  await press(page.locator('.ce-toolbar__plus'))
  const viewport = page.viewportSize()!
  if (isTouch()) {
    // The sheet slides in; its place is checked once it stands.
    await expect
      .poll(async () => {
        const panel = await box(menuPanel(page))
        return [panel.x, panel.width, panel.y + panel.height].map(Math.round)
      })
      .toEqual([0, viewport.width, viewport.height])
    await expect(page.locator('.ce-toolbox .ce-popover__overlay')).toBeVisible()
    // A tap on the overlay closes the sheet.
    await page.touchscreen.tap(viewport.width / 2, 20)
    await expect(menuPanel(page)).toHaveCount(0)
  } else {
    const panel = await box(menuPanel(page))
    const plus = await box(page.locator('.ce-toolbar__plus'))
    expect(panel.width).toBeLessThan(viewport.width / 2)
    expect(panel.y).toBeGreaterThanOrEqual(plus.y + plus.height)
    await expect(page.locator('.ce-toolbox .ce-popover__overlay')).toBeHidden()
  }
})

test('drags a block by «⋮⋮» to another place and undoes the move', async ({ page }) => {
  await startWithThree(page)
  const before = await step(page)

  // The toolbar of the first block: under the pointer on a desktop, of the focused block on a phone.
  if (isTouch()) await press(paragraphs(page).first())
  else {
    // The pointer is already over the first block from the click that started typing: it has to come back to it.
    await paragraphs(page).nth(2).hover()
    await paragraphs(page).first().hover()
  }
  // On a desktop the handle stands at the block's line, on a phone under it; it is read once the toolbar has moved.
  const first = await box(paragraphs(page).first())
  await expect
    .poll(async () => {
      const handle = await box(page.locator('.ce-toolbar__settings-btn'))
      const top = handle.y + handle.height / 2
      return top > first.y && top < first.y + first.height * (isTouch() ? 2.5 : 1)
    })
    .toBe(true)
  const handle = await box(page.locator('.ce-toolbar__settings-btn'))
  const last = await box(paragraphs(page).nth(2))
  const x = handle.x + handle.width / 2
  await page.mouse.move(x, handle.y + handle.height / 2)
  await page.mouse.down()
  await page.mouse.move(x, last.y + last.height / 2, { steps: 5 })
  await page.mouse.move(x, last.y + last.height, { steps: 5 })
  await expect(page.locator('.ce-block--dragging')).toHaveCount(1)
  await expect(page.locator('.ce-block[data-drop="after"]')).toHaveText('Третий')
  await page.mouse.up()

  await expect(paragraphs(page)).toHaveText(['Второй', 'Третий', 'Первый'])
  await expect(page.locator('.ce-block--dragging, .ce-block[data-drop]')).toHaveCount(0)
  // Letting go does not open the block menu.
  await expect(page.locator('.ce-settings .ce-popover--opened')).toHaveCount(0)
  await atStep(page, before + 1)

  await undo(page)
  await expect(paragraphs(page)).toHaveText(['Первый', 'Второй', 'Третий'])
  await redo(page)
  await expect(paragraphs(page)).toHaveText(['Второй', 'Третий', 'Первый'])
})

test('moves a block with Ctrl+Shift+arrows and keeps the caret in it', async ({ page }) => {
  await startWithThree(page)
  const before = await step(page)
  await press(paragraphs(page).nth(1))
  await page.keyboard.press('End')

  await page.keyboard.press('ControlOrMeta+Shift+ArrowUp')
  await expect(paragraphs(page)).toHaveText(['Второй', 'Первый', 'Третий'])
  await page.keyboard.type('!')
  await expect(paragraphs(page)).toHaveText(['Второй!', 'Первый', 'Третий'])

  // At the top it stays; down twice takes it to the end.
  await page.keyboard.press('ControlOrMeta+Shift+ArrowUp')
  await page.keyboard.press('ControlOrMeta+Shift+ArrowDown')
  await page.keyboard.press('ControlOrMeta+Shift+ArrowDown')
  await expect(paragraphs(page)).toHaveText(['Первый', 'Третий', 'Второй!'])
  await expect(paragraphs(page).nth(2)).toBeFocused()
  await settled(page)
  expect(await step(page)).toBeGreaterThan(before)

  await stepUntil(page, undo, () =>
    expect(paragraphs(page)).toHaveText(['Первый', 'Второй', 'Третий'], { timeout: 500 }),
  )
})
