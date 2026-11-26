import { expect, expectNoHorizontalScroll, isTouch, press, test } from './fixtures'
import type { Page } from '@playwright/test'

// A new topic (E11-11, FR-TOPIC, ADR-0084) in both profiles against the fake API (e2e/fake-api.mjs): a guest signs in
// and comes back to the form, the form checks the lengths and the address before sending, the API's field errors land
// under the fields, a created topic opens at its address with its owner, and the limit is said above the button. The
// fake keeps the limit per client address, so every test sends its own `x-forwarded-for`. The real rules (the stop
// list, the reserved addresses, a confirmed email, the limit from the settings) are tested by the backend
// (TopicCreateApiTests, TopicCreationRulesTests).

async function asOwnClient(page: Page): Promise<void> {
  const octet = () => Math.floor(Math.random() * 254) + 1
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': `10.${octet()}.${octet()}.${octet()}` })
}

const unique = () => Math.random().toString(36).slice(2, 8)

async function openForm(page: Page): Promise<void> {
  await asOwnClient(page)
  await page.goto('/topics/new')
  await page.waitForURL((url) => url.pathname === '/topics/new')
  await expect(page.getByRole('heading', { level: 1, name: 'Новая тема' })).toBeVisible()
}

async function create(page: Page, name: string, address: string): Promise<void> {
  await page.getByRole('textbox', { name: 'Название' }).fill(name)
  await page.getByRole('textbox', { name: 'Адрес' }).fill(address)
  await press(page.getByRole('button', { name: 'Создать тему' }))
}

test.describe('создание темы', () => {
  test('гость входит, создаёт тему и попадает на её страницу владельцем', async ({ page }) => {
    await openForm(page)
    await expectNoHorizontalScroll(page)
    const address = `club_${unique()}`
    await page.getByRole('textbox', { name: 'Название' }).fill(`Клуб ${unique()}`)
    await page.getByRole('textbox', { name: 'Адрес' }).fill(address.toUpperCase())
    await expect(page.getByText(`Страница темы: /${address}`)).toBeVisible()
    await page.getByRole('textbox', { name: 'Описание' }).fill('Встречи и обсуждения')
    await press(page.getByRole('button', { name: 'Создать тему' }))

    await page.waitForURL((url) => url.pathname === `/${address}`)
    const topic = page.getByTestId('topic')
    await expect(topic.getByText('Встречи и обсуждения')).toBeVisible()
    await expect(topic.getByTestId('topic-owner')).toContainText('Владелец темы: Анна')
    await expect(page.getByText('В этой теме пока нет постов')).toBeVisible()
  })

  test('форма не отправляет пустые и неверные поля и показывает ошибки API', async ({ page }) => {
    await openForm(page)
    await press(page.getByRole('button', { name: 'Создать тему' }))
    await expect(page.getByText('Название — от 2 до 25 символов.')).toBeVisible()
    await expect(page.getByText(/Адрес — от 3 до 32 латинских букв/)).toBeVisible()
    await expect(page.getByRole('textbox', { name: 'Название' })).toBeFocused()

    await create(page, 'Книги', 'settings')
    await expect(page.getByText('Тема с таким названием уже есть.')).toBeVisible()
    await expect(page.getByText('Такой адрес выбрать нельзя.')).toBeVisible()
    await expect(page).toHaveURL(/\/topics\/new$/)
  })

  test('после предела тем форма говорит, что больше нельзя', async ({ page }) => {
    await openForm(page)
    for (let i = 0; i < 2; i++) {
      const address = `limit_${unique()}`
      await create(page, `Тема ${unique()}`, address)
      await page.waitForURL((url) => url.pathname === `/${address}`)
      await page.goto('/topics/new')
    }
    await create(page, `Тема ${unique()}`, `limit_${unique()}`)
    await expect(page.getByTestId('topic-create-error')).toContainText('не больше 2 тем')
    await expect(page.getByRole('textbox', { name: 'Название' })).not.toHaveValue('')
  })

  test('без подтверждённой почты форма ведёт к подтверждению', async ({ page }) => {
    await openForm(page)
    await create(page, `Тема ${unique()}`, 'unconfirmed')
    const error = page.getByTestId('topic-create-error')
    await expect(error).toContainText('Подтвердите почту, чтобы создавать темы.')
    await expect(error.getByRole('link', { name: 'Подтвердить почту' })).toHaveAttribute(
      'href',
      '/settings/account',
    )
  })

  test('меню вошедшего ведёт к форме новой темы', async ({ page }) => {
    await openForm(page)
    await page.goto(isTouch() ? '/menu' : '/')
    const nav = isTouch() ? page.getByTestId('menu-screen') : page.getByTestId('shell-nav')
    await press(nav.getByRole('link', { name: 'Создать тему' }))
    await page.waitForURL((url) => url.pathname === '/topics/new')
  })
})
