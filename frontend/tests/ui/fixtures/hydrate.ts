// Renders a component on the "server" and hydrates its HTML in the document, as a page loads: what the component
// does with the DOM it finds (a picture that loaded or failed before hydration) is seen only this way.
import { createSSRApp, h, nextTick, type Component } from 'vue'
import { renderToString } from 'vue/server-renderer'

export async function hydrate(component: Component, props: Record<string, unknown>): Promise<Element> {
  const html = await renderToString(createSSRApp({ render: () => h(component, props) }))
  const container = document.createElement('div')
  container.innerHTML = html
  document.body.append(container)
  createSSRApp({ render: () => h(component, props) }).mount(container)
  await nextTick()
  await nextTick()
  return container.firstElementChild!
}
