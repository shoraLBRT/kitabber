import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'
import type { Page } from '@playwright/test'

// The nickname of the own blog (E04-05, FR-PROFILE, ADR-0090) in both profiles against the fake API
// (e2e/fake-api.mjs): a taken or reserved nickname is refused under the field, a new one is saved and the old address
// leads to the new one, a second change is locked for 30 days, and a failure keeps what was typed. The fake keeps a
// change per client address, so every test sends its own `x-forwarded-for`. The real rules (taken, reserved, 30 and 90
// days) are tested by the backend (ProfileNicknameApiTests, NicknameChangeRulesTests).
async function asOwnClient(page: Page): Promise<void> {
  const octet = () => Math.floor(Math.random() * 254) + 1
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': `10.${octet()}.${octet()}.${octet()}` })
}

async function openSettings(page: Page): Promise<void> {
  await asOwnClient(page)
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
  await page.goto('/settings/blog')
  await waitForHydration(page)
}

const field = (page: Page) => page.getByRole('textbox', { name: 'Никнейм' })
const submit = (page: Page) => page.getByRole('button', { name: 'Сменить никнейм' })

test.describe('никнейм блога', () => {
  test('занятый и зарезервированный никнейм отклоняются', async ({ page }) => {
    await openSettings(page)
    await expect(field(page)).toHaveValue('anna')

    await field(page).fill('boris')
    await press(submit(page))
    await expect(page.getByTestId('nickname-form')).toContainText('Этот никнейм уже занят блогом или темой.')

    await field(page).fill('settings')
    await press(submit(page))
    await expect(page.getByTestId('nickname-form')).toContainText('Такой никнейм выбрать нельзя.')
    await expectNoHorizontalScroll(page)
  })

  test('новый никнейм сохраняется, старый адрес ведёт на новый, вторая смена закрыта', async ({ page }) => {
    await openSettings(page)

    await field(page).fill('@Anna_New')
    await press(submit(page))
    await expect(page.getByText('Никнейм изменён')).toBeVisible()
    await expect(page.getByTestId('nickname-locked')).toBeVisible()
    await expect(field(page)).toBeDisabled()

    await page.goto('/u/anna')
    await page.waitForURL((url) => url.pathname === '/u/anna_new')
    await expect(page.getByTestId('blog-nickname')).toHaveText('@anna_new')
  })

  test('сбой оставляет введённое', async ({ page }) => {
    await openSettings(page)

    await field(page).fill('outage')
    await press(submit(page))

    await expect(page.getByText('Не удалось сохранить. Попробуйте ещё раз.')).toBeVisible()
    await expect(field(page)).toHaveValue('outage')
  })
})
