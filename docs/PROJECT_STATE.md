# Состояние проекта

Обновляется в каждом PR. Последнее обновление: 19 ноября 2026 (E02-04).

## Текущая веха

**M0. Фундамент.** Критерий выхода: PR проверяется автоматически; пустой каркас открывается на 1440 и 390 px.

## Следующая задача

\#15 «E02-05 Каркас десктопа» (базовые компоненты #14 — на ревью). Дальше — по [ROADMAP.md](ROADMAP.md).

## Что уже есть

- Требования: [spec.md](spec.md), [telegram-bridge.md](telegram-bridge.md),
  [unified-accounts.md](unified-accounts.md), [content-policy.md](content-policy.md).
- Решения: [ADR-0001](adr/0001-stack.md) (стек, принято), [ADR-0002](adr/0002-identity.md) (аккаунты,
  предложено), [ADR-0003](adr/0003-telegram-bridge.md) (Telegram, предложено).
  [ADR-0004](adr/0004-database-migrations.md) (миграции EF Core, схема и история миграций на модуль, принято в
  задаче E01-05).
  [ADR-0005](adr/0005-messaging-and-cache.md) (MassTransit 8 на RabbitMQ, outbox в схеме модуля, Redis, принято в
  задаче E01-06).
  [ADR-0006](adr/0006-rest-api-conventions.md) (REST: `/api/v1`, ProblemDetails с кодом, курсорная пагинация, снимок
  OpenAPI, принято в задаче E01-07).
  [ADR-0007](adr/0007-configuration-and-secrets.md) (настройки через переменные окружения, проверка на старте,
  gitleaks; принято в задаче E01-13; выкладка E01-03 берёт секреты стендов из окружений GitHub Actions).
  [ADR-0008](adr/0008-backend-test-framework.md) (общий каркас тестов, контейнер на сборку, Respawn, управляемые
  часы, остаёмся на VSTest; принято в задаче E01-15).
  [ADR-0009](adr/0009-demo-data-and-factories.md) (демо-данные и фабрики: одно зерно, фабрики в коде модуля,
  сидер-команда `backend/tools/Seed`; принято в задаче E01-17).
  [ADR-0010](adr/0010-images-and-deployment.md) (образы в GHCR, стенд на Docker Compose, выкладка тегом по SSH, откат
  прежним тегом; предложено в задаче E01-03).
  [ADR-0011](adr/0011-brand-source-and-icons.md) (бренд в `brand/brand.json`, знак из данных, иконки Lucide;
  предложено в задаче E02-01).
  [ADR-0012](adr/0012-design-tokens.md) (дизайн-токены и линтер стилей; предложено в задаче E02-02).
  [ADR-0013](adr/0013-ui-components.md) (свои базовые компоненты без headless-библиотеки, шторка как режим модального
  окна, витрина — страница `/ui` вместо Histoire или Storybook; предложено в задаче E02-04).
- Бэклог в GitHub Issues: 256 задач в 19 эпиках и 8 вехах; порядок — в [ROADMAP.md](ROADMAP.md).
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
  VSTest (`IsTestingPlatformApplication=false`), см. ADR-0008.

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

- Каркас API (E01-07): `Platform.Http` — версия в пути `/api/v1` (модуль реализует `IApiEndpoints` и
  регистрируется `AddApiEndpoints<T>()`), все ошибки — RFC 9457 problem details с полями `code` и `traceId`
  (`ApiProblems`, `ApiProblemException`, коды `ApiErrorCodes`; 401/403/404/405, ошибки валидации и чтения тела,
  непредвиденные исключения без их текста), валидация DataAnnotations встроенным генератором .NET 10 (каждый модуль
  вызывает `AddValidation()`, типы запроса публичные), курсорная пагинация `PageQuery` → `PageRequest<TCursor>` →
  `CursorPage<T>` (`limit` 1–100, по умолчанию 20; курсор — base64url JSON ключа сортировки). OpenAPI `v1`
  отдаётся по `/openapi/v1.json`, пишется при сборке и сравнивается со снимком `backend/src/Api/openapi/v1.json`
  (обновление — `UPDATE_OPENAPI_SNAPSHOT=1`). Тесты `backend/tests/Platform.Api.Tests` (хост в процессе, без
  Docker): формат ошибок, права доступа на тестовой схеме аутентификации, обход курсоров, снимок. Правила —
  [ADR-0006](adr/0006-rest-api-conventions.md). Эндпоинтов продукта пока нет, поэтому снимок пуст (`paths: {}`).

- Настройки и секреты (E01-13): `Platform.Configuration` — окружения `Development`/`Test`/`Staging`/`Production`
  (другое имя не стартует), слои `appsettings.json` → `appsettings.{Окружение}.json` → user secrets → переменные
  окружения → командная строка → файлы секретов из `Secrets:Directory`. Библиотеки объявляют свои настройки
  (`Setting`: секрет, допустимые значения, когда обязательна), хост проверяет их в `ConfigurePlatform` до сборки и
  не стартует, называя каждую отсутствующую настройку и её переменную окружения; в Staging и Production секрет из
  файла настроек — тоже ошибка. Все настройки — в [CONFIG.md](CONFIG.md), тест сверяет документ с кодом и
  проверяет, что в `appsettings.json`/`Staging`/`Production` нет секретов. `scripts/secret-scan.ps1` (шаг
  `secret scan`) гоняет gitleaks по всем файлам, которые git закоммитил бы, после проверки на подложенном токене;
  CI ставит gitleaks 8.28.0, локально шаг пропускается без gitleaks. Тесты — `backend/tests/Platform.Configuration.Tests`.
  Правила и варианты хранилища секретов для стендов — [ADR-0007](adr/0007-configuration-and-secrets.md); выбор
  хранилища — за владельцем; выкладка E01-03 пишет env-файл из секретов окружения GitHub, код от этого не зависит.

- Каркас серверных тестов (E01-15): библиотека `backend/tests/Platform.Testing` — контейнеры PostgreSQL, Redis,
  RabbitMQ из образов `compose.yaml` (один на тестовую сборку, `AssemblyFixture`), `PlatformApiFactory` (API в
  процессе, окружение `Test`, `WithSetting`/`WithModule`), `IntegrationApiFactory` (API на PostgreSQL и Redis:
  база и номерная база Redis на тестовый класс, миграции модулей один раз, `ResetAsync()` — Respawn и `FLUSHDB`
  перед каждым тестом), `TestAuthentication` (заголовки `X-Test-Account`/`X-Test-Role`), `DatabaseReset`,
  `TestClock` (`FakeTimeProvider`). API регистрирует `TimeProvider.System`, фабрика подменяет его. Все четыре
  прежних тестовых проекта переведены на каркас; образец — `backend/tests/Platform.Api.IntegrationTests`
  (образцовый модуль заметок: успешный путь, 401/403, ошибка валидации, кэш в Redis, часы, сброс). Раннер —
  по-прежнему VSTest: переход на Microsoft.Testing.Platform отклонён (TRX и сводка CI), причины — в
  [ADR-0008](adr/0008-backend-test-framework.md). Правила — [TESTING.md](TESTING.md).
- Демо-данные и фабрики объектов (E01-17): библиотека `backend/src/Seeding` (`Platform.Seeding`) — `SeedData`
  (зерно и `TimeProvider`; идентификаторы `Id(kind, n)` и случайные последовательности выводятся из зерна),
  `ObjectFactory<T>`, `IDemoSeeder`, `AddMissingAsync` (идемпотентная запись по идентификаторам); команда
  `dotnet run --project backend/tools/Seed` — миграции, затем демо-сидеры модулей из `SeedCatalog`; в `Staging` и
  `Production` отказывается без `Seed:AllowProtectedEnvironment=true`. Тестам — `TestData.Create()`,
  `IntegrationApiFactory.Data`, `SaveAsync<TContext>`, `SeedDemoAsync()`. Фабрики живут в коде модуля (не в
  `Platform.Testing`), так их использует и сидер. Образец — `NoteFactory` и `NotesDemoSeeder` модуля заметок.
  Каталог сидера пока пуст: модулей продукта нет; пользователи, блоги, темы, посты и комментарии появятся в
  E01-19 (#287) по мере модулей. Правила — [ADR-0009](adr/0009-demo-data-and-factories.md).
- Образы и выкладка (E01-03): `scripts/images.ps1` собирает пять образов — `api`, `workers`, `migrator`
  (`backend/Dockerfile`, три цели), `frontend` (сервер Nuxt) и `admin` (Vite за nginx без root); базовые образы
  закреплены по дайджесту. Workflow `images` на каждом PR собирает их и прогоняет смоук выкладки, после слияния в
  `main` публикует в GHCR (`ghcr.io/<владелец>/<репозиторий>/<образ>`, теги `sha-<коммит>` и `latest`; релиз
  release-please добавляет `<версия>` и `<мажор>.<минор>`). Стенд stage — `deploy/compose.stage.yaml` (образы,
  PostgreSQL, Redis, RabbitMQ, Caddy с TLS; хосты в окружении `Staging`), выкладка — `scripts/deploy.ps1`: мигратор
  новой версии, затем замена контейнеров с ожиданием healthcheck; откат — выкладка прежнего тега. Workflow
  `deploy` выкладывает тег вручную на `stage` или `production` (по SSH к Docker сервера) и вызывается после
  слияния, когда переменная `DEPLOY_STAGE` = `true`; без неё выкладка пропускается, и секреты хостинга CI не нужны.
  `scripts/deploy-test.ps1` статически проверяет Dockerfile, compose-файл и workflow (шаг `deploy config`), а смоуком
  выкладывает, обновляет, роняет миграцию и откатывает стенд на Docker раннера (задание `images / build`; локально
  — при `VERIFY_IMAGES=1`). Инструкция и список того, что нужно от владельца, — [DEPLOY.md](DEPLOY.md). Сервер,
  домен и значения окружений GitHub пока не заданы: stage не подключён.
- Временный бренд (E02-01): `brand/brand.json` — единственное место названия (kitabber), знака, палитры
  светлой и тёмной темы и набора иконок (Lucide, `lucide-vue-next`, ISC). Оба клиента показывают в шапке
  `BrandLogo`, заголовок и значок вкладки берутся оттуда же; витрина бренда — страница `/brand` сайта. Образы
  получают `brand/` именованным контекстом сборки. Vitest с Vue Test Utils и happy-dom подключён в обоих
  клиентах (`pnpm test`, шаг verify «unit tests»); Playwright и JUnit-отчёты клиента — #17.
- Дизайн-токены (E02-02, [ADR-0012](adr/0012-design-tokens.md)): `brand/tokens.json` плюс палитра `brand.json` →
  генератор `brand/tokens.ts` → закоммиченный `brand/tokens.css` (CSS-переменные `--color-*`, `--font-*`,
  `--space-*`, `--radius-*`, `--size-*`), подключён в обоих клиентах. Светлая и тёмная палитры: атрибут
  `data-theme` или системная тема; переключателя темы пока нет (FR-UI). Типографика для лонгридов: 18 px, 1,7,
  строка до 40rem. stylelint (`pnpm run lint`) запрещает в стилях компонентов сырые цвета и размеры; витрина
  токенов — страница `/tokens` сайта. `BrandLogo` красится токенами и следует теме страницы.

- Спецификации ключевых экранов (E02-03, [docs/ui](ui/README.md)): лента, пост, редактор, блог, тема, комментарии,
  уведомления и настройки — схема раскладки на 1440 и 390 px, размеры именами токенов, состояния «Пусто»,
  «Загрузка», «Ошибка» и ссылки на FR. Решения, которых нет в ТЗ (адреса страниц, пороги глубины веток, раскладка
  настроек, высоты шапки и обложки), помечены «предложение, требует подтверждения». Шаг verify «ui specs»
  (`scripts/ui-specs-test.ps1`, случай в самопроверке) проверяет файлы, разделы, FR, токены и якоря ссылок в `docs/`.
- Базовые компоненты (E02-04, [ADR-0013](adr/0013-ui-components.md)): `frontend/components/ui/` — `UiButton`,
  `UiField`, `UiMenu`, `UiTabs`, `UiModal` (уже 640 px — шторка снизу), `UiSheet`, `UiToast` с `UiToaster` и
  `useToast()` (тосты подключены в `app.vue`), `UiAvatar`. Клавиатура и ARIA по шаблонам WAI-ARIA: ловушка фокуса
  и Esc в диалогах, стрелки в меню и вкладках, живые области тостов. Витрина со всеми состояниями и переключателем
  темы — страница `/ui` сайта. Новые токены `--color-overlay` и `--color-shadow`. Админка компоненты пока не
  использует; сквозные проверки на 1440 и 390 px — #17.

Функций продукта пока нет: оба клиента — пустые страницы с логотипом в шапке, у API есть только `/health`.

## Базовая линия тестов

| Набор | Число тестов |
| --- | --- |
| Сервер, модульные | 30 (`Platform.Api.Tests`: 13 — курсорная пагинация и часы; `Platform.Configuration.Tests`: 12, проверка настроек и файлы секретов; `Platform.Seeding.Tests`: 5, зерно, идентификаторы и фабрики) |
| Сервер, интеграционные | 80 (`Platform.Api.Tests`: 22 в процессе, без Docker; `Platform.Configuration.Tests`: 7, настоящие хосты и сверка с репозиторием; `Platform.Persistence.IntegrationTests`: 7 на PostgreSQL (миграции, сброс Respawn), 7 без базы; `Platform.Messaging.IntegrationTests`: 6 на контейнерах, 11 без Docker; `Platform.Seeding.Tests`: 5, команда сидера без базы (окружения, настройки); `Platform.Api.IntegrationTests`: 15, API на PostgreSQL и Redis и сидер на PostgreSQL) |
| Клиент, компонентные | 99 (`frontend`: 96; из них 62 — базовые компоненты E02-04: кнопка, поле, меню, вкладки, окно и шторка, тосты, аватар, полнота витрины `/ui` и переключение темы; прежние 34 — бренд в одном месте, копии `BrandLogo` совпадают, нет картинок, иконки и лицензия, контраст WCAG AA, отличие от цветов DTF, `BrandLogo`; токены: файл совпадает с генератором, роли палитр, контраст всех текстовых цветов, типографика чтения, линтер стилей ловит 10 видов сырых значений и пропускает токены, линтер в обоих клиентах, только известные переменные; витрина токенов; `admin`: 3 — `BrandLogo`, лицензия иконок) |
| Сквозные (в каждом из двух профилей) | 0 |

Число не должно уменьшаться; уменьшение объясняется в PR.

## Открытые вопросы к владельцу

Ни один не блокирует M0–M3.

| Вопрос | Когда нужен ответ | Связанные задачи |
| --- | --- | --- |
| Финальное название и бренд (сейчас временные kitabber и палитра ADR-0011; меняются правкой `brand/brand.json`) | До открытой беты (M5) | E02-01 |
| Подтвердить ADR-0002: свой провайдер OpenID Connect с первого дня | До начала M1 | E18-01, E18-02 |
| Какие проекты будут объединять аккаунты и на каком они стеке | До M7 | E18-06 |
| Хостинг (сервер с Docker), домен, почтовый сервис, хранилище; что задать для stage — в [DEPLOY.md](DEPLOY.md#что-нужно-от-владельца) | До первой выкладки на stage | E01-03, E01-12 |
| Где держать секреты стендов: выкладка сделана на секретах окружений GitHub (рекомендация ADR-0007) — подтвердить или выбрать другое | До первой выкладки на stage | E01-03 |
| Ключи внешних провайдеров входа (Яндекс, VK, Google, Telegram) | До M2 | E03-07 |
| Подтвердить ADR-0013: витрина компонентов — страница `/ui` сайта вместо Histoire или Storybook, как предлагала задача #14 | До E02-05 (#15) | E02-04 |
| Итоговый текст правил сообщества | До M4 | E15-17 |
| Бот Telegram, служебный аккаунт и `api_id`, сервер вне России, тестовый канал | До M6 | E19-01 |
| Переносить ли мост с Telegram раньше (до открытой беты) | До конца M3 | эпик E19 |
| Предложения в спецификациях экранов ([docs/ui](ui/README.md), пометка «предложение, требует подтверждения»); пункты меню «коллекция» и «подписка» из раздела 5 ТЗ не показываются | До каркасов #15 и #16 | E02-03 |
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
| Обязательная проверка `images / build` | Сборка образов и смоук выкладки на каждом PR (E01-03); сейчас запускается, но не обязательна | Settings → Branches → правило `main` |
| Окружения `stage` и `production`, переменная `DEPLOY_STAGE` | Подключение стендов; полный список значений — [DEPLOY.md](DEPLOY.md#что-нужно-от-владельца) | Settings → Environments, Settings → Variables |
