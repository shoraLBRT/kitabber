import brand from '../../../../brand/brand.json'

// The sample picture of the /ui showcase (E06-04, ADR-0040) in the widths of its srcset, drawn here from the brand
// palette: the apps keep no image files (tests/brand.spec.ts). Each width says its number, so the showcase shows
// which one the browser took. Only /showcase/landscape-{320,640,1280}.svg exist.
const widths = new Set([320, 640, 1280])

export default defineEventHandler((event) => {
  const width = Number(/^landscape-(\d+)\.svg$/.exec(getRouterParam(event, 'file') ?? '')?.[1])
  if (!widths.has(width)) throw createError({ statusCode: 404, message: 'Нет такой картинки' })
  const { light, dark } = brand.palette
  setResponseHeader(event, 'content-type', 'image/svg+xml')
  setResponseHeader(event, 'cache-control', 'public, max-age=31536000, immutable')
  return [
    `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${(width * 5) / 8}" viewBox="0 0 320 200">`,
    `<rect width="320" height="200" fill="${dark.surface}"/>`,
    `<circle cx="250" cy="60" r="28" fill="${dark.accent}"/>`,
    `<path d="M0 200 L110 90 L180 160 L230 120 L320 200 Z" fill="${light.accent}"/>`,
    `<text x="20" y="40" font-family="sans-serif" font-size="24" fill="${dark.text}">${width} px</text>`,
    '</svg>',
  ].join('')
})
