import type { Locator, Page } from '@playwright/test'
import { expect, test, waitForHydration } from './fixtures'

// Arabic text and right-to-left writing in content (E02-14, #67, ADR-0068) in both profiles, on the Arabic post of the
// fake API (e2e/fake-api.mjs): a block that starts with Arabic letters is `dir="auto"` and drawn right to left, its
// letters and harakat in Noto Naskh Arabic served by the site, and a mixed Russian and Arabic line keeps its
// punctuation and digits where they belong. The places of characters are read from the layout (Range rectangles), so
// the check is the same on every machine, unlike a picture.

const address = '/u/anna/0195f3a0-0000-7000-8000-0000000000c9'

interface Span {
  left: number
  right: number
}

/** Where the given piece of the element's text is drawn: its leftmost and rightmost edges on the line. */
async function spanOf(element: Locator, piece: string): Promise<Span> {
  const span = await element.evaluate((root, needle) => {
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT)
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      const at = node.textContent!.indexOf(needle)
      if (at < 0) continue
      const range = document.createRange()
      range.setStart(node, at)
      range.setEnd(node, at + needle.length)
      const rects = [...range.getClientRects()]
      return { left: Math.min(...rects.map((r) => r.left)), right: Math.max(...rects.map((r) => r.right)) }
    }
    return null
  }, piece)
  expect(span, `«${piece}» must be in the text`).not.toBeNull()
  return span!
}

/** The families the browser really drew the element's text with (Chromium, DevTools protocol). */
async function drawnFonts(
  page: Page,
  selector: string,
): Promise<{ familyName: string; glyphCount: number }[]> {
  const cdp = await page.context().newCDPSession(page)
  await cdp.send('DOM.enable')
  await cdp.send('CSS.enable')
  const { root } = await cdp.send('DOM.getDocument')
  const { nodeId } = await cdp.send('DOM.querySelector', { nodeId: root.nodeId, selector })
  const { fonts } = await cdp.send('CSS.getPlatformFontsForNode', { nodeId })
  await cdp.detach()
  return fonts
}

test.describe('арабский текст в контенте', () => {
  test('блок на арабском получает dir=auto и идёт справа налево', async ({ page }) => {
    await page.goto(address)
    await waitForHydration(page)
    const blocks = page.getByTestId('post-blocks')
    const arabic = blocks.locator('p').first()
    await expect(arabic).toHaveText('بِسْمِ اللَّهِ الرَّحْمَٰنِ الرَّحِيمِ')
    await expect(arabic).toHaveAttribute('dir', 'auto')
    expect(await arabic.evaluate((node) => node.matches(':dir(rtl)'))).toBe(true)

    // The line lies against the right edge of the block: the first word is the rightmost one.
    const block = (await arabic.boundingBox())!
    const first = await spanOf(arabic, 'بِسْمِ')
    const last = await spanOf(arabic, 'الرَّحِيمِ')
    expect(Math.abs(block.x + block.width - first.right)).toBeLessThan(2)
    expect(last.right).toBeLessThan(first.left)

    // The quote and the list are right to left too: the line of the quote and the markers of the list on the right.
    const quote = blocks.locator('figure')
    expect(await quote.evaluate((node) => node.matches(':dir(rtl)'))).toBe(true)
    const line = await quote.evaluate((node) => getComputedStyle(node).borderRightWidth)
    expect(parseFloat(line)).toBeGreaterThan(0)
    const list = blocks.locator('ol')
    expect(await list.evaluate((node) => node.matches(':dir(rtl)'))).toBe(true)
    expect(await list.evaluate((node) => getComputedStyle(node).paddingRight)).not.toBe('0px')

    // A Russian block stays left to right; the title and the comment are dir=auto as well.
    expect(
      await blocks
        .locator('p')
        .nth(2)
        .evaluate((node) => node.matches(':dir(ltr)')),
    ).toBe(true)
    await expect(page.getByTestId('post-title')).toHaveAttribute('dir', 'auto')
    await expect(page.getByTestId('comment-text').first()).toHaveAttribute('dir', 'auto')
  })

  test('арабские буквы и огласовки нарисованы шрифтом Noto Naskh Arabic с сайта', async ({ page }) => {
    const faces: string[] = []
    page.on('request', (request) => {
      if (/noto-naskh-arabic-arabic-\d+-normal.*\.woff2/.test(request.url())) faces.push(request.url())
    })
    await page.goto(address)
    await waitForHydration(page)
    await page.evaluate(() => document.fonts.ready)
    await expect
      .poll(() => page.evaluate(() => document.fonts.check('400 16px "Noto Naskh Arabic"', 'بِسْمِ')))
      .toBe(true)
    // The harakat (fatha, kasra, sukun, shadda, the dagger alif) are drawn by the same face as their letters: no other
    // family fills in a missing mark; at most the three spaces between the words come from the next family.
    const fonts = await drawnFonts(page, '[data-testid="post-blocks"] p')
    expect(fonts.find((font) => font.familyName === 'Noto Naskh Arabic')?.glyphCount ?? 0).toBeGreaterThan(20)
    const others = fonts.filter((font) => font.familyName !== 'Noto Naskh Arabic')
    expect(others.reduce((sum, font) => sum + font.glyphCount, 0)).toBeLessThanOrEqual(3)
    expect(faces.length).toBeGreaterThan(0)
    for (const url of faces) expect(new URL(url).origin).toBe(new URL(page.url()).origin)
    // The Russian words of a mixed line come from the text family, the Arabic ones from the Arabic face.
    const title = (await drawnFonts(page, '[data-testid="post-title"]')).map((font) => font.familyName)
    expect(title).toContain('Noto Naskh Arabic')
    expect(title.length).toBeGreaterThan(1)
  })

  test('страница без арабского текста не загружает арабский шрифт', async ({ page }) => {
    const faces: string[] = []
    page.on('request', (request) => {
      if (request.url().includes('noto-naskh-arabic')) faces.push(request.url())
    })
    await page.goto('/u/anna/0195f3a0-0000-7000-8000-0000000000c2')
    await waitForHydration(page)
    await page.evaluate(() => document.fonts.ready)
    expect(faces).toEqual([])
  })

  test('смешанная строка русский+арабский не ломает пунктуацию', async ({ page }) => {
    await page.goto(address)
    await waitForHydration(page)
    const blocks = page.getByTestId('post-blocks')

    // An Arabic block with a Russian word: right to left, the colon after «قال» to its left, the Russian word read
    // left to right inside its guillemets, the full stop at the left end of the line.
    const arabicFirst = blocks.locator('p').nth(1)
    const said = await spanOf(arabicFirst, 'قال')
    const colon = await spanOf(arabicFirst, ':')
    const hello = await spanOf(arabicFirst, 'Привет')
    const went = await spanOf(arabicFirst, 'ذهب')
    const stop = await spanOf(arabicFirst, '.')
    expect(colon.right).toBeLessThanOrEqual(said.left + 1)
    expect(hello.right).toBeLessThan(colon.left)
    expect(went.right).toBeLessThan(hello.left)
    expect(stop.right).toBeLessThanOrEqual(went.left + 1)

    // A Russian block with an Arabic quote: left to right; the Arabic quote reads right to left inside the guillemets
    // (its bold first word on the right), the closing guillemet, the digits and the comma stay after it, on the right.
    const russianFirst = blocks.locator('p').nth(2)
    const open = await spanOf(russianFirst, '«')
    const first = await spanOf(russianFirst, 'سبحان')
    const second = await spanOf(russianFirst, 'الله')
    const close = await spanOf(russianFirst, '»')
    const digits = await spanOf(russianFirst, '33')
    const comma = await spanOf(russianFirst, ',')
    expect(open.right).toBeLessThanOrEqual(second.left + 1)
    expect(second.right).toBeLessThan(first.left)
    expect(close.left).toBeGreaterThanOrEqual(first.right - 1)
    expect(digits.left).toBeGreaterThan(close.right - 1)
    expect(comma.left).toBeGreaterThanOrEqual(digits.right - 1)

    // The same in a comment, plain text; the title, plain text too, carries the isolate around its Arabic words.
    const isolated = `Цитаты: «${String.fromCharCode(0x2067)}الحمد لله${String.fromCharCode(0x2069)}» 2 раза`
    expect(await page.getByTestId('post-title').textContent()).toBe(isolated)
    const comment = page.getByTestId('comment-text').first()
    expect((await spanOf(comment, '100')).left).toBeGreaterThan((await spanOf(comment, '»')).right - 1)
  })
})
