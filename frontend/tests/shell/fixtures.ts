// Test doubles of the shell tests: NuxtLink as a plain link, so components mount without Nuxt.
import { defineComponent, h } from 'vue'

export const NuxtLink = defineComponent({
  name: 'NuxtLink',
  props: { to: { type: String, required: true } },
  setup(props, { slots }) {
    return () => h('a', { href: props.to }, slots.default?.())
  },
})

export const global = { components: { NuxtLink }, stubs: { teleport: true } }
