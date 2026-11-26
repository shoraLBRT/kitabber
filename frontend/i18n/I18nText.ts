// A text of the dictionary with markup in its slots (E02-10, ADR-0063): `Отправили код на {email}.` with the address in
// <strong>. Each `{name}` is drawn by the slot of that name, or by the `params` value; the words around stay in the
// dictionary, so a translation may move the slot.
import { defineComponent, h, type PropType } from 'vue'
import { t, type MessageKey, type Messages, type MessageParams } from './index'

export default defineComponent({
  name: 'I18nText',
  props: {
    keypath: { type: String as PropType<MessageKey<Messages>>, required: true },
    tag: { type: String, default: 'span' },
    params: { type: Object as PropType<MessageParams>, default: () => ({}) },
  },
  setup(props, { slots }) {
    return () => {
      const slotNames = Object.keys(slots)
      // Slots with markup are kept as `{name}`; the rest are filled as text.
      const text = t(props.keypath, {
        ...props.params,
        ...Object.fromEntries(slotNames.map((name) => [name, `{${name}}`])),
      })
      const parts = text.split(/\{(\w+)\}/)
      const children = parts.map((part, index) => (index % 2 === 1 ? (slots[part]?.() ?? `{${part}}`) : part))
      return h(props.tag, children)
    }
  },
})
