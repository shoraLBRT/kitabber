// Moving blocks (E05-07, ADR-0039): Editor.js 2.31 moves a block only by «Выше» and «Ниже» of its menu «⋮⋮» and has
// no drag of blocks (its drag and drop is for text and files). Here the block is dragged by the «⋮⋮» button with the
// mouse, a pen or a finger (pointer events), and moved by Ctrl+Shift+↑ and Ctrl+Shift+↓ (Cmd on a Mac) from inside
// it. Both go through editor.blocks.move, so the move is one change of the history (undo puts the block back).
import type { API } from '@editorjs/editorjs'
import { caretOffsetIn, placeCaret } from './caret'

/** The part of the Editor.js API moving blocks uses. */
export interface MoveApi {
  blocks: Pick<API['blocks'], 'getCurrentBlockIndex' | 'getBlocksCount' | 'move'>
  toolbar: Pick<API['toolbar'], 'close' | 'toggleBlockSettings'>
  caret: Pick<API['caret'], 'setToBlock'>
}

/** The drag handle: the «⋮⋮» button of the Editor.js toolbar. */
export const handleSelector = '.ce-toolbar__settings-btn'
/** On the block being dragged. */
export const draggingClass = 'ce-block--dragging'
/** On the block the dragged one lands next to: data-drop="before" or "after". */
export const dropAttribute = 'data-drop'

/** Pixels the pointer goes before a press on «⋮⋮» becomes a drag; a shorter press still opens the block menu. */
export const dragThreshold = 4

/**
 * Where a dragged block lands: the index it gets among all blocks, given the vertical middles of the blocks in order
 * (the dragged one included) and the pointer's y. The block goes after every other block whose middle is above y.
 */
export function dropIndex(middles: readonly number[], from: number, y: number): number {
  let index = 0
  middles.forEach((middle, at) => {
    if (at !== from && middle < y) index++
  })
  return index
}

/** The step of the keyboard move for a key press: -1 up, 1 down, 0 for any other key. */
export function keyStep(
  event: Pick<KeyboardEvent, 'code' | 'ctrlKey' | 'metaKey' | 'shiftKey' | 'altKey'>,
): number {
  if (!(event.ctrlKey || event.metaKey) || !event.shiftKey || event.altKey) return 0
  if (event.code === 'ArrowUp') return -1
  if (event.code === 'ArrowDown') return 1
  return 0
}

/** Turns on dragging by «⋮⋮» and the keys in the editor at holder; returns the function that turns them off. */
export function moveBlocks(holder: HTMLElement, api: MoveApi): () => void {
  const blocks = () => Array.from(holder.querySelectorAll<HTMLElement>('.ce-block'))

  const onKeydown = (event: KeyboardEvent) => {
    const step = keyStep(event)
    if (step === 0) return
    const from = api.blocks.getCurrentBlockIndex()
    if (from < 0) return
    // The keys always belong to the move here, also at the edges: the browser's own Ctrl+Shift+↑ selects text.
    event.preventDefault()
    event.stopPropagation()
    const to = from + step
    if (to < 0 || to >= api.blocks.getBlocksCount()) return
    // Moving the element drops the focus and the selection; the caret goes back into the field it was in (it moves
    // with the block), at the text offset it had.
    const selection = window.getSelection()
    const node = selection?.focusNode
    const field = (node instanceof Element ? node : node?.parentElement)?.closest<HTMLElement>(
      '[contenteditable="true"]',
    )
    const offset = field ? caretOffsetIn(field, selection) : null
    api.blocks.move(to, from)
    if (field && offset !== null) {
      field.focus({ preventScroll: true })
      placeCaret(field, offset)
      field.scrollIntoView?.({ block: 'nearest' })
    } else {
      api.caret.setToBlock(to, 'end')
    }
  }

  let press: { pointer: number; x: number; y: number; handle: HTMLElement } | null = null
  let drag: { from: number; to: number; block: HTMLElement } | null = null

  const clearMarks = () => {
    holder.querySelectorAll(`[${dropAttribute}]`).forEach((element) => element.removeAttribute(dropAttribute))
  }

  const mark = (to: number) => {
    clearMarks()
    if (!drag) return
    const others = blocks().filter((element) => element !== drag!.block)
    if (others.length === 0) return
    if (to < others.length) others[to]!.setAttribute(dropAttribute, 'before')
    else others[others.length - 1]!.setAttribute(dropAttribute, 'after')
  }

  const target = (y: number) => {
    const middles = blocks().map((element) => {
      const rect = element.getBoundingClientRect()
      return rect.top + rect.height / 2
    })
    return dropIndex(middles, drag!.from, y)
  }

  const onPointerDown = (event: PointerEvent) => {
    if (event.button !== 0 || !(event.target instanceof Element)) return
    const handle = event.target.closest<HTMLElement>(handleSelector)
    if (!handle || !holder.contains(handle)) return
    press = { pointer: event.pointerId, x: event.clientX, y: event.clientY, handle }
  }

  const onPointerMove = (event: PointerEvent) => {
    if (!press || event.pointerId !== press.pointer) return
    if (!drag) {
      if (Math.hypot(event.clientX - press.x, event.clientY - press.y) < dragThreshold) return
      // The press opened the block menu and made the toolbar's block the current one; that block is dragged.
      const from = api.blocks.getCurrentBlockIndex()
      const block = blocks()[from]
      if (from < 0 || !block) {
        press = null
        return
      }
      api.toolbar.toggleBlockSettings(false)
      press.handle.setPointerCapture?.(event.pointerId)
      drag = { from, to: from, block }
      block.classList.add(draggingClass)
    }
    event.preventDefault()
    drag.to = target(event.clientY)
    mark(drag.to)
  }

  const finish = (event: PointerEvent, drop: boolean) => {
    if (!press || event.pointerId !== press.pointer) return
    press = null
    if (!drag) return
    const { from, to, block } = drag
    drag = null
    block.classList.remove(draggingClass)
    clearMarks()
    // The click that ends the drag must not open the block menu again.
    holder.addEventListener('click', swallow, { capture: true, once: true })
    setTimeout(() => holder.removeEventListener('click', swallow, true), 0)
    api.toolbar.close()
    if (drop && to !== from) {
      api.blocks.move(to, from)
      api.caret.setToBlock(to, 'end')
    }
  }
  const swallow = (event: Event) => {
    event.preventDefault()
    event.stopPropagation()
  }
  const onPointerUp = (event: PointerEvent) => finish(event, true)
  const onPointerCancel = (event: PointerEvent) => finish(event, false)

  holder.addEventListener('keydown', onKeydown, true)
  holder.addEventListener('pointerdown', onPointerDown, true)
  window.addEventListener('pointermove', onPointerMove)
  window.addEventListener('pointerup', onPointerUp)
  window.addEventListener('pointercancel', onPointerCancel)
  return () => {
    holder.removeEventListener('keydown', onKeydown, true)
    holder.removeEventListener('pointerdown', onPointerDown, true)
    window.removeEventListener('pointermove', onPointerMove)
    window.removeEventListener('pointerup', onPointerUp)
    window.removeEventListener('pointercancel', onPointerCancel)
  }
}
