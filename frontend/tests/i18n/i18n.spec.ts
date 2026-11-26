import { readdirSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import { h } from 'vue'
import brand from '../../../brand/brand.json'
import { createI18n, type Dictionary, type PartialDictionary } from '../../i18n/core'
import {
  defaultLocale,
  dictionaries,
  formatDate,
  formatNumber,
  i18n,
  locale,
  t,
  type Messages,
} from '../../i18n'
import I18nText from '../../i18n/I18nText'
import ru from '../../i18n/locales/ru'

// The interface strings (E02-10, ADR-0063): the dictionary of the locale, plural forms, the fallback of a second
// locale to Russian, dates and numbers by the locale. The lint check of the templates: tests/i18n/lint.spec.ts.

describe('the dictionary of the app', () => {
  it('is Russian, and the page and Intl use that locale', () => {
    expect(locale).toBe('ru')
    expect(defaultLocale).toBe('ru')
    expect(i18n.locale).toBe('ru')
  })

  it('lists every dictionary file of i18n/locales/, so a second language is its file and its line', () => {
    const files = readdirSync(join(dirname(fileURLToPath(import.meta.url)), '../../i18n/locales'))
      .filter((file) => file.endsWith('.ts') && file !== 'index.ts')
      .map((file) => file.replace(/\.ts$/, ''))
    expect(Object.keys(dictionaries).sort()).toEqual(files.sort())
    expect(dictionaries.ru).toBe(ru)
  })

  it('gives the text of a key, its slots filled', () => {
    expect(t('common.retry')).toBe('Повторить')
    expect(t('shell.accountMenu', { name: 'Анна' })).toBe('Меню профиля: Анна')
  })

  it('picks the Russian plural form by the count', () => {
    expect(t('comments.replies', { count: 1 })).toBe('1 ответ')
    expect(t('comments.replies', { count: 3 })).toBe('3 ответа')
    expect(t('comments.replies', { count: 5 })).toBe('5 ответов')
    expect(t('comments.replies', { count: 11 })).toBe('11 ответов')
    expect(t('comments.replies', { count: 21 })).toBe('21 ответ')
    expect(t('comments.replies', { count: 1.5 })).toBe('1,5 ответа')
  })

  it('writes numbers and dates in Russian', () => {
    expect(formatNumber(1234567)).toBe('1 234 567')
    expect(t('comments.replies', { count: 1000 })).toBe('1 000 ответов')
    expect(formatDate('2026-03-08T10:00:00Z', { day: 'numeric', month: 'long', timeZone: 'UTC' })).toBe(
      '8 марта',
    )
  })

  it('has no empty text and no plural without the Russian forms', () => {
    const walk = (node: Dictionary, path: string): void => {
      for (const [key, value] of Object.entries(node)) {
        const at = `${path}${key}`
        if (typeof value === 'string') expect(value.trim(), at).not.toBe('')
        else if ('other' in value && typeof value.other === 'string')
          expect(Object.keys(value).sort(), at).toEqual(['few', 'many', 'one', 'other'])
        else walk(value as Dictionary, `${at}.`)
      }
    }
    walk(ru, '')
  })

  it('rejects a key the dictionary does not have at type check', () => {
    // @ts-expect-error: not a key of the dictionary
    expect(t('common.nope')).toBe('common.nope')
  })
})

describe('a second locale', () => {
  const en = {
    common: { retry: 'Retry' },
    comments: { replies: { one: '{count} reply', other: '{count} replies' } },
  } satisfies PartialDictionary<Messages>
  const english = createI18n<Messages>({
    locale: 'en',
    fallbackLocale: 'ru',
    dictionaries: { ru, en },
    globals: { brand: brand.name },
  })

  it('takes its own texts and plural rules', () => {
    expect(english.t('common.retry')).toBe('Retry')
    expect(english.t('comments.replies', { count: 1 })).toBe('1 reply')
    expect(english.t('comments.replies', { count: 3 })).toBe('3 replies')
    expect(english.t('comments.replies', { count: 1000 })).toBe('1,000 replies')
  })

  it('falls back to Russian for a text it leaves out', () => {
    expect(english.t('common.close')).toBe('Закрыть')
  })

  it('formats numbers and dates by its locale', () => {
    expect(english.formatNumber(1234.5)).toBe('1,234.5')
    expect(
      english.formatDate('2026-03-08T10:00:00Z', { day: 'numeric', month: 'long', timeZone: 'UTC' }),
    ).toBe('March 8')
  })

  it('fills the product name from brand/brand.json and keeps an unknown slot', () => {
    const own = createI18n<Dictionary>({
      locale: 'ru',
      fallbackLocale: 'ru',
      dictionaries: { ru: { about: 'О {brand}', slot: 'Привет, {name}' } },
      globals: { brand: brand.name },
    })
    expect(own.t('about' as never)).toBe(`О ${brand.name}`)
    expect(own.t('slot' as never)).toBe('Привет, {name}')
    expect(own.t('missing' as never)).toBe('missing')
  })
})

describe('I18nText', () => {
  it('draws the markup of a slot inside the text of the dictionary', () => {
    const wrapper = mount(I18nText, {
      props: { keypath: 'auth.codeSent', tag: 'p', params: { minutes: 10 } },
      attrs: { class: 'lead' },
      slots: { email: () => h('strong', 'anna@example.org') },
    })
    expect(wrapper.element.tagName).toBe('P')
    expect(wrapper.classes()).toContain('lead')
    expect(wrapper.find('strong').text()).toBe('anna@example.org')
    expect(wrapper.text()).toBe('Отправили код на anna@example.org. Он действует 10 минут.')
  })
})
