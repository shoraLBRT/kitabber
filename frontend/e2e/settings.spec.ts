import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'
import type { Page } from '@playwright/test'

// The blog settings (E04-03, FR-PROFILE, docs/ui/settings.md) in both profiles against the fake API
// (e2e/fake-api.mjs): the owner opens them from the blog, the limits are checked before anything is sent, a chosen
// picture is cropped in the browser and uploaded as a JPEG through the media service, and the blog shows the new name
// and description right after «Сохранить». The fake keeps an edit per client address, so every test sends its own
// `x-forwarded-for` and parallel tests never see each other's edits. The real rules (limits, whose image) are tested
// by the backend (Platform.Modules.Blogs.IntegrationTests, ProfileEditApiTests).
const fakeApi = 'http://127.0.0.1:3102'
// A 1×1 PNG.
const picture = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=',
  'base64',
)

async function asOwnClient(page: Page): Promise<string> {
  const octet = () => Math.floor(Math.random() * 254) + 1
  const client = `10.${octet()}.${octet()}.${octet()}`
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': client })
  return client
}

async function signIn(page: Page): Promise<void> {
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

test.describe('настройки блога', () => {
  test('гость входит и возвращается в настройки', async ({ page }) => {
    await asOwnClient(page)
    await page.goto('/settings/blog')
    await page.waitForURL((url) => url.pathname === '/settings/blog')
    await expect(page.getByRole('heading', { level: 1, name: 'Настройки блога' })).toBeVisible()
    await expect(page.getByRole('textbox', { name: 'Название' })).toHaveValue('Анна')
  })

  test('владелец меняет имя, описание и аватар, и блог сразу показывает изменения', async ({
    page,
    request,
  }) => {
    const client = await asOwnClient(page)
    await signIn(page)
    await page.goto('/u/anna')
    await waitForHydration(page)
    await press(page.getByRole('group', { name: 'Управление блогом' }).getByRole('link', { name: 'Ред.' }))
    await page.waitForURL((url) => url.pathname === '/settings/blog')
    const form = page.getByTestId('blog-settings')
    const name = form.getByRole('textbox', { name: 'Название' })
    await expect(name).toHaveValue('Анна')

    // The limit is checked as the person types; «Сохранить» then sends nothing and focuses the field.
    await name.fill('Очень длинное название блога')
    await expect(form.getByText('Название — не длиннее 25 символов.')).toBeVisible()
    await press(form.getByRole('button', { name: 'Сохранить' }))
    await expect(name).toBeFocused()

    await name.fill('Анна К.')
    await expect(form.getByText('7/25')).toBeVisible()
    await form.getByRole('textbox', { name: 'Описание' }).fill('Пишу о книгах, городах и поездах.')

    await form
      .getByTestId('avatar-file')
      .setInputFiles({ name: 'photo.png', mimeType: 'image/png', buffer: picture })
    const dialog = page.getByRole('dialog', { name: 'Аватар' })
    await expect(dialog.getByTestId('avatar-frame')).toBeVisible()
    await expectNoHorizontalScroll(page)
    await press(dialog.getByRole('button', { name: 'Применить' }))
    await expect(dialog).toHaveCount(0)
    await expect(form.getByTestId('avatar-preview')).toHaveJSProperty('tagName', 'IMG')

    await press(form.getByRole('button', { name: 'Сохранить' }))
    await expect(page.getByTestId('toasts-polite')).toContainText('Изменения сохранены')
    await expectNoHorizontalScroll(page)

    // The cropped avatar went through the media service as a JPEG, and the profile points at it.
    const uploads = await (await request.get(`${fakeApi}/__uploads?client=${client}`)).json()
    expect(uploads).toHaveLength(1)
    expect(uploads[0].jpeg).toBe(true)
    const saved = await (
      await request.get(`${fakeApi}/api/v1/profiles/by-nickname/anna`, {
        headers: { 'x-forwarded-for': client },
      })
    ).json()
    expect(saved.avatarId).toBe(uploads[0].id)

    await press(page.getByTestId('settings-back'))
    await page.waitForURL((url) => url.pathname === '/u/anna')
    const blog = page.getByTestId('blog')
    await expect(blog.getByRole('heading', { level: 1, name: 'Анна К.' })).toBeVisible()
    await expect(blog.getByText('Пишу о книгах, городах и поездах.')).toBeVisible()
    // The avatar is shown through its delivery link (ADR-0037), not the letter placeholder.
    const avatar = blog.locator('img.blog__avatar')
    await expect(avatar).toHaveAttribute('src', saved.avatarUrl)
    await expect.poll(() => avatar.evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0)
    // The 64 px avatar in the width for the screen's density (ADR-0040): 64 at one device pixel per CSS pixel
    // (desktop), 192 at three (the phone profile).
    const density = await page.evaluate(() => window.devicePixelRatio)
    await expect
      .poll(() => avatar.evaluate((img: HTMLImageElement) => img.currentSrc))
      .toMatch(new RegExp(`-${64 * density}\\.webp$`))

    // And after a reload, rendered by the server.
    await page.reload()
    await expect(page.getByRole('heading', { level: 1, name: 'Анна К.' })).toBeVisible()
    await expect(page.getByTestId('blog').locator('img.blog__avatar')).toHaveAttribute('src', saved.avatarUrl)
  })

  test('сбой сохранения — тост, введённое остаётся', async ({ page }) => {
    await asOwnClient(page)
    await signIn(page)
    await page.goto('/settings/blog')
    await waitForHydration(page)
    const form = page.getByTestId('blog-settings')
    await form.getByRole('textbox', { name: 'Название' }).fill('сбой')
    await press(form.getByRole('button', { name: 'Сохранить' }))
    await expect(page.getByTestId('toasts-assertive')).toContainText('Не удалось сохранить')
    await expect(form.getByRole('textbox', { name: 'Название' })).toHaveValue('сбой')
    await expectNoHorizontalScroll(page)
  })
})
