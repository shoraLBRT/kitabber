// Undo and redo of the block editor (E05-01, ADR-0026). Editor.js has no history of its own, and the browser's undo
// of a contenteditable works inside one block only, so the editor keeps snapshots of the whole document: undo and
// redo cross blocks (a new block, a removed block, text typed in another block). Typing in one block is merged into
// one step while it goes on without a pause; any other change (blocks added, removed, reordered, or a different
// block edited) is a step of its own.
import { sameDocument, type PostDocument } from './document'

/** Where the caret was: the block's index in the document and the text offset inside it. */
export interface Caret {
  block: number
  offset: number
}

export interface Snapshot {
  document: PostDocument
  caret: Caret | null
}

export interface HistoryOptions {
  /** Milliseconds now; a clock the tests control (docs/TESTING.md: time is injected). */
  now?: () => number
  /** Typing in the same block with pauses shorter than this is one step. */
  mergeWindowMs?: number
  /** Steps kept; the oldest go first. */
  limit?: number
}

export class EditorHistory {
  private readonly entries: Snapshot[]
  private index = 0
  private lastRecordedAt = Number.NEGATIVE_INFINITY
  /** The block the last step typed in, while it may still grow; null after a structural step or undo/redo. */
  private typingBlock: string | null = null
  private readonly now: () => number
  private readonly mergeWindowMs: number
  private readonly limit: number

  constructor(initial: Snapshot, options: HistoryOptions = {}) {
    this.entries = [clone(initial)]
    this.now = options.now ?? (() => Date.now())
    this.mergeWindowMs = options.mergeWindowMs ?? 1000
    this.limit = Math.max(2, options.limit ?? 100)
  }

  get current(): Snapshot {
    return clone(this.entries[this.index]!)
  }

  /** Steps that can be undone from here: 0 at the start. */
  get position(): number {
    return this.index
  }

  get canUndo(): boolean {
    return this.index > 0
  }

  get canRedo(): boolean {
    return this.index < this.entries.length - 1
  }

  /** Records the document after a change. Returns false when nothing changed (e.g. the echo of an undo). */
  record(snapshot: Snapshot): boolean {
    const top = this.entries[this.index]!
    if (sameDocument(top.document, snapshot.document)) {
      top.caret = snapshot.caret ? { ...snapshot.caret } : top.caret
      return false
    }
    const at = this.now()
    const edited = singleEditedBlock(top.document, snapshot.document)
    const merge =
      this.index > 0 &&
      edited !== null &&
      edited === this.typingBlock &&
      at - this.lastRecordedAt < this.mergeWindowMs
    this.entries.splice(this.index + 1)
    if (merge) {
      this.entries[this.index] = clone(snapshot)
    } else {
      this.entries.push(clone(snapshot))
      this.index++
      if (this.entries.length > this.limit) {
        this.entries.shift()
        this.index--
      }
    }
    this.typingBlock = edited
    this.lastRecordedAt = at
    return true
  }

  /** Steps back; returns the snapshot to show, or null at the start. */
  undo(): Snapshot | null {
    if (!this.canUndo) return null
    this.index--
    this.typingBlock = null
    return this.current
  }

  /** Steps forward again; returns the snapshot to show, or null at the end. */
  redo(): Snapshot | null {
    if (!this.canRedo) return null
    this.index++
    this.typingBlock = null
    return this.current
  }
}

/** The id of the one block whose data changed, when the blocks are otherwise the same and in the same order. */
function singleEditedBlock(before: PostDocument, after: PostDocument): string | null {
  if (before.blocks.length !== after.blocks.length) return null
  let edited: string | null = null
  for (let i = 0; i < before.blocks.length; i++) {
    const a = before.blocks[i]!
    const b = after.blocks[i]!
    if (a.id !== b.id || a.type !== b.type) return null
    if (JSON.stringify(a.data) !== JSON.stringify(b.data)) {
      if (edited !== null) return null
      edited = a.id
    }
  }
  return edited
}

function clone(snapshot: Snapshot): Snapshot {
  return JSON.parse(JSON.stringify(snapshot)) as Snapshot
}
