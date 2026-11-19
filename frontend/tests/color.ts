// Colour maths for the brand and token tests: WCAG 2.x contrast and the CIE76 colour difference.
type Rgb = [number, number, number]
const rgb = (hex: string): Rgb => [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255) as Rgb
const linear = (c: number) => (c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4)

// WCAG 2.x relative luminance and contrast ratio.
export function contrast(a: string, b: string): number {
  const lum = (hex: string) => {
    const [r, g, bl] = rgb(hex).map(linear) as Rgb
    return 0.2126 * r + 0.7152 * g + 0.0722 * bl
  }
  const [hi, lo] = [lum(a), lum(b)].sort((x, y) => y - x) as [number, number]
  return (hi + 0.05) / (lo + 0.05)
}

// CIE76 colour difference in CIELAB (D65).
export function deltaE(a: string, b: string): number {
  const lab = (hex: string) => {
    const [r, g, bl] = rgb(hex).map(linear) as Rgb
    const xyz = [
      (0.4124 * r + 0.3576 * g + 0.1805 * bl) / 0.95047,
      0.2126 * r + 0.7152 * g + 0.0722 * bl,
      (0.0193 * r + 0.1192 * g + 0.9505 * bl) / 1.08883,
    ].map((t) => (t > 216 / 24389 ? Math.cbrt(t) : (t * 24389) / 27 / 116 + 16 / 116)) as Rgb
    return [116 * xyz[1] - 16, 500 * (xyz[0] - xyz[1]), 200 * (xyz[1] - xyz[2])]
  }
  const [x, y] = [lab(a), lab(b)]
  return Math.hypot(...x.map((v, i) => v - y[i]!))
}
