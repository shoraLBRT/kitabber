import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import CommentTree from '../../components/comments/CommentTree.vue'
import { isCommentSort, withComment, type CommentSort, type CommentsState } from '../../api/comments'
import { commentSortKey, readCommentSort, saveCommentSort } from '../../utils/comment-sort'
import { comment } from './factory'

// The order of comments (E09-04, ADR-0058): the switch «Популярные | По дате» above the tree, the choice kept in the
// browser, and where a sent comment goes in each order. The server sorts; the page against the fake API is in
// e2e/comment-sort.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

function mountTree(state: CommentsState, sort?: CommentSort) {
  return mount(CommentTree, {
    props: { postId: 'p1', signedIn: false, state, authors: new Map(), branches: {}, sort },
    global: { stubs },
  })
}

const found: CommentsState = {
  status: 'found',
  maxDepth: 8,
  comments: [comment('1', null, 1, 1), comment('1.1', '1', 2), comment('2', null, 1)],
}

function memoryStorage(): Storage {
  const items = new Map<string, string>()
  return {
    getItem: (key) => items.get(key) ?? null,
    setItem: (key, value) => void items.set(key, value),
    removeItem: (key) => void items.delete(key),
    clear: () => items.clear(),
    key: (n) => [...items.keys()][n] ?? null,
    get length() {
      return items.size
    },
  }
}

describe('the sort switch', () => {
  it('names the group and presses «Популярные» by default', () => {
    const view = mountTree(found)
    const group = view.get('[data-testid="comments-sort"]')
    expect(group.attributes('role')).toBe('group')
    expect(group.attributes('aria-label')).toBe('Порядок комментариев')
    const options = group.findAll('button')
    expect(options.map((o) => o.text())).toEqual(['Популярные', 'По дате'])
    expect(options.map((o) => o.attributes('aria-pressed'))).toEqual(['true', 'false'])
  })

  it('presses the order given and asks for another one', async () => {
    const view = mountTree(found, 'date')
    expect(view.get('[data-testid="comments-sort-date"]').attributes('aria-pressed')).toBe('true')
    await view.get('[data-testid="comments-sort-popular"]').trigger('click')
    expect(view.emitted('sort')).toEqual([['popular']])
  })

  it('stays while the tree loads or fails, and is gone when there is nothing to order', () => {
    expect(mountTree({ status: 'loading' }).find('[data-testid="comments-sort"]').exists()).toBe(true)
    expect(mountTree({ status: 'error' }).find('[data-testid="comments-sort"]').exists()).toBe(true)
    const empty = mountTree({ status: 'found', comments: [], maxDepth: 8 })
    expect(empty.find('[data-testid="comments-sort"]').exists()).toBe(false)
  })
})

describe('the remembered order', () => {
  it('is «Популярные» until the reader chooses', () => {
    expect(readCommentSort(memoryStorage())).toBe('popular')
    expect(readCommentSort(undefined)).toBe('popular')
  })

  it('keeps the choice for the next posts', () => {
    const storage = memoryStorage()
    saveCommentSort(storage, 'date')
    expect(storage.getItem(commentSortKey)).toBe('date')
    expect(readCommentSort(storage)).toBe('date')
  })

  it('ignores a value it does not know and a storage that throws', () => {
    const storage = memoryStorage()
    storage.setItem(commentSortKey, 'rating')
    expect(readCommentSort(storage)).toBe('popular')
    const blocked = {
      getItem: () => {
        throw new Error('blocked')
      },
      setItem: () => {
        throw new Error('blocked')
      },
    } as unknown as Storage
    expect(readCommentSort(blocked)).toBe('popular')
    expect(() => saveCommentSort(blocked, 'date')).not.toThrow()
  })

  it('knows only its two orders', () => {
    expect(['popular', 'date', 'Date', null].map(isCommentSort)).toEqual([true, true, false, false])
  })
})

describe('a sent comment', () => {
  const tree = [comment('1', null, 1, 1), comment('1.1', '1', 2), comment('2', null, 1)]

  it('goes to the end of «Популярные», where a thread with no replies stands', () => {
    expect(withComment(tree, comment('3', null, 1), 'popular').map((c) => c.id)).toEqual([
      '1',
      '1.1',
      '2',
      '3',
    ])
  })

  it('goes to the top of «По дате», as the newest', () => {
    expect(withComment(tree, comment('3', null, 1), 'date').map((c) => c.id)).toEqual(['3', '1', '1.1', '2'])
  })

  it('as a reply stays under its parent in either order', () => {
    expect(withComment(tree, comment('1.2', '1', 2), 'date').map((c) => c.id)).toEqual([
      '1',
      '1.1',
      '1.2',
      '2',
    ])
  })
})
