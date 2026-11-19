// Style lint rules shared by both apps (E02-02, ADR-0012): every colour and size in component styles comes from
// the design tokens (brand/tokens.css), so the rules forbid raw colours, raw lengths and raw type values.
// Plain data, no imports: each app's stylelint.config.mjs adds the syntax for .vue files from its own node_modules.
const lengths = ['px', 'rem', 'em', 'ex', 'ch', 'pt', 'pc', 'cm', 'mm', 'in', 'q']

const sized =
  '/^(margin|padding|gap|row-gap|column-gap|inset|top|right|bottom|left|width|height|min-|max-|font|line-height|' +
  'letter-spacing|word-spacing|border|outline|flex-basis|text-indent|text-underline-offset|box-shadow|text-shadow|' +
  'grid-template|grid-auto|translate|scroll-margin|scroll-padding|--)/'

export default {
  'color-no-hex': true,
  'color-named': 'never',
  'function-disallowed-list': ['rgb', 'rgba', 'hsl', 'hsla', 'hwb', 'lab', 'lch', 'oklab', 'oklch', 'color'],
  'declaration-property-unit-disallowed-list': { [sized]: lengths },
  'declaration-property-value-disallowed-list': {
    'font-weight': ['/^\\d/', 'bold', 'bolder', 'lighter'],
    'line-height': ['/^[\\d.]/'],
    'font-family': ['/^(?!var\\(|inherit$)/'],
  },
}
