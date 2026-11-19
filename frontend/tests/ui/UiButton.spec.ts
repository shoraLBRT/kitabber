import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import UiButton from '../../components/ui/UiButton.vue'

describe('UiButton', () => {
  it('is a native button of type "button" that emits click', async () => {
    const wrapper = mount(UiButton, { slots: { default: 'Опубликовать' } })
    const button = wrapper.get('button')
    expect(button.attributes('type')).toBe('button')
    expect(button.text()).toBe('Опубликовать')
    await button.trigger('click')
    expect(wrapper.emitted('click')).toHaveLength(1)
  })

  it('shows the variant and size as classes', () => {
    const wrapper = mount(UiButton, { props: { variant: 'danger', size: 'sm', type: 'submit' } })
    expect(wrapper.classes()).toEqual(expect.arrayContaining(['ui-button--danger', 'ui-button--sm']))
    expect(wrapper.attributes('type')).toBe('submit')
  })

  it('while loading stays focusable, announces busy and ignores clicks', async () => {
    const wrapper = mount(UiButton, { props: { loading: true }, slots: { default: 'Сохранить' } })
    const button = wrapper.get('button')
    expect(button.attributes('disabled')).toBeUndefined()
    expect(button.attributes('aria-busy')).toBe('true')
    expect(button.attributes('aria-disabled')).toBe('true')
    expect(wrapper.find('.ui-button__spinner').exists()).toBe(true)
    await button.trigger('click')
    expect(wrapper.emitted('click')).toBeUndefined()
  })

  it('when disabled is a disabled native button', () => {
    const wrapper = mount(UiButton, { props: { disabled: true } })
    expect(wrapper.get('button').element.disabled).toBe(true)
  })

  it('takes an accessible name for an icon-only button', () => {
    const wrapper = mount(UiButton, {
      attrs: { 'aria-label': 'Написать' },
      slots: { icon: '<svg aria-hidden="true" />' },
    })
    expect(wrapper.attributes('aria-label')).toBe('Написать')
    expect(wrapper.find('.ui-button__label').exists()).toBe(false)
  })
})
