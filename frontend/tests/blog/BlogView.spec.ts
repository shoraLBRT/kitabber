import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import BlogView from '../../components/blog/BlogView.vue'
import BlogTabEmpty from '../../components/blog/BlogTabEmpty.vue'
import type { Profile, ProfileLookup } from '../../api/profiles'

// The blog page's header, tabs and states (E04-02, docs/ui/blog.md); the server render, the addresses and the owner
// against the fake API are in e2e/blog.spec.ts.
const stubs = { NuxtLink: { props: ['to'], template: '<a :href="to"><slot /></a>' } }

const anna: Profile = {
  id: '0195f3a0-0000-7000-8000-0000000000b1',
  type: 'personal',
  name: 'Анна',
  nickname: 'anna',
  description: 'Пишу о книгах и городах.',
  registeredAt: null,
}

function mountView(state: ProfileLookup | { status: 'loading' }, tab: 'posts' | 'comments' = 'posts') {
  return mount(BlogView, {
    props: { state, base: '/u/anna', tab },
    slots: { default: '<p data-testid="tab-content">содержимое</p>' },
    global: { stubs },
  })
}

describe('BlogView', () => {
  it('shows the header of a blog to a visitor: name, nickname, description, no owner controls', () => {
    const wrapper = mountView({ status: 'found', profile: anna, owner: false })
    expect(wrapper.get('header h1').text()).toBe('Анна')
    expect(wrapper.get('[data-testid="blog-nickname"]').text()).toBe('@anna')
    expect(wrapper.text()).toContain('Пишу о книгах и городах.')
    expect(wrapper.find('[data-testid="blog-registered"]').exists()).toBe(false)
    expect(wrapper.find('[aria-label="Управление блогом"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="tab-content"]').text()).toBe('содержимое')
  })

  it('shows the owner the registration date and the management buttons', () => {
    const profile = { ...anna, registeredAt: '2026-03-14T23:30:00Z' }
    const wrapper = mountView({ status: 'found', profile, owner: true })
    expect(wrapper.get('[data-testid="blog-registered"]').text()).toBe('На сайте с 14 марта 2026 г.')
    const buttons = wrapper.get('[aria-label="Управление блогом"]').findAll('button')
    expect(buttons.map((button) => button.text())).toEqual(['Ред.', 'Статистика', 'Настройки'])
    // Their screens come with their own issues; until then the buttons say so instead of leading nowhere.
    expect(buttons.every((button) => button.attributes('disabled') !== undefined)).toBe(true)
    expect(wrapper.get('#blog-soon').text()).toContain('появятся позже')
  })

  it('leaves out what a new blog does not have yet', () => {
    const profile = { ...anna, name: null, nickname: null, description: null }
    const wrapper = mountView({ status: 'found', profile, owner: false })
    expect(wrapper.get('h1').text()).toBe('Блог без названия')
    expect(wrapper.find('[data-testid="blog-nickname"]').exists()).toBe(false)
    expect(wrapper.find('.blog__description').exists()).toBe(false)

    const nicknameOnly = mountView({
      status: 'found',
      profile: { ...profile, nickname: 'anna' },
      owner: false,
    })
    expect(nicknameOnly.get('h1').text()).toBe('@anna')
  })

  it('links the tabs to their addresses and marks the current one', () => {
    const wrapper = mountView({ status: 'found', profile: anna, owner: false }, 'comments')
    const links = wrapper.get('nav[aria-label="Разделы блога"]').findAll('a')
    expect(links.map((link) => [link.text(), link.attributes('href')])).toEqual([
      ['Посты', '/u/anna'],
      ['Комментарии', '/u/anna/comments'],
    ])
    expect(links.map((link) => link.attributes('aria-current'))).toEqual([undefined, 'page'])
    expect(wrapper.get('.blog__panel').attributes('aria-label')).toBe('Комментарии')
  })

  it('shows a skeleton while loading', () => {
    const wrapper = mountView({ status: 'loading' })
    const loading = wrapper.get('[data-testid="blog-loading"]')
    expect(loading.attributes('aria-busy')).toBe('true')
    expect(loading.get('[role="status"]').text()).toBe('Загрузка блога')
    expect(wrapper.findAll('.blog__skeleton--card')).toHaveLength(3)
    expect(wrapper.find('h1').exists()).toBe(false)
  })

  it('says when the blog is not found', () => {
    const wrapper = mountView({ status: 'not-found' })
    expect(wrapper.get('h1').text()).toBe('Блог не найден')
    expect(wrapper.get('a').attributes('href')).toBe('/')
    expect(wrapper.find('[data-testid="tab-content"]').exists()).toBe(false)
  })

  it('offers a retry when loading failed', async () => {
    const wrapper = mountView({ status: 'error' })
    expect(wrapper.get('h1').text()).toBe('Не удалось загрузить блог')
    expect(wrapper.find('[role="alert"]').exists()).toBe(true)
    await wrapper.get('button').trigger('click')
    expect(wrapper.emitted('retry')).toHaveLength(1)
  })
})

describe('BlogTabEmpty', () => {
  it('says the posts tab is empty and offers the owner the first post', () => {
    const visitor = mount(BlogTabEmpty, { props: { tab: 'posts', owner: false }, global: { stubs } })
    expect(visitor.text()).toBe('Здесь пока нет постов')
    const owner = mount(BlogTabEmpty, { props: { tab: 'posts', owner: true }, global: { stubs } })
    expect(owner.get('a').text()).toBe('Написать первый пост')
    expect(owner.get('a').attributes('href')).toBe('/write')
  })

  it('says the comments tab is empty', () => {
    const wrapper = mount(BlogTabEmpty, { props: { tab: 'comments', owner: true }, global: { stubs } })
    expect(wrapper.text()).toBe('Пока нет комментариев')
    expect(wrapper.find('a').exists()).toBe(false)
  })
})
