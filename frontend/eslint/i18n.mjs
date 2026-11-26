// The lint check of the interface strings (E02-10, ADR-0063): a text the person reads comes from the dictionary
// (i18n/locales/), not from a template or a string in the code. `i18n/no-raw-text` reports
// - text between the tags of a template that has a letter in it: `<p>Привет</p>`, `<p>Hello</p>`;
// - a static attribute of a template with Cyrillic in it, and a static text attribute (title, placeholder, alt, label,
//   aria-label…) with any letter: `<input placeholder="Search">`;
// - a string or a template literal with Cyrillic in the script of a component or in the code (`'Сохранить'`), so a
//   text cannot be moved out of the template into a constant to pass the check.
// The dictionaries, the tests and the developer showcases are left out in eslint.config.mjs.

const letter = /\p{L}/u
const cyrillic = /\p{Script=Cyrillic}/u

/** Static attributes whose value is a text for the person. */
const textAttributes = new Set([
  'alt',
  'aria-description',
  'aria-label',
  'aria-placeholder',
  'aria-roledescription',
  'aria-valuetext',
  'label',
  'placeholder',
  'title',
])

const noRawText = {
  meta: {
    type: 'problem',
    docs: { description: 'Interface strings come from the dictionary (i18n/locales/), not from the code.' },
    schema: [],
    messages: {
      text: 'Text «{{text}}» is not taken from the dictionary: move it to i18n/locales/ru.ts and use t().',
    },
  },
  create(context) {
    const report = (node, text) =>
      context.report({ node, messageId: 'text', data: { text: text.trim().slice(0, 40) } })
    const script = {
      Literal(node) {
        if (typeof node.value === 'string' && cyrillic.test(node.value)) report(node, node.value)
      },
      TemplateElement(node) {
        if (cyrillic.test(node.value.cooked ?? node.value.raw)) report(node, node.value.raw)
      },
    }
    const services = context.sourceCode.parserServices
    if (!services?.defineTemplateBodyVisitor) return script
    return services.defineTemplateBodyVisitor(
      {
        ...script,
        VText(node) {
          if (letter.test(node.value)) report(node, node.value)
        },
        VAttribute(node) {
          if (node.directive || !node.value) return
          const text = node.value.value
          const name = node.key.rawName.toLowerCase()
          if (cyrillic.test(text) || (textAttributes.has(name) && letter.test(text))) report(node, text)
        },
      },
      script,
    )
  },
}

export default { meta: { name: 'i18n' }, rules: { 'no-raw-text': noRawText } }
