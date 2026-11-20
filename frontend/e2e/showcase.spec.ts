import brand from '../../brand/brand.json' with { type: 'json' }
import { expect, expectNoHorizontalScroll, test } from './fixtures'

// Design showcases (E02-01 /brand, E02-02 /tokens, E02-04 /ui) in both palettes (ADR-0012): the page renders
// without errors, has exactly one first-level heading, takes the background of the chosen theme and does not scroll sideways.
const pages = [
  { path: '/brand', heading: 'Бренд', sections: ['Светлая тема', 'Тёмная тема'] },
  { path: '/tokens', heading: 'Дизайн-токены', sections: [] },
  { path: '/ui', heading: 'Базовые компоненты', sections: [] },
] as const
const themes = ['light', 'dark'] as const

// The /ui showcase shows the fallback of UiAvatar with an image that does not exist.
test.use({ missingResources: ['/missing-avatar.png'] })

function rgb(hex: string): string {
  const [r, g, b] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16))
  return `rgb(${r}, ${g}, ${b})`
}

for (const { path, heading, sections } of pages) {
  for (const theme of themes) {
    test(`витрина ${path}, тема ${theme}`, async ({ page }) => {
      // The other palette as the system preference: the data-theme attribute must win over it.
      await page.emulateMedia({ colorScheme: theme === 'light' ? 'dark' : 'light' })
      await page.goto(path)
      await page.evaluate((value) => (document.documentElement.dataset.theme = value), theme)

      await expect(page.getByRole('heading', { level: 1 })).toHaveCount(1)
      await expect(page.getByRole('heading', { level: 1, name: heading, exact: true })).toBeVisible()
      for (const section of sections) {
        await expect(page.getByRole('heading', { level: 2, name: section, exact: true })).toBeVisible()
      }
      await expect(page.locator('body')).toHaveCSS('background-color', rgb(brand.palette[theme].background))
      await expect(page.locator('body')).toHaveCSS('color', rgb(brand.palette[theme].text))
      await expectNoHorizontalScroll(page)
    })
  }
}
