import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import UiField from '../../components/ui/UiField.vue'

describe('UiField', () => {
  it('ties the visible label to the control', () => {
    const wrapper = mount(UiField, { props: { label: 'Имя' } })
    const input = wrapper.get('input')
    expect(wrapper.get('label').attributes('for')).toBe(input.attributes('id'))
    expect(input.attributes('type')).toBe('text')
  })

  it('works with v-model', async () => {
    const wrapper = mount(UiField, {
      props: {
        label: 'Имя',
        modelValue: 'Анна',
        'onUpdate:modelValue': (v: string) => wrapper.setProps({ modelValue: v }),
      },
    })
    expect(wrapper.get('input').element.value).toBe('Анна')
    await wrapper.get('input').setValue('Борис')
    expect(wrapper.emitted('update:modelValue')!.at(-1)).toEqual(['Борис'])
  })

  it('describes the control by the hint and marks an error with aria-invalid', () => {
    const wrapper = mount(UiField, { props: { label: 'Почта', hint: 'Для входа', error: 'Неверный адрес' } })
    const input = wrapper.get('input')
    const ids = input.attributes('aria-describedby')!.split(' ')
    expect(ids.map((id) => wrapper.get(`[id="${id}"]`).text())).toEqual(['Для входа', 'Неверный адрес'])
    expect(input.attributes('aria-invalid')).toBe('true')
  })

  it('has no aria-invalid or description without a hint and an error', () => {
    const input = mount(UiField, { props: { label: 'Имя' } }).get('input')
    expect(input.attributes('aria-invalid')).toBeUndefined()
    expect(input.attributes('aria-describedby')).toBeUndefined()
  })

  it('renders a textarea when multiline and passes other attributes to the control', () => {
    const wrapper = mount(UiField, {
      props: { label: 'О себе', multiline: true, required: true },
      attrs: { maxlength: '300' },
    })
    const textarea = wrapper.get('textarea')
    expect(textarea.attributes('maxlength')).toBe('300')
    expect(textarea.element.required).toBe(true)
    expect(wrapper.attributes('maxlength')).toBeUndefined()
  })

  it('gives each field of a page its own id', () => {
    const page = defineComponent({ render: () => [h(UiField, { label: 'А' }), h(UiField, { label: 'Б' })] })
    const ids = mount(page)
      .findAll('input')
      .map((input) => input.attributes('id'))
    expect(new Set(ids).size).toBe(2)
  })
})
