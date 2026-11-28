import { expect, expectNoHorizontalScroll, isTouch, press, test, waitForHydration } from './fixtures'
import type { Page } from '@playwright/test'

// The management of a topic (E11-12, FR-TOPIC, ADR-0121) in both profiles against the fake API (e2e/fake-api.mjs): the
// owner (Анна owns «Наука») changes the cover, the description and the rules, appoints and removes a moderator and
// offers the topic to Борис, then cancels; the topic «Книги» of Борис is offered to Анна, who accepts it; a guest
// reads the rules and the moderators only. The fake keeps the topics per client address, so every test sends its own
// `x-forwarded-for`. The real rules are the backend's (TopicManagementApiTests, TopicManagementRulesTests).
const science = '0195f3a0-0000-7000-8000-0000000000b5'
const books = '0195f3a0-0000-7000-8000-0000000000b4'

async function asOwnClient(page: Page): Promise<void> {
  const octet = () => Math.floor(Math.random() * 254) + 1
  await page.setExtraHTTPHeaders({ 'x-forwarded-for': `10.${octet()}.${octet()}.${octet()}` })
}

async function signIn(page: Page): Promise<void> {
  await page.goto(isTouch() ? '/menu' : '/')
  const login = isTouch()
    ? page.getByTestId('menu-screen').getByRole('link', { name: 'Войти' })
    : page.getByTestId('shell-header').getByRole('link', { name: 'Войти' })
  await press(login)
  await page.waitForURL((url) => url.pathname === '/')
}

async function squareJpeg(page: Page): Promise<Buffer> {
  const data = await page.evaluate(() => {
    const canvas = document.createElement('canvas')
    canvas.width = 300
    canvas.height = 300
    const context = canvas.getContext('2d')!
    context.fillStyle = '#336699'
    context.fillRect(0, 0, 300, 300)
    return canvas.toDataURL('image/jpeg')
  })
  return Buffer.from(data.split(',')[1]!, 'base64')
}

async function openManagement(page: Page, topic: string): Promise<void> {
  await page.goto(`/topics/${topic}/manage`)
  await waitForHydration(page)
  await expect(page.getByTestId('topic-management')).toBeVisible()
}

test.describe('управление темой', () => {
  test('владелец меняет обложку, описание и правила, назначает и снимает модератора', async ({ page }) => {
    await asOwnClient(page)
    await signIn(page)
    await page.goto('/science')
    await press(page.getByTestId('topic-manage-link'))
    await page.waitForURL((url) => url.pathname === `/topics/${science}/manage`)
    await waitForHydration(page)
    await expect(page.getByRole('heading', { level: 1, name: 'Управление темой' })).toBeVisible()
    await expectNoHorizontalScroll(page)

    const cover = page.getByTestId('blog-cover')
    await cover.getByTestId('blog-cover-file').setInputFiles({
      name: 'cover.jpg',
      mimeType: 'image/jpeg',
      buffer: await squareJpeg(page),
    })
    await cover.getByRole('button', { name: 'Сохранить' }).click()
    await expect(page.getByText('Обложка сохранена')).toBeVisible()
    await expect(cover.getByTestId('blog-cover-image')).toHaveAttribute('src', /-cover-1920\.webp$/)

    await page.getByRole('textbox', { name: 'Описание' }).fill('Открытия и опыты')
    await page.getByRole('textbox', { name: 'Правила' }).fill('1. Без спама\n2. С источниками')
    await press(page.getByTestId('manage-settings-save'))
    await expect(page.getByText('Изменения сохранены')).toBeVisible()

    const appoint = page.getByTestId('manage-appoint')
    await appoint.getByRole('textbox', { name: 'Никнейм блога' }).fill('nobody_here')
    await press(appoint.getByRole('button', { name: 'Назначить' }))
    await expect(page.getByText('Нет блога с таким никнеймом.')).toBeVisible()
    await appoint.getByRole('textbox', { name: 'Никнейм блога' }).fill('@boris')
    await press(appoint.getByRole('button', { name: 'Назначить' }))
    await expect(page.getByTestId('manage-moderator')).toHaveText(/Борис/)

    // The topic's page shows what the owner changed.
    await page.goto('/science')
    await expect(page.getByTestId('topic-description')).toHaveText('Открытия и опыты')
    await expect(page.getByTestId('blog-cover-image')).toHaveAttribute('src', /-cover-1920\.webp$/)

    await openManagement(page, science)
    await expect(page.getByRole('textbox', { name: 'Правила' })).toHaveValue('1. Без спама\n2. С источниками')
    await press(page.getByRole('button', { name: 'Снять модератора Борис' }))
    await expect(page.getByTestId('manage-moderator')).toHaveCount(0)
    await expect(page.getByTestId('manage-no-moderators')).toBeVisible()
  })

  test('владелец предлагает тему и отменяет передачу', async ({ page }) => {
    await asOwnClient(page)
    await signIn(page)
    await openManagement(page, science)

    const offer = page.getByTestId('manage-offer')
    await offer.getByRole('textbox', { name: 'Никнейм блога' }).fill('anna')
    await press(offer.getByRole('button', { name: 'Предложить тему' }))
    await expect(page.getByText('Тема уже ваша: выберите другого человека.')).toBeVisible()
    await offer.getByRole('textbox', { name: 'Никнейм блога' }).fill('boris')
    await press(offer.getByRole('button', { name: 'Предложить тему' }))
    await expect(page.getByTestId('manage-transfer-pending')).toHaveText(
      'Тема предложена Борис. Ждём согласия.',
    )
    // The topic is still hers until Борис accepts.
    await page.reload()
    await waitForHydration(page)
    await expect(page.getByRole('heading', { level: 1, name: 'Управление темой' })).toBeVisible()

    await press(page.getByTestId('manage-transfer-cancel'))
    await expect(page.getByTestId('manage-offer')).toBeVisible()
    await expect(page.getByTestId('manage-transfer-pending')).toHaveCount(0)
  })

  test('получатель принимает предложенную тему и становится владельцем', async ({ page }) => {
    await asOwnClient(page)
    await signIn(page)
    await openManagement(page, books)
    await expect(page.getByRole('heading', { level: 1, name: 'Правила и модераторы' })).toBeVisible()
    await expect(page.getByTestId('transfer-offer')).toContainText(
      'Борис предлагает вам стать владельцем темы.',
    )
    await expect(page.getByTestId('manage-settings')).toHaveCount(0)

    await press(page.getByTestId('transfer-accept'))
    await expect(page.getByText('Теперь тема ваша')).toBeVisible()
    await expect(page.getByTestId('transfer-offer')).toHaveCount(0)
    await expect(page.getByTestId('manage-settings')).toBeVisible()
    await expect(page.getByTestId('manage-owner')).toContainText('Анна')

    await page.goto('/books')
    await expect(page.getByTestId('topic-owner')).toHaveText('Владелец темы: Анна')
  })

  test('гость видит правила и модераторов, но не управление', async ({ page }) => {
    await asOwnClient(page)
    await openManagement(page, science)

    await expect(page.getByRole('heading', { level: 1, name: 'Правила и модераторы' })).toBeVisible()
    await expect(page.getByText('У темы пока нет правил.')).toBeVisible()
    await expect(page.getByTestId('manage-owner')).toContainText('Анна')
    await expect(page.getByTestId('manage-settings')).toHaveCount(0)
    await expect(page.getByTestId('manage-transfer')).toHaveCount(0)
    await expect(page.getByTestId('blog-cover')).toHaveCount(0)
    await expectNoHorizontalScroll(page)
  })
})
