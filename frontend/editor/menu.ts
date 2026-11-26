// The search of the block menu on a phone (E05-07, ADR-0039). On a desktop Editor.js gives its block menu «+» a search
// field that takes the focus, filters the blocks by name and lets the arrows and Enter pick one. On a phone (its own
// 650 px breakpoint) it shows the menu as a sheet at the bottom of the screen with no search; here the sheet gets the
// same field, with the same markup, and filters the blocks the same way. It does not take the focus by itself, so
// opening the sheet does not move the caret; Enter in it adds the first block found.

import { locale } from '../i18n'

/** The menu «+» of Editor.js while it is open. */
const openMenu = '.ce-toolbox .ce-popover--opened'
const searchClass = 'cdx-search-field'
const hiddenItem = 'ce-popover-item--hidden'
const nothingFoundShown = 'ce-popover__nothing-found-message--displayed'

const searchIcon =
  '<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="11" cy="11" r="7"/><path d="m21 21-4.3-4.3"/></svg>'

/** True when a block named `title` matches the search `query`: its name contains the query, letter case aside. */
export function matchesQuery(title: string, query: string): boolean {
  return title.toLocaleLowerCase(locale).includes(query.trim().toLocaleLowerCase(locale))
}

/** Shows the blocks of the menu that match query and the «Ничего не найдено» line when none does. */
export function filterMenu(container: HTMLElement, query: string) {
  let found = 0
  container.querySelectorAll<HTMLElement>('.ce-popover-item').forEach((item) => {
    const title = item.querySelector('.ce-popover-item__title')?.textContent ?? ''
    const match = matchesQuery(title, query)
    item.classList.toggle(hiddenItem, !match)
    if (match) found++
  })
  container
    .querySelector('.ce-popover__nothing-found-message')
    ?.classList.toggle(nothingFoundShown, found === 0)
}

/** Adds the search to the phone's block menu in the editor at holder; returns the function that stops it. */
export function searchOnPhone(holder: HTMLElement, placeholder: string): () => void {
  const isPhone = () => window.matchMedia('(max-width: 650px)').matches

  const build = (container: HTMLElement) => {
    const field = document.createElement('div')
    field.classList.add(searchClass, 'block-menu-search')
    field.innerHTML = `<div class="${searchClass}__icon">${searchIcon}</div>`
    const input = document.createElement('input')
    input.classList.add(`${searchClass}__input`)
    input.type = 'search'
    input.placeholder = placeholder
    input.enterKeyHint = 'go'
    input.setAttribute('aria-label', placeholder)
    input.addEventListener('input', () => filterMenu(container, input.value))
    input.addEventListener('keydown', (event) => {
      if (event.key !== 'Enter') return
      event.preventDefault()
      event.stopPropagation()
      container.querySelector<HTMLElement>(`.ce-popover-item:not(.${hiddenItem})`)?.click()
    })
    field.append(input)
    container.prepend(field)
  }

  const sync = () => {
    const container = holder.querySelector<HTMLElement>('.ce-toolbox .ce-popover > .ce-popover__container')
    if (!container) return
    const opened = !!holder.querySelector(openMenu)
    const field = container.querySelector<HTMLInputElement>(`.block-menu-search input`)
    if (opened && isPhone() && !field) build(container)
    // A closed menu opens next time with all its blocks and an empty field.
    if (!opened && field && (field.value !== '' || container.querySelector(`.${hiddenItem}`))) {
      field.value = ''
      filterMenu(container, '')
      container.querySelector('.ce-popover__nothing-found-message')?.classList.remove(nothingFoundShown)
    }
  }

  const observer = new MutationObserver(sync)
  observer.observe(holder, { subtree: true, attributes: true, attributeFilter: ['class'], childList: true })
  return () => observer.disconnect()
}
