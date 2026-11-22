import type { API, BlockToolConstructorOptions, PasteEvent } from '@editorjs/editorjs'
import { describe, expect, it } from 'vitest'
import { TextTool, textTags, type TextConfig, type TextData } from '../../editor/text'
import { isSafeHref } from '../../editor/inline'

// The text block (ADR-0044, #331): the paragraph of Editor.js 2.31 with a flat sanitize, so a heading turned into text
// keeps its inline markup.

const api = { styles: { block: 'cdx-block' }, i18n: { t: (text: string) => text } } as unknown as API

function tool(data: Partial<TextData>, config: TextConfig = {}, readOnly = false) {
  return new TextTool({ data, config, api, readOnly } as unknown as BlockToolConstructorOptions<
    Partial<TextData>,
    TextConfig
  >)
}

type Rules = Record<string, unknown>

/**
 * What Editor.js 2.31 does with a block tool's sanitize: an object rule gets the tags of the inline tools merged in,
 * any other rule is kept as it is (BlockToolAdapter.sanitizeConfig).
 */
function composed(sanitize: Rules, inline: Rules): Rules {
  return Object.fromEntries(
    Object.entries(sanitize).map(([key, rule]) => [
      key,
      rule && typeof rule === 'object' ? { ...inline, ...(rule as Rules) } : rule,
    ]),
  )
}

describe('TextTool', () => {
  it('keeps the tags of its inline tools and the line break, the server whitelist (ADR-0023)', () => {
    expect(Object.keys(textTags()).sort()).toEqual(['a', 'b', 'br', 'i', 'mark'])
    const link = textTags().a as { href: (value: string) => boolean; rel: boolean }
    expect(link.rel).toBe(true)
    expect(link.href('https://example.test/')).toBe(isSafeHref('https://example.test/'))
    expect(link.href('javascript:alert(1)')).toBe(false)
  })

  it('declares a flat list of tags that Editor.js does not change, so conversion reads it as tags', () => {
    const sanitize = TextTool.sanitize as unknown as Rules
    // No per-field rule: Editor.js 2.31 reads the whole config as a list of tags when it converts into the block.
    expect(sanitize).not.toHaveProperty('text')
    const inline = { b: {}, i: {}, mark: {}, a: { href: true } }
    const config = composed(sanitize, inline)
    for (const [tag, attributes] of Object.entries(textTags())) {
      const rule = config[tag]
      expect(typeof rule).toBe('function')
      expect(Object.keys((rule as () => Rules)())).toEqual(Object.keys(attributes))
    }
  })

  it('converts to and from other blocks by the text field and takes pasted paragraphs', () => {
    expect(TextTool.conversionConfig).toEqual({ export: 'text', import: 'text' })
    expect(TextTool.pasteConfig).toEqual({ tags: ['P'] })
  })

  it('renders the text with its markup as an editable paragraph', () => {
    const element = tool({ text: 'Глава <b>первая</b>' }).render()
    expect(element.classList.contains('ce-paragraph')).toBe(true)
    expect(element.classList.contains('cdx-block')).toBe(true)
    expect(element.innerHTML).toBe('Глава <b>первая</b>')
    expect(element.getAttribute('contenteditable')).toBe('true')
    expect(tool({}, {}, true).render().getAttribute('contenteditable')).toBe('false')
  })

  it('shows the placeholder of its config', () => {
    expect(tool({}, { placeholder: 'Начните писать' }).render().dataset.placeholderActive).toBe(
      'Начните писать',
    )
  })

  it('saves the text with its markup and merges another block at the end', () => {
    const text = tool({ text: 'Глава ' })
    const element = text.render()
    text.merge({ text: ' <i>первая</i>' })
    expect(text.save(element)).toEqual({ text: 'Глава <i>первая</i>' })
  })

  it('drops an empty paragraph on save unless preserveBlank is set', () => {
    expect(tool({}).validate({ text: ' ' })).toBe(false)
    expect(tool({}, { preserveBlank: true }).validate({ text: '' })).toBe(true)
    expect(tool({}).validate({ text: 'Текст' })).toBe(true)
  })

  it('takes the content of a pasted paragraph', async () => {
    const text = tool({})
    const element = text.render()
    const pasted = document.createElement('p')
    pasted.innerHTML = 'Из <mark>буфера</mark>'
    text.onPaste({ detail: { data: pasted } } as unknown as PasteEvent)
    await new Promise((resolve) => window.requestAnimationFrame(resolve))
    expect(element.innerHTML).toBe('Из <mark>буфера</mark>')
  })

  it('clears a paragraph emptied by Backspace, so its placeholder shows', () => {
    const element = tool({ text: 'x' }).render()
    element.innerHTML = '<br>'
    element.dispatchEvent(new KeyboardEvent('keyup', { code: 'Backspace' }))
    expect(element.innerHTML).toBe('')
  })
})
