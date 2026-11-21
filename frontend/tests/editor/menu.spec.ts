import { afterEach, describe, expect, it, vi } from 'vitest'
import { filterMenu, matchesQuery, searchOnPhone } from '../../editor/menu'

// The search of the block menu on a phone (E05-07, #41).

const titles = ['Текст', 'Заголовок', 'Маркированный список', 'Нумерованный список', 'Цитата']

/** An editor holder with the block menu of Editor.js, closed or open. */
function holderWithMenu(opened: boolean) {
  const holder = document.createElement('div')
  holder.innerHTML = `
    <div class="ce-toolbox"><div class="ce-popover${opened ? ' ce-popover--opened' : ''}">
      <div class="ce-popover__container">
        <div class="ce-popover__nothing-found-message">Ничего не найдено</div>
        <div class="ce-popover__items">${titles
          .map(
            (title) =>
              `<div class="ce-popover-item"><div class="ce-popover-item__title">${title}</div></div>`,
          )
          .join('')}</div>
      </div>
    </div></div>`
  document.body.append(holder)
  return holder
}

const shown = (holder: HTMLElement) =>
  Array.from(holder.querySelectorAll('.ce-popover-item:not(.ce-popover-item--hidden)')).map(
    (item) => item.textContent,
  )
const nothingFound = (holder: HTMLElement) =>
  holder
    .querySelector('.ce-popover__nothing-found-message')!
    .classList.contains('ce-popover__nothing-found-message--displayed')

function phone(matches: boolean) {
  vi.stubGlobal('matchMedia', (query: string) => ({ matches, media: query }) as MediaQueryList)
}

const flush = () => new Promise((resolve) => setTimeout(resolve, 0))

let stop: (() => void) | null = null

afterEach(() => {
  stop?.()
  stop = null
  vi.unstubAllGlobals()
  document.body.replaceChildren()
})

describe('matchesQuery', () => {
  it('finds a block by a part of its name in any letter case', () => {
    expect(matchesQuery('Нумерованный список', 'СПИС')).toBe(true)
    expect(matchesQuery('Цитата', ' цит ')).toBe(true)
    expect(matchesQuery('Цитата', 'список')).toBe(false)
    expect(matchesQuery('Текст', '')).toBe(true)
  })
})

describe('filterMenu', () => {
  it('hides the blocks that do not match and shows «Ничего не найдено» when none does', () => {
    const holder = holderWithMenu(true)
    const container = holder.querySelector<HTMLElement>('.ce-popover__container')!
    filterMenu(container, 'спис')
    expect(shown(holder)).toEqual(['Маркированный список', 'Нумерованный список'])
    expect(nothingFound(holder)).toBe(false)
    filterMenu(container, 'нет такого')
    expect(shown(holder)).toEqual([])
    expect(nothingFound(holder)).toBe(true)
    filterMenu(container, '')
    expect(shown(holder)).toEqual(titles)
    expect(nothingFound(holder)).toBe(false)
  })
})

describe('searchOnPhone', () => {
  it('adds the search to the open menu on a phone, filters it and adds the first block found on Enter', async () => {
    phone(true)
    const holder = holderWithMenu(false)
    stop = searchOnPhone(holder, 'Поиск')
    holder.querySelector('.ce-popover')!.classList.add('ce-popover--opened')
    await flush()

    const input = holder.querySelector<HTMLInputElement>('.block-menu-search .cdx-search-field__input')!
    expect(input.placeholder).toBe('Поиск')
    expect(input.getAttribute('aria-label')).toBe('Поиск')
    expect(document.activeElement).not.toBe(input)

    input.value = 'цит'
    input.dispatchEvent(new Event('input'))
    expect(shown(holder)).toEqual(['Цитата'])
    const clicked = vi.fn()
    holder.querySelectorAll('.ce-popover-item')[4]!.addEventListener('click', clicked)
    const enter = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true })
    input.dispatchEvent(enter)
    expect(enter.defaultPrevented).toBe(true)
    expect(clicked).toHaveBeenCalledOnce()
  })

  it('opens the menu again with all its blocks and an empty field', async () => {
    phone(true)
    const holder = holderWithMenu(true)
    stop = searchOnPhone(holder, 'Поиск')
    holder.querySelector('.ce-popover__items')!.append(document.createElement('span'))
    await flush()
    const input = holder.querySelector<HTMLInputElement>('.block-menu-search input')!
    input.value = 'zzz'
    input.dispatchEvent(new Event('input'))
    expect(shown(holder)).toEqual([])

    holder.querySelector('.ce-popover')!.classList.remove('ce-popover--opened')
    await flush()
    expect(input.value).toBe('')
    expect(shown(holder)).toEqual(titles)
    expect(nothingFound(holder)).toBe(false)
  })

  it('leaves the desktop menu to Editor.js, which has its own search', async () => {
    phone(false)
    const holder = holderWithMenu(false)
    stop = searchOnPhone(holder, 'Поиск')
    holder.querySelector('.ce-popover')!.classList.add('ce-popover--opened')
    await flush()
    expect(holder.querySelector('.block-menu-search')).toBeNull()
  })
})
