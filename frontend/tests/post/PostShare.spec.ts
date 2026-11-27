import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import PostShare from '../../components/post/PostShare.vue'
import { useToast } from '../../composables/useToast'
import { canonicalUrl, networkShareUrl, shareItems, usesSystemShare } from '../../utils/share'

// «Поделиться» of a post (E07-07, ADR-0103): the phone's system share menu, the list with copying the link and the
// social networks on a wide screen, and the canonical link in all of them. The page is in e2e/post-share.spec.ts.
const path = '/u/anna/0195f3a0-0000-7000-8000-0000000000c1-privet-mir'
const link = `${window.location.origin}${path}`

const screen = (phone: boolean) =>
  vi.spyOn(window, 'matchMedia').mockImplementation(
    (query) =>
      ({
        matches: phone,
        media: query,
        addEventListener: () => {},
        removeEventListener: () => {},
      }) as unknown as MediaQueryList,
  )

const withShare = (share: Navigator['share'] | undefined) =>
  Object.defineProperty(navigator, 'share', { value: share, configurable: true })

const withClipboard = () => {
  const writeText = vi.fn().mockResolvedValue(undefined)
  Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true })
  return writeText
}

let wrapper: ReturnType<typeof mount>
const mountShare = async (title: string | null = 'Привет, мир') => {
  wrapper = mount(PostShare, { props: { path, title }, attachTo: document.body })
  await flushPromises()
  return wrapper
}
const press = async () => {
  await wrapper.get('button').trigger('click')
  await flushPromises()
}

afterEach(() => {
  wrapper?.unmount()
  vi.restoreAllMocks()
  withShare(undefined)
  useToast().clear()
})

describe('the share link', () => {
  it('is the canonical address without the query and the hash', () => {
    expect(canonicalUrl(`${path}?from=feed#comment-1`, 'https://site.test')).toBe(`https://site.test${path}`)
    expect(canonicalUrl(path, 'https://site.test')).toBe(`https://site.test${path}`)
  })

  it('goes into the share address of each network with the title', () => {
    const url = 'https://site.test/u/anna/1-a'
    const encoded = encodeURIComponent(url)
    expect(networkShareUrl('telegram', url, 'Привет')).toBe(
      `https://t.me/share/url?url=${encoded}&text=${encodeURIComponent('Привет')}`,
    )
    expect(networkShareUrl('vk', url, 'Привет')).toBe(
      `https://vk.com/share.php?url=${encoded}&title=${encodeURIComponent('Привет')}`,
    )
    // A short post has no title: the address takes the link only.
    expect(networkShareUrl('x', url, '')).toBe(`https://x.com/intent/post?url=${encoded}`)
  })

  it('lists copying first, then the networks', () => {
    expect(shareItems().map((item) => item.label)).toEqual([
      'Скопировать ссылку',
      'Telegram',
      'ВКонтакте',
      'X',
    ])
  })
})

describe('the system menu', () => {
  it('is used on a phone whose browser has the Web Share API, and nowhere else', () => {
    screen(true)
    withShare(undefined)
    expect(usesSystemShare()).toBe(false)
    withShare(vi.fn())
    expect(usesSystemShare()).toBe(true)
    screen(false)
    expect(usesSystemShare()).toBe(false)
  })

  it('opens on a phone with the canonical link and the title', async () => {
    screen(true)
    const share = vi.fn().mockResolvedValue(undefined)
    withShare(share)
    await mountShare()
    expect(wrapper.find('[aria-haspopup="menu"]').exists()).toBe(false)
    await press()
    expect(share).toHaveBeenCalledWith({ title: 'Привет, мир', url: link })
    expect(wrapper.find('[role="menu"]').exists()).toBe(false)
  })

  it('takes the link only for a post without a title', async () => {
    screen(true)
    const share = vi.fn().mockResolvedValue(undefined)
    withShare(share)
    await mountShare(null)
    await press()
    expect(share).toHaveBeenCalledWith({ url: link })
  })

  it('says nothing when the person closes it', async () => {
    screen(true)
    withShare(vi.fn().mockRejectedValue(new DOMException('closed', 'AbortError')))
    const writeText = withClipboard()
    await mountShare()
    await press()
    expect(writeText).not.toHaveBeenCalled()
    expect(useToast().toasts).toHaveLength(0)
  })

  it('copies the link when the browser refuses it', async () => {
    screen(true)
    withShare(vi.fn().mockRejectedValue(new DOMException('denied', 'NotAllowedError')))
    const writeText = withClipboard()
    await mountShare()
    await press()
    expect(writeText).toHaveBeenCalledWith(link)
    expect(useToast().toasts.map((toast) => toast.message)).toEqual(['Ссылка скопирована'])
  })
})

describe('the list on a wide screen', () => {
  it('opens over the button even when the browser has the Web Share API', async () => {
    screen(false)
    const share = vi.fn()
    withShare(share)
    await mountShare()
    const trigger = wrapper.get('button')
    expect(trigger.attributes('aria-label')).toBe('Поделиться')
    await press()
    expect(share).not.toHaveBeenCalled()
    expect(wrapper.get('[role="menu"]').classes()).toContain('ui-menu__list--above')
    expect(wrapper.findAll('[role="menuitem"]').map((item) => item.text())).toEqual([
      'Скопировать ссылку',
      'Telegram',
      'ВКонтакте',
      'X',
    ])
  })

  it('copies the canonical link and says so', async () => {
    screen(false)
    const writeText = withClipboard()
    await mountShare()
    await press()
    await wrapper.get('[data-id="copy-link"]').trigger('click')
    await flushPromises()
    expect(writeText).toHaveBeenCalledWith(link)
    expect(useToast().toasts.map((toast) => toast.message)).toEqual(['Ссылка скопирована'])
    expect(wrapper.find('[role="menu"]').exists()).toBe(false)
  })

  it('says so when the link cannot be copied', async () => {
    screen(false)
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText: vi.fn().mockRejectedValue(new Error('denied')) },
      configurable: true,
    })
    await mountShare()
    await press()
    await wrapper.get('[data-id="copy-link"]').trigger('click')
    await flushPromises()
    expect(useToast().toasts.map((toast) => toast.message)).toEqual(['Не удалось скопировать ссылку'])
  })

  it('opens a network in a new tab, with no opener', async () => {
    screen(false)
    const open = vi.spyOn(window, 'open').mockReturnValue(null)
    await mountShare()
    await press()
    await wrapper.get('[data-id="telegram"]').trigger('click')
    expect(open).toHaveBeenCalledWith(
      networkShareUrl('telegram', link, 'Привет, мир'),
      '_blank',
      'noopener,noreferrer',
    )
  })
})
