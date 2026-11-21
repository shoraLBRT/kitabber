import AxeBuilder from '@axe-core/playwright'
import { box, expect, isTouch, press, test, waitForHydration } from './fixtures'
import type { Page } from '@playwright/test'

// The sign-in window (E03-04, #30) in both profiles: a modal dialog on the desktop, a sheet over the whole screen on
// the phone. A guest's "Нравится" (the stand-in on /ui until the reactions) opens it; after the sign-in the person is
// back on the same page, signed in, and the like is set. The forms pass axe (WCAG 2.1 A and AA) empty and with their
// errors, which are shown at the fields. The fake API and the fake provider stand in for Identity.

async function expectAccessible(page: Page) {
  const { violations } = await new AxeBuilder({ page })
    .include('[role="dialog"]')
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze()
  expect(violations.map(({ id, nodes }) => `${id}: ${nodes.map((node) => node.target).join(', ')}`)).toEqual(
    [],
  )
}

async function openFromLike(page: Page) {
  await page.goto('/ui')
  await waitForHydration(page)
  const like = page.getByTestId('showcase-auth-gate').getByRole('button', { name: 'Нравится' })
  await expect(like).toHaveAttribute('aria-pressed', 'false')
  await press(like)
  const dialog = page.getByRole('dialog', { name: 'Вход' })
  await expect(dialog).toBeVisible()
  return { like, dialog }
}

test.describe('окно входа', () => {
  // Refused sign-ins and registrations are answers of the API, which the browser still logs as failed loads; the
  // broken avatar is part of the /ui showcase.
  test.use({ missingResources: ['/api/v1/sign-in/password', '/api/v1/registration', '/missing-avatar.png'] })

  test('гость нажимает «Нравится», входит и возвращается к действию', async ({ page }) => {
    const { dialog } = await openFromLike(page)

    // A modal on the desktop, the whole screen on the phone.
    const panel = await box(dialog)
    const viewport = page.viewportSize()!
    if (isTouch()) {
      await expect(page.locator('[data-presentation]')).toHaveAttribute('data-presentation', 'sheet')
      expect(Math.round(panel.height)).toBe(viewport.height)
      expect(Math.round(panel.width)).toBe(viewport.width)
    } else {
      await expect(page.locator('[data-presentation]')).toHaveAttribute('data-presentation', 'modal')
      expect(panel.width).toBeLessThan(viewport.width / 2)
    }

    // Empty: the form is accessible and waits for an email and a password.
    const submit = dialog.getByRole('button', { name: 'Войти', exact: true })
    await expect(submit).toBeDisabled()
    await expectAccessible(page)

    // The error is at the field and tied to it.
    await dialog.getByLabel('Почта').fill('reader@example.com')
    const password = dialog.getByLabel('Пароль')
    await password.fill('wrong-password')
    await press(submit)
    await expect(password).toHaveAttribute('aria-invalid', 'true')
    await expect(password).toHaveAccessibleDescription('Неверная почта или пароль.')
    await expectAccessible(page)

    await password.fill('secret-1')
    // The sign-in leaves /ui for the provider and comes back through the callback.
    const callback = page.waitForRequest((request) => new URL(request.url()).pathname === '/auth/callback')
    await press(submit)
    await callback

    // Back on the same page, signed in, and the like is set by itself — once.
    await page.waitForURL((url) => url.pathname === '/ui')
    await waitForHydration(page)
    const cookies = await page.context().cookies()
    expect(cookies.find((cookie) => cookie.name === 'auth.session')?.httpOnly).toBe(true)
    const like = page.getByTestId('showcase-auth-gate').getByRole('button', { name: 'Нравится' })
    await expect(like).toHaveAttribute('aria-pressed', 'true')
    await expect(page.getByRole('dialog')).toHaveCount(0)

    // Signed in, the button works without the window; a reload does not repeat the action.
    await press(like)
    await expect(like).toHaveAttribute('aria-pressed', 'false')
    await expect(page.getByRole('dialog')).toHaveCount(0)
    await page.reload()
    await waitForHydration(page)
    await expect(like).toHaveAttribute('aria-pressed', 'false')
  })

  test('регистрация в том же окне: ошибка у поля, доступность, переход ко входу и закрытие', async ({
    page,
  }) => {
    const { like, dialog } = await openFromLike(page)

    await press(dialog.getByRole('button', { name: 'Зарегистрироваться' }))
    const registration = page.getByRole('dialog', { name: 'Регистрация' })
    await expect(registration).toBeVisible()
    await expectAccessible(page)

    const email = registration.getByLabel('Почта')
    await email.fill('taken@example.com')
    await registration.getByLabel('Пароль').fill('secret-1')
    await press(registration.getByRole('button', { name: 'Зарегистрироваться' }))
    await expect(email).toHaveAttribute('aria-invalid', 'true')
    await expect(email).toHaveAccessibleDescription('Этот адрес уже занят другим аккаунтом.')
    await expectAccessible(page)

    await press(registration.getByRole('button', { name: 'Войти', exact: true }))
    await expect(page.getByRole('dialog', { name: 'Вход' })).toBeVisible()

    // Closing leaves the guest where they were, with nothing done.
    await press(page.getByRole('dialog').getByRole('button', { name: 'Закрыть' }))
    await expect(page.getByRole('dialog')).toHaveCount(0)
    await expect(like).toHaveAttribute('aria-pressed', 'false')
    if (!isTouch()) await expect(like).toBeFocused()
  })
})
