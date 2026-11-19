# Состояние проекта

Обновляется в каждом PR. Последнее обновление: 19 ноября 2026 (E01-06).

## Текущая веха

**M0. Фундамент.** Критерий выхода: PR проверяется автоматически; пустой каркас открывается на 1440 и 390 px.

## Следующая задача

\#6 «E01-07 Каркас API: версии, формат ошибок, курсорная пагинация, OpenAPI». Дальше — по [ROADMAP.md](ROADMAP.md).

## Что уже есть

- Требования: [spec.md](spec.md), [telegram-bridge.md](telegram-bridge.md),
  [unified-accounts.md](unified-accounts.md), [content-policy.md](content-policy.md).
- Решения: [ADR-0001](adr/0001-stack.md) (стек, принято), [ADR-0002](adr/0002-identity.md) (аккаунты,
  предложено), [ADR-0003](adr/0003-telegram-bridge.md) (Telegram, предложено).
  [ADR-0004](adr/0004-database-migrations.md) (миграции EF Core, схема и история миграций на модуль, принято в
  задаче E01-05).
  [ADR-0005](adr/0005-messaging-and-cache.md) (MassTransit 8 на RabbitMQ, outbox в схеме модуля, Redis, принято в
  задаче E01-06).
- Бэклог в GitHub Issues: 255 задач в 19 эпиках и 8 вехах; порядок — в [ROADMAP.md](ROADMAP.md).
- Процесс: `CLAUDE.md`, `scripts/verify.ps1`, CI (`ci / verify`), проверка заголовка PR, release-please,
  конфигурация Renovate.

- Каркас монорепозитория (E01-01): решение .NET `backend/Platform.slnx` (хосты `Api` и `Workers`, net10.0),
  веб-клиент на Nuxt 3 в `frontend/`, админка на Vue 3 + Vite в `admin/`; общие `.editorconfig`,
  `.prettierrc.json`, `global.json`. Структура и линтеры описаны в [ARCHITECTURE.md](ARCHITECTURE.md).
- `scripts/verify.ps1` проверяет структуру репозитория, сборку и `dotnet format` бэкенда, ESLint, Prettier,
  проверку типов и `pnpm build` обоих клиентов. CI ставит .NET по `global.json`, pnpm и Node 22.
- CI (E01-02): `ci / verify` запускает `scripts/verify.ps1` с кэшами NuGet и pnpm и тайм-аутом 10 минут;
  таблица шагов и сводка отчётов о тестах (`artifacts/test-results`, TRX и JUnit) видны в сводке запуска, отчёты
  выкладываются артефактом. Самопроверка `scripts/verify-selftest.ps1` (шаг verify) доказывает, что упавший шаг
  роняет проверку, линтеры отвергают плохой код и workflow соблюдает эти ограничения.
- Локальное окружение (E01-04): `compose.yaml` в корне поднимает PostgreSQL, Redis, RabbitMQ, OpenSearch,
  MinIO, imgproxy, Mailpit и ClickHouse командой `docker compose up -d --wait`; порты и пароли — из `.env`
  (шаблон `.env.example`), запуск за три команды описан в README. `scripts/dev-env-test.ps1` проверяет файл
  статически и смоук-тестом поднимает окружение на свободных портах (healthcheck, доступ с хоста, пароли).
  Без запущенного Docker смоук локально помечается `skipped`, в CI обязателен. MinIO — образ Chainguard
  `cgr.dev/chainguard/minio`, закреплённый по digest: официальные образы MinIO больше не публикуются.

- База данных (E01-05): EF Core на PostgreSQL. `Platform.Persistence` даёт `ModuleDbContext<T>`: у модуля своя
  схема (имя модуля в snake_case), свой `DbContext` и своя история миграций `<схема>.__ef_migrations_history`;
  имена в базе — snake_case. Команда `dotnet run --project backend/src/Migrator` (`migrate`, `status`,
  `rollback <схема>`) накатывает миграции всех модулей из `ModuleCatalog`; API их не накатывает. Справочные данные —
  в миграциях (`HasData`), демо-данные — сидер E01-17. Правила — [ADR-0004](adr/0004-database-migrations.md).
  Модулей пока нет, поэтому `ModuleCatalog` пуст; накат на чистую базу, независимость схем и откат последней
  миграции проверяются на двух образцовых модулях в `backend/tests/Platform.Persistence.IntegrationTests`
  (PostgreSQL в Testcontainers; без Docker локально тесты пропускаются, в CI падают). Тесты пока запускаются через
  VSTest (`IsTestingPlatformApplication=false`): переход на Microsoft.Testing.Platform и общий каркас — E01-15.

- Очереди, воркеры и кэш (E01-06): `Platform.Messaging` — шина MassTransit 8.5 (Apache-2.0; 9+ платная, Renovate
  её не предлагает) на RabbitMQ или тестовом транспорте `InMemory` (`Messaging:Transport`); outbox и inbox модуля
  лежат в его схеме (`AddOutboxTables`, `AddModuleOutbox<T>`, `ModuleConsumerDefinition<,>`). `Platform.Caching` —
  `IDistributedCache` на Redis или в памяти (`Cache:Provider`). Хост `Platform.Workers` (`WorkerHost`) собирает шину,
  потребители модулей и кэш; модулей пока нет, `WorkerHost.AddModules` пуст, API шину ещё не подключает.
  `backend/tests/Platform.Messaging.IntegrationTests` (PostgreSQL, RabbitMQ, Redis в Testcontainers) проверяют:
  событие из транзакции доставляется ровно один раз и не уходит без коммита, повторная доставка отбрасывается
  inbox, сообщения, опубликованные при остановленном воркере, обрабатываются после перезапуска; тестовый
  транспорт и кэш в памяти работают без Docker. Правила — [ADR-0005](adr/0005-messaging-and-cache.md). Ограничение:
  bus outbox — один на хост; несколько публикующих модулей в одном хосте — задача #281.

Функций продукта пока нет: оба клиента — пустые страницы, у API есть только `/health`.

## Базовая линия тестов

| Набор | Число тестов |
| --- | --- |
| Сервер, модульные | 0 |
| Сервер, интеграционные | 28 (`Platform.Persistence.IntegrationTests`: 5 на PostgreSQL, 6 без базы; `Platform.Messaging.IntegrationTests`: 6 на контейнерах, 11 без Docker) |
| Клиент, компонентные | 0 |
| Сквозные (в каждом из двух профилей) | 0 |

Число не должно уменьшаться; уменьшение объясняется в PR.

## Открытые вопросы к владельцу

Ни один не блокирует M0–M3.

| Вопрос | Когда нужен ответ | Связанные задачи |
| --- | --- | --- |
| Финальное название и бренд (сейчас kitabber) | До открытой беты (M5) | E02-01 |
| Подтвердить ADR-0002: свой провайдер OpenID Connect с первого дня | До начала M1 | E18-01, E18-02 |
| Какие проекты будут объединять аккаунты и на каком они стеке | До M7 | E18-06 |
| Хостинг, домен, почтовый сервис, хранилище | До первой выкладки на stage | E01-03, E01-12 |
| Ключи внешних провайдеров входа (Яндекс, VK, Google, Telegram) | До M2 | E03-07 |
| Итоговый текст правил сообщества | До M4 | E15-17 |
| Бот Telegram, служебный аккаунт и `api_id`, сервер вне России, тестовый канал | До M6 | E19-01 |
| Переносить ли мост с Telegram раньше (до открытой беты) | До конца M3 | эпик E19 |
| Юридические документы | До M7 | E17-08 |
| MassTransit 8 (Apache-2.0) пока поддерживается авторами ограниченно; при прекращении поддержки — лицензия 9, Wolverine или свой outbox (ADR-0005) | Когда ветка 8 перестанет получать исправления | E01-06 |

## Настройки репозитория

Включено (19 ноября 2026):

- репозиторий публичный, основная ветка `main`;
- слияние только сквошем, заголовок сквош-коммита — заголовок PR, ветка удаляется после слияния;
- workflow `ci`, `PR title` и `Release` работают;
- GitHub Actions разрешено создавать pull request (нужно release-please);
- правило `main`: только через PR, только сквош, обязательные проверки `pr-title / check` и `verify`, запрет удаления и принудительной записи;
- автослияние (auto-merge) разрешено.

Не включено — нужно действие владельца:

| Что | Зачем | Где |
| --- | --- | --- |
| Приложение Renovate | Обновление зависимостей; конфигурация уже в `renovate.json` | github.com/apps/renovate |
