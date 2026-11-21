// Caret inside an editable block as a text offset (E05-01, ADR-0026): undo and redo put the caret back where the
// restored change was. The offset counts characters of the block's text, so it survives re-rendering the markup.

/** Characters of `root`'s text before the caret, or null when the selection is not inside `root`. */
export function caretOffsetIn(root: HTMLElement, selection: Selection | null): number | null {
  if (!selection || selection.rangeCount === 0) return null
  const { focusNode, focusOffset } = selection
  if (!focusNode || !root.contains(focusNode)) return null
  const range = root.ownerDocument.createRange()
  range.selectNodeContents(root)
  range.setEnd(focusNode, focusOffset)
  return range.toString().length
}

/** Puts a collapsed caret `offset` characters into `root`'s text (clamped to its end) and focuses it. */
export function placeCaret(root: HTMLElement, offset: number): void {
  const doc = root.ownerDocument
  const selection = doc.defaultView?.getSelection()
  if (!selection) return
  const range = doc.createRange()
  const walker = doc.createTreeWalker(root, NodeFilter.SHOW_TEXT)
  let left = Math.max(0, offset)
  let placed = false
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    const length = node.textContent?.length ?? 0
    if (left <= length) {
      range.setStart(node, left)
      placed = true
      break
    }
    left -= length
  }
  if (!placed) {
    range.selectNodeContents(root)
    range.collapse(false)
  }
  range.collapse(true)
  root.focus()
  selection.removeAllRanges()
  selection.addRange(range)
}
