import { afterEach, describe, expect, it, vi } from 'vitest'
import { dragThreshold, dropIndex, keyStep, type MoveApi, moveBlocks } from '../../editor/move'

// Moving blocks (E05-07, #41): by the «⋮⋮» handle with a pointer and by Ctrl+Shift+↑/↓.

function fakeApi(count: number, current: number) {
  const api = {
    blocks: {
      getCurrentBlockIndex: vi.fn(() => current),
      getBlocksCount: vi.fn(() => count),
      move: vi.fn(),
    },
    toolbar: { close: vi.fn(), toggleBlockSettings: vi.fn() },
    caret: { setToBlock: vi.fn() },
  }
  return api as unknown as typeof api & MoveApi
}

/** An editor holder with three blocks 40 px high, one under another from y = 0, and the «⋮⋮» handle. */
function holderWithBlocks() {
  const holder = document.createElement('div')
  holder.innerHTML = `
    <div class="ce-block"><div contenteditable="true">Первый</div></div>
    <div class="ce-block"><div contenteditable="true">Второй</div></div>
    <div class="ce-block"><div contenteditable="true">Третий</div></div>
    <div class="ce-toolbar"><span class="ce-toolbar__settings-btn"></span></div>`
  holder.querySelectorAll<HTMLElement>('.ce-block').forEach((block, index) => {
    block.getBoundingClientRect = () => ({ top: index * 40, height: 40 }) as DOMRect
  })
  document.body.append(holder)
  return holder
}

const handle = (holder: HTMLElement) => holder.querySelector<HTMLElement>('.ce-toolbar__settings-btn')!

function pointer(target: EventTarget, type: string, y: number) {
  const event = new PointerEvent(type, {
    bubbles: true,
    cancelable: true,
    pointerId: 1,
    button: 0,
    clientX: 0,
    clientY: y,
  })
  target.dispatchEvent(event)
  return event
}

function keydown(target: HTMLElement, code: string, options: KeyboardEventInit = {}) {
  const event = new KeyboardEvent('keydown', { code, bubbles: true, cancelable: true, ...options })
  target.dispatchEvent(event)
  return event
}

let stop: (() => void) | null = null

afterEach(() => {
  stop?.()
  stop = null
  document.body.replaceChildren()
})

describe('dropIndex', () => {
  const middles = [20, 60, 100]

  it('puts the block after every other block whose middle is above the pointer', () => {
    expect(dropIndex(middles, 0, 70)).toBe(1)
    expect(dropIndex(middles, 0, 200)).toBe(2)
    expect(dropIndex(middles, 2, 0)).toBe(0)
    expect(dropIndex(middles, 2, 30)).toBe(1)
  })

  it('leaves the block in place while the pointer stays between its neighbours', () => {
    expect(dropIndex(middles, 1, 50)).toBe(1)
    expect(dropIndex(middles, 1, 90)).toBe(1)
  })
})

describe('keyStep', () => {
  it('reads Ctrl+Shift and Cmd+Shift with the arrows up and down', () => {
    expect(keyStep({ code: 'ArrowUp', ctrlKey: true, metaKey: false, shiftKey: true, altKey: false })).toBe(
      -1,
    )
    expect(keyStep({ code: 'ArrowDown', ctrlKey: false, metaKey: true, shiftKey: true, altKey: false })).toBe(
      1,
    )
  })

  it('ignores the arrows without both modifiers and other keys', () => {
    expect(keyStep({ code: 'ArrowUp', ctrlKey: true, metaKey: false, shiftKey: false, altKey: false })).toBe(
      0,
    )
    expect(keyStep({ code: 'ArrowUp', ctrlKey: false, metaKey: false, shiftKey: true, altKey: false })).toBe(
      0,
    )
    expect(keyStep({ code: 'ArrowUp', ctrlKey: true, metaKey: false, shiftKey: true, altKey: true })).toBe(0)
    expect(keyStep({ code: 'KeyZ', ctrlKey: true, metaKey: false, shiftKey: true, altKey: false })).toBe(0)
  })
})

describe('moveBlocks by keys', () => {
  it('moves the current block one place up or down', () => {
    const holder = holderWithBlocks()
    const api = fakeApi(3, 1)
    stop = moveBlocks(holder, api)
    const input = holder.querySelectorAll<HTMLElement>('[contenteditable]')[1]!

    expect(keydown(input, 'ArrowUp', { ctrlKey: true, shiftKey: true }).defaultPrevented).toBe(true)
    expect(api.blocks.move).toHaveBeenLastCalledWith(0, 1)
    keydown(input, 'ArrowDown', { metaKey: true, shiftKey: true })
    expect(api.blocks.move).toHaveBeenLastCalledWith(2, 1)
  })

  it('keeps the first block first and the last block last, and still takes the keys', () => {
    const holder = holderWithBlocks()
    const first = fakeApi(3, 0)
    stop = moveBlocks(holder, first)
    expect(keydown(holder, 'ArrowUp', { ctrlKey: true, shiftKey: true }).defaultPrevented).toBe(true)
    stop()
    const last = fakeApi(3, 2)
    stop = moveBlocks(holder, last)
    keydown(holder, 'ArrowDown', { ctrlKey: true, shiftKey: true })
    expect(first.blocks.move).not.toHaveBeenCalled()
    expect(last.blocks.move).not.toHaveBeenCalled()
  })

  it('leaves other keys to the editor', () => {
    const holder = holderWithBlocks()
    const api = fakeApi(3, 1)
    stop = moveBlocks(holder, api)
    expect(keydown(holder, 'ArrowUp', { shiftKey: true }).defaultPrevented).toBe(false)
    expect(api.blocks.move).not.toHaveBeenCalled()
  })
})

describe('moveBlocks by dragging «⋮⋮»', () => {
  it('drags the current block to where the pointer is let go and puts the caret in it', () => {
    const holder = holderWithBlocks()
    const api = fakeApi(3, 0)
    stop = moveBlocks(holder, api)
    const blocks = holder.querySelectorAll('.ce-block')

    pointer(handle(holder), 'pointerdown', 10)
    pointer(window, 'pointermove', 90)
    expect(api.toolbar.toggleBlockSettings).toHaveBeenCalledWith(false)
    expect(blocks[0]!.classList.contains('ce-block--dragging')).toBe(true)
    expect(blocks[2]!.getAttribute('data-drop')).toBe('before')
    pointer(window, 'pointermove', 115)
    expect(blocks[2]!.getAttribute('data-drop')).toBe('after')
    pointer(window, 'pointerup', 115)

    expect(api.blocks.move).toHaveBeenCalledWith(2, 0)
    expect(api.caret.setToBlock).toHaveBeenCalledWith(2, 'end')
    expect(holder.querySelectorAll('[data-drop], .ce-block--dragging')).toHaveLength(0)
  })

  it('swallows the click that ends a drag, so the block menu does not open again', () => {
    const holder = holderWithBlocks()
    stop = moveBlocks(holder, fakeApi(3, 0))
    pointer(handle(holder), 'pointerdown', 10)
    pointer(window, 'pointermove', 90)
    pointer(window, 'pointerup', 90)
    const click = new MouseEvent('click', { bubbles: true, cancelable: true })
    handle(holder).dispatchEvent(click)
    expect(click.defaultPrevented).toBe(true)
  })

  it('leaves a short press to the block menu and a cancelled drag without a move', () => {
    const holder = holderWithBlocks()
    const api = fakeApi(3, 0)
    stop = moveBlocks(holder, api)

    pointer(handle(holder), 'pointerdown', 10)
    pointer(window, 'pointermove', 10 + dragThreshold - 1)
    pointer(window, 'pointerup', 10 + dragThreshold - 1)
    expect(api.toolbar.toggleBlockSettings).not.toHaveBeenCalled()

    pointer(handle(holder), 'pointerdown', 10)
    pointer(window, 'pointermove', 90)
    pointer(window, 'pointercancel', 90)
    expect(api.blocks.move).not.toHaveBeenCalled()
  })

  it('does not drag from anything but the handle', () => {
    const holder = holderWithBlocks()
    const api = fakeApi(3, 0)
    stop = moveBlocks(holder, api)
    pointer(holder.querySelector('.ce-block')!, 'pointerdown', 10)
    pointer(window, 'pointermove', 90)
    pointer(window, 'pointerup', 90)
    expect(api.blocks.move).not.toHaveBeenCalled()
  })
})
