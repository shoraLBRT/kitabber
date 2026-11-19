// The favicon: the mark from brand.json as an SVG data URI. Framework-free, so both apps import it.
import brand from './brand.json'

export function faviconHref(theme: 'light' | 'dark' = 'light'): string {
  const { mark, palette } = brand
  const strokes = mark.strokes.map((d) => `<path d="${d}"/>`).join('')
  const svg =
    `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${mark.viewBox}">` +
    `<rect width="100%" height="100%" rx="${mark.radius}" fill="${palette[theme].accent}"/>` +
    `<g fill="none" stroke="${palette[theme].onAccent}" stroke-width="3" stroke-linecap="round" stroke-linejoin="round">` +
    `${strokes}</g></svg>`
  return `data:image/svg+xml,${encodeURIComponent(svg)}`
}
