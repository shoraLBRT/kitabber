# Состояние проекта

Обновляется в каждом PR. Последнее обновление: 20 ноября 2026 (#25).

## Текущая веха

**M1. MVP** — задачи и критерий выхода в [ROADMAP.md](ROADMAP.md).

**M0. Фундамент — завершена.** Критерий выхода (PR проверяется автоматически; пустой каркас открывается на 1440 и
390 px) показан автотестом: сквозные тесты Playwright (E01-16) открывают каркас в профилях desktop и mobile в
`ci / verify`. Последняя задача вехи, P2 #302 (кэш системных пакетов Playwright в CI), закрыта.

## Следующая задача

Первая открытая задача по [ROADMAP.md](ROADMAP.md) с закрытыми зависимостями.

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
  [ADR-0014](adr/0014-typed-api-client.md) (клиент API: `openapi-typescript` и `openapi-fetch` в каждом клиенте,
  проверка расхождения со снимком OpenAPI; принято, задача E01-08).
  [ADR-0015](adr/0015-observability.md) (наблюдаемость: OpenTelemetry с выдачей по OTLP, `traceparent` от Nuxt,
  JSON-логи, `/health/live` и `/health/ready`, `/metrics` для Prometheus; принято, задача E01-09).
  [ADR-0016](adr/0016-rate-limiting.md) (лимиты частоты: фиксированные окна в Redis, по аккаунту и по IP, политики
  из настроек, доверенные прокси; принято, задача E01-10).
  [ADR-0017](adr/0017-mail.md) (почта: шаблоны-файлы с локализацией и снимками, рендер у отправителя, доставка
  воркерами через очередь, SMTP на MailKit за `IEmailSender`, Mailpit в тестах; принято, задача E01-12).
  [ADR-0018](adr/0018-openid-connect-provider.md) (вход: OpenIddict 7 в модуле Identity, только код авторизации с
  PKCE S256, публичные клиенты из настроек, JWT-токены доступа, проверка в API по `Auth:Authority`, ключи — секреты
  стенда, сайт входит через свой сервер на `openid-client`; принято, задача E18-02).
  [ADR-0019](adr/0019-realtime-channel.md) (реальное время: один хаб SignalR в API, темы `account:` и `post:` с
  политиками подписки, билет хаба на 60 секунд от сервера сайта вместо токена в браузере, бесконечное тихое
  переподключение, Redis backplane; предложено в задаче E01-11).
- Бэклог в GitHub Issues: 262 задачи в 19 эпиках и 8 вехах; порядок — в [ROADMAP.md](ROADMAP.md).
- Процесс: `CLAUDE.md`, `scripts/verify.ps1`, CI (`ci / verify`), проверка заголовка PR, release-please,
  конфигурация Renovate (в том числе regex-менеджер для версии gitleaks в `ci.yml`, #285).

- Каркас монорепозитория (E01-01): решение .NET `backend/Platform.slnx` (хосты `Api` и `Workers`, net10.0),
  веб-клиент на Nuxt 3 в `frontend/`, админка на Vue 3 + Vite в `admin/`; общие `.editorconfig`,
  `.prettierrc.json`, `global.json`. Структура и линтеры описаны в [ARCHITECTURE.md](ARCHITECTURE.md).
- `scripts/verify.ps1` проверяет структуру репозитория, сборку и `dotnet format` бэкенда, ESLint, Prettier,
  проверку типов и `pnpm build` обоих клиентов. CI ставит .NET по `global.json`, pnpm и Node 22.
- CI (E01-02): `ci / verify` запускает `scripts/verify.ps1` с кэшами NuGet и pnpm и тайм-аутом 10 минут;
  таблица шагов и сводка отчётов о тестах (`artifacts/test-results`, TRX и JUnit) видны в сводке запуска, отчёты
  выкладываются артефактом. Самопроверка `scripts/verify-selftest.ps1` (шаг verify) доказывает, что упавший шаг
  роняет проверку, линтеры отвергают плохой код и workflow соблюдает эти ограничения. Системные пакеты Chromium
  (#302) ставятся из кэша .deb-файлов (`scripts/ci/playwright-deps.sh`), а не из зеркала Ubuntu на каждом прогоне;
  подробности — [TESTING.md](TESTING.md).
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
- Типизированный клиент API (E01-08, [ADR-0014](adr/0014-typed-api-client.md)): `frontend/api/` и `admin/src/api/` —
  `schema.d.ts`, сгенерированный `openapi-typescript` 7 из снимка `backend/src/Api/openapi/v1.json` командой
  `pnpm run api:generate`, и `createApiClient()` на `openapi-fetch`. `tests/api-client.spec.ts` в обоих клиентах
  запускает ту же команду и сравнивает с закоммиченным файлом, запрещает ручные `fetch`/`$fetch`/`useFetch` и строки
  `/api/` вне клиента и проверяет, что ошибка приходит как problem details. Пока `paths` пуст. Клиент сайта
  доступен как `useNuxtApp().$api` (`plugins/api.ts`, E01-09); адрес API — `runtimeConfig.apiBase`
  (`NUXT_API_BASE`, для отрисовки на сервере; на стенде `http://api:8080`) и `runtimeConfig.public.apiBase`
  (`NUXT_PUBLIC_API_BASE`, для браузера; по умолчанию тот же адрес сайта).
- Наблюдаемость (E01-09, [ADR-0015](adr/0015-observability.md)): библиотека `backend/src/Observability`
  (`AddPlatformObservability`) в API и воркерах — трассы, метрики и логи OpenTelemetry по источникам `Platform.*`,
  `Npgsql`, `MassTransit`, исходящему HTTP и среде .NET; выдача по OTLP только при `OTEL_EXPORTER_OTLP_ENDPOINT`.
  Логи в консоль — JSON с `TraceId`/`SpanId` (в Development — `simple`). Клиент API сайта ставит W3C `traceparent`
  на каждый запрос, при серверном рендеринге — в трассе страницы; API продолжает трассу до спана Npgsql, `traceId`
  в problem details тот же. У API: `/health/live` (без проверок), `/health/ready` (проверки с тегом `ready`: база
  модуля из `AddModuleDbContext`, кэш из `AddPlatformCache`, шина MassTransit; 503 при сбое) и `/metrics` в формате
  Prometheus (пакет beta). Прежний `/health` убран; healthcheck API на стенде — `/health/ready`, Caddy отдаёт
  `/health/*`, но не `/metrics`. Фейк коллектора для тестов — `TestTelemetry`. Тесты — `ObservabilityTests`
  (`Platform.Api.Tests`), `ObservabilityIntegrationTests` (`Platform.Api.IntegrationTests`: трасса до базы и
  готовность на PostgreSQL и Redis), `WorkerObservabilityTests` (`Platform.Configuration.Tests`),
  `frontend/tests/api-trace.spec.ts`. Коллектора, хранилищ и оповещений пока нет — см. открытые вопросы.
- Лимиты частоты запросов (E01-10, [ADR-0016](adr/0016-rate-limiting.md)): `Platform.Http.RateLimiting` — промежуточное
  ПО после аутентификации считает каждый запрос под `/api` в политики `Default` (300 в минуту) и `Writes` (`POST`,
  `PUT`, `PATCH`, `DELETE`, 60 в минуту) по аккаунту, аноним — по IP (IPv6 — по /64); сверх лимита — 429,
  `Retry-After` и problem details `too_many_requests`. Модуль объявляет свою политику (`AddRateLimitPolicy`) и вешает
  её на эндпоинт (`.WithRateLimit`), исключение — `.WithoutRateLimit()`. Значения — настройки
  `RateLimiting:Policies:<имя>:...` и `RateLimiting:Enabled` ([CONFIG.md](CONFIG.md)). Счётчики — `IRateCounter` из
  `Platform.Caching` (Lua-скрипт `INCR` + `PEXPIRE` в Redis, фейк в памяти на `TimeProvider`), общие для всех
  экземпляров API; при сбое Redis запрос проходит. API теперь подключает кэш (`ConnectionStrings:Redis`).
  `X-Forwarded-For` принимается только от loopback и сетей `Http:TrustedProxies` (на стенде — частные сети Docker);
  Nuxt при серверной отрисовке передаёт API входящий `x-forwarded-for` (`frontend/api/forwarded.ts`). Тесты —
  `RateLimitingTests` (`Platform.Api.Tests`), `RateLimitingIntegrationTests` (два экземпляра API на одном Redis),
  счётчики в `CacheTests`, `frontend/tests/api-forwarded.spec.ts`. Капча и лимиты попыток входа — E03-12 (#32).
- Почта (E01-12, [ADR-0017](adr/0017-mail.md)): `Platform.Mail` — `IMailer.SendAsync(адрес, шаблон, культура,
  значения)` рендерит письмо и публикует `SendEmail`; воркеры (`SendEmailConsumer`) доставляют его через
  `IEmailSender` с повтором шины, после повторов — очередь `send-email_error`. Отправители: `SmtpEmailSender`
  (MailKit 4, MIT; `Mail:Smtp:*`, `Mail:From:*`) и фейк `InMemoryEmailSender` (`Mail:Provider=InMemory`). Шаблоны —
  файлы `backend/src/Mail/Templates/<культура>/` (тема, текст, HTML, общий макет), поля `{{имя}}`, название и цвета —
  из `brand/brand.json`; культура откатывается к родителю, затем к русскому. Пока один шаблон — `login-code` (код
  входа); шаблоны остальных писем добавят их задачи. Development шлёт в Mailpit (`localhost:1025`, интерфейс —
  `:8025`); тесты — `Platform.Mail.Tests` (снимки, очередь и повтор, Mailpit в Testcontainers: `MailpitFixture`,
  `MailpitClient` в `Platform.Testing`). Стенд берёт SMTP из окружения GitHub (`SMTP_*`, `MAIL_FROM`,
  [DEPLOY.md](DEPLOY.md#что-нужно-от-владельца)). API шину и почту ещё не подключает — это сделает первая задача,
  которой нужна почта из API.

- Настройки и секреты (E01-13): `Platform.Configuration` — окружения `Development`/`Test`/`Staging`/`Production`
  (другое имя не стартует), слои `appsettings.json` → `appsettings.{Окружение}.json` → user secrets → переменные
  окружения → командная строка → файлы секретов из `Secrets:Directory`. Библиотеки объявляют свои настройки
  (`Setting`: секрет, допустимые значения, когда обязательна), хост проверяет их в `ConfigurePlatform` до сборки и
  не стартует, называя каждую отсутствующую настройку и её переменную окружения; в Staging и Production секрет из
  файла настроек — тоже ошибка. Все настройки — в [CONFIG.md](CONFIG.md), тест сверяет документ с кодом и
  проверяет, что в `appsettings.json`/`Staging`/`Production` нет секретов. `scripts/secret-scan.ps1` (шаг
  `secret scan`) гоняет gitleaks по всем файлам, которые git закоммитил бы, после проверки на подложенном токене;
  CI ставит gitleaks 8.28.0 и сверяет архив с контрольными суммами релиза; версию обновляет Renovate (regex-менеджер в `renovate.json`, #285, самопроверка verify проверяет, что он находит версию в `ci.yml`); локально шаг пропускается без gitleaks. Тесты — `backend/tests/Platform.Configuration.Tests`.
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
- Модуль Identity (E18-01, [ADR-0002](adr/0002-identity.md), FR-ID-1): первый модуль платформы,
  `backend/src/Modules/Identity` (`Platform.Modules.Identity`), схема `identity`, таблица `accounts` (`id`,
  `created_at`) и миграция `CreateAccounts`. Аккаунт — только глобальный идентификатор: `account_id` — UUID v7 по
  часам `TimeProvider` (`Account.Create`, `AccountIds`); профиль (имя, никнейм, аватар) будет в модуле Blogs (#33)
  и сошлётся на аккаунт только по `account_id`. Почта (#26), внешние идентичности (#88), вход по OIDC (#24) и
  события `account.*` (#89) добавят свои задачи. Модуль подключён к мигратору (`ModuleCatalog`) и сидеру
  (`SeedCatalog`: 20 демо-аккаунтов `AccountFactory`; их идентификаторы — `Id("account", n)` из зерна, то есть
  UUID v8, а не v7, чтобы фабрики других модулей брали авторов без чтения схемы `identity`). Граница FR-ID-1 проверяется дважды: архитектурный тест
  читает модели EF Core всех модулей (столбцы почты, паролей и внешних идентификаторов только в `identity`, нет
  внешних ключей между схемами, таблицы только в своей схеме), интеграционный тест мигратора смотрит то же в
  реальной базе (`information_schema`, `pg_constraint`), чтобы поймать и миграцию на голом SQL.
- Вход по OpenID Connect (E18-02, [ADR-0018](adr/0018-openid-connect-provider.md), FR-ID-2): API подключает модуль
  Identity (`AddIdentityModule`, `MapIdentityEndpoints`; теперь API нужен `ConnectionStrings:Platform`). Identity —
  провайдер на OpenIddict 7: `/.well-known/openid-configuration`, `/.well-known/jwks`, `/connect/authorize`,
  `/connect/token`; только код авторизации с PKCE S256 (код 2 минуты, один раз; токены 15 минут), `sub` = `account_id`,
  токен доступа — JWT `at+jwt` с аудиторией `platform-api`. Таблицы OpenIddict — в схеме `identity`
  (`oidc_applications`, `oidc_authorizations`, `oidc_scopes`, `oidc_tokens`, миграция `AddOpenIddict`). Клиенты `web` и
  `admin` — публичные, из `Identity:Clients:*:RedirectUris`, точный адрес возврата; API пишет их в базу при старте
  (`OidcClientSync`). `/connect/authorize` берёт человека из cookie провайдера `identity.session`, без неё отправляет в
  окно входа (`Identity:SignInPath`, `/login`), с `prompt=none` отвечает `login_required`. Само окно входа и способы
  войти ещё не сделаны (#28, #30): локально и на стенде войти пока нечем, в тестах сессию ставит тестовый эндпоинт.
  API проверяет `Authorization: Bearer` (OpenIddict validation): без `Auth:Authority` — своим сервером, с внешним
  адресом — по его discovery; `HttpContext.User` несёт `sub`, лимиты частоты считаются по аккаунту. Ключи подписи и
  шифрования — `Identity:SigningKey`, `Identity:EncryptionKey` (секреты стенда; в Development и Test создаются при
  старте), [CONFIG.md](CONFIG.md#вход-модуль-identity-в-api). Сайт — клиент через свой сервер (`openid-client` 6.8):
  `/auth/sign-in` и `/auth/callback` (`frontend/server/routes/auth/`), токены — в памяти сервера Nuxt, браузеру —
  cookie `auth.session` (HttpOnly, SameSite=Lax), странице — только `useAccount().value.signedIn`; «Войти» ведёт на
  `/auth/sign-in`, вошедший видит меню аккаунта с временным именем «Аккаунт» (профиль — #33); клиент API на сервере
  добавляет токен. Стенд: Caddy отдаёт адреса провайдера API, выкладка пишет `SITE_URL`, нужны секреты
  `IDENTITY_SIGNING_KEY`, `IDENTITY_ENCRYPTION_KEY`, `SESSION_PASSWORD` ([DEPLOY.md](DEPLOY.md#что-нужно-от-владельца)).
  Тесты — `backend/tests/Platform.Modules.Identity.IntegrationTests/SignIn` (поток целиком, негативные пути, issuer из
  настройки, внешний провайдер-фейк, лимит по аккаунту, ключи и клиенты из настроек), `frontend/tests/auth-oidc.spec.ts`,
  сквозной `frontend/e2e/sign-in.spec.ts` на фейковом провайдере `frontend/e2e/fake-oidc-provider.mjs`. Не сделано
  здесь: обновление токенов, выход, общее хранилище сессий сайта (#29), вход в админку (клиент `admin` только
  зарегистрирован), окно входа (#28, #30).
- Канал реального времени (E01-11, [ADR-0019](adr/0019-realtime-channel.md)): библиотека `backend/src/Realtime`
  (`Platform.Realtime`, `AddPlatformRealtime`, `MapRealtime`) — хаб SignalR `/hubs/realtime` только для вошедших.
  Темы — группы `account:<id>` (соединение входит само) и `post:<id>` (`Subscribe`/`Unsubscribe`); право подписки —
  `IRealtimeTopicPolicy` вида темы (на чужой аккаунт и неизвестный вид — `topic_refused`; на пост пока любой вошедший,
  проверку видимости добавит модуль Posts). Модули шлют события через `IRealtimePublisher`, клиент получает `receive`
  с `{ topic, type, data }`. Браузер подключается по билету: `POST /auth/realtime-ticket` на сервере сайта →
  `POST /api/v1/realtime/tickets` с токеном человека (первый путь в снимке OpenAPI); билет — 60 секунд, в Redis под
  SHA-256, открывает только хаб (в адресе `access_token` или заголовком `Bearer`); без билета хаб принимает токен
  провайдера. Redis backplane включается вместе с кэшем (`Cache:Provider=Redis`, каналы `platform:realtime`). Клиент —
  `frontend/realtime/channel.ts`, `plugins/realtime.client.ts` (`@microsoft/signalr` 10, загружается только у
  вошедшего), `useRealtimeTopic`; переподключение без конца (0, 2, 5, 10, затем каждые 30 секунд) с новым билетом и
  повторной подпиской, прекращается при выходе. Стенд: Caddy отдаёт `/hubs/*` API; `nuxt dev` проксирует `/hubs`.
  Тесты — `RealtimeIntegrationTests` (`Platform.Api.IntegrationTests`, Redis: своё событие и не чужое, подписка на
  пост, отказ в чужом аккаунте и неизвестной теме, без билета, поддельный и просроченный билет, билет заголовком,
  билет не покупает билет, backplane между двумя экземплярами), `RealtimeSignInTests` (настоящие токены провайдера:
  билет за токен, просроченный токен), `frontend/tests/realtime.spec.ts`, сквозной `frontend/e2e/realtime.spec.ts` на
  фейковом API `frontend/e2e/fake-api.mjs` (long polling: подключение по билету, переподключение после обрыва с новым
  билетом). Не сделано: события продукта (их добавят комментарии, уведомления и счётчики), отправка из воркеров,
  закрытие соединений при выходе и блокировке (#29, модерация).
- Тесты границ модулей (E01-18): `backend/tests/Platform.Architecture.Tests` по именам сборок
  (`Platform.Modules.<Модуль>` / `.Contracts`) запрещает ссылки на внутренности чужого модуля, ссылки
  контрактов и общих сборок на модули, Telegram вне `Platform.Telegram.Gateway`, библиотеки учётных данных вне
  модуля `Identity`, чужую схему у `DbContext`. Проверка — по метаданным сборок, без сторонних библиотек.
  Нарушения ловятся на модулях-образцах `backend/tests/ArchitectureFixtures`. Правила —
  [ARCHITECTURE.md](ARCHITECTURE.md#границы-модулей).
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
  клиентах (`pnpm test`, шаг verify «unit tests»).
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
  использует.
- Каркас десктопа (E02-05): `frontend/components/shell/` — `ShellLayout` (ссылка «Перейти к содержимому», шапка,
  сетка из колонок `--size-menu`, `--size-feed`, `--size-aside` с промежутком `--space-6`; от 1240 px три колонки,
  925–1239 px без правой, 640–924 px одна колонка без левого меню, до 639 px во всю ширину; левое меню и правая
  колонка прилипают под шапкой), `ShellHeader` (знак, поиск — пока только событие `search`, «Написать» → `/write`,
  колокольчик → `/notifications` и меню аватара для вошедшего, «Войти» → `/login` для гостя; на телефоне — только
  знак и поиск), `ShellNav` (ленты, «Сообщения», «Рейтинг», темы с «Показать все», «О проекте», «Правила»,
  переключатель темы; текущий экран — `aria-current="page"`), `ShellAside` с двумя `ShellWidget` (состояния «пусто»,
  «загрузка», «ошибка» с «Повторить», «готово»), `ShellThemeSwitch` и `composables/useTheme.ts` (светлая, тёмная,
  системная; выбор хранится в `localStorage` этого браузера). Пункты и адреса меню — один список
  `frontend/utils/navigation.ts` (адреса — предложение из docs/ui). Раскладка `layouts/default.vue` оборачивает все
  страницы, витрины `/brand`, `/tokens`, `/ui` — раскладка `bare`. Каркас телефона (E02-06) — в слоте `mobile` у `ShellLayout`.
  Страниц лент, сообщений, тем и служебных пока нет: ссылки меню ведут на 404 (страницы 404 — #64).
- Каркас телефона (E02-06): `ShellTabBar` — нижняя панель «Лента», «Рейтинг», «Уведомления», «Сообщения», «Меню»
  ниже 925 px (на 640–924 px тоже: левого меню там нет), со счётчиками непрочитанного (проп `counts`, данных пока
  нет), текущей вкладкой `aria-current` и прокруткой наверх по повторному нажатию; `ShellWriteButton` — плавающая
  «Написать» ниже 640 px (на 640–924 px «Написать» остаётся в шапке); `ShellMenuScreen` на странице `/menu` —
  профиль или «Войти», черновики, закладки, настройки, темы, тема оформления, служебные ссылки, «Выйти». Безопасные
  зоны: `viewport-fit=cover`, отступы `env(safe-area-inset-*)` у шапки, панели и кнопки; вкладки высотой
  `--size-tab-bar` (56 px), нажимаемые строки не ниже `--size-touch` (44 px) — новые токены. Тосты ниже 925 px
  поднимаются над панелью. Пункты панели и меню аккаунта — в том же `frontend/utils/navigation.ts`.
- Тестовый каркас клиентов (E01-16): Vitest пишет JUnit в `artifacts/test-results/<клиент>/`; Playwright
  (`@playwright/test`, только Chromium) — `frontend/e2e` и `admin/e2e`, `pnpm e2e`, проекты `desktop` (1440×900) и
  `mobile` (390×844, сенсорный ввод), каждый тест в обоих. Тесты идут против продакшен-сборки, которую verify
  собирает шагом «build» (`node .output/server/index.mjs` и `vite preview` в `webServer`). JUnit — в
  `artifacts/test-results/<клиент>-e2e/`, при падении трасса и снимок экрана — в `…-e2e/failures` (артефакт CI
  `test-results`). Фикстура `frontend/e2e/fixtures.ts` роняет тест на ошибке скрипта или консоли; `makeScrollable` меняет DOM только после гидратации (`waitForHydration`, #300: иначе несовпадение гидратации сбрасывало прокрутку, тест «прокрутка наверх» падал в профиле desktop). Сквозные проверки
  каркаса из #15 и #16 (ширины 220/640/320, скрытие колонок на 1100 и 800 px, прилипание, нижняя панель, плавающая
  «Написать», экран «Меню», прокрутка наверх, безопасные зоны через эмуляцию Chromium) и витрины `/brand`, `/tokens`,
  `/ui` в обеих темах (у каждой ровно один `h1`, у `/brand` разделы тем — `h2`); в админке — дымовой тест. Снимков экрана для сравнения нет. Браузеры в CI кэшируются
  (`~/.cache/ms-playwright`).

Функций продукта пока нет: сайт — пустой каркас десктопа и телефона без данных, админка — пустая страница с логотипом, у API есть служебные `/health/live`, `/health/ready`, `/metrics` и адреса провайдера входа (E18-02), но окна входа ещё нет.

## Базовая линия тестов

| Набор | Число тестов |
| --- | --- |
| Сервер, модульные | 90 (`Platform.Mail.Tests`: 14 — шаблоны писем E01-12: снимок каждого шаблона, экранирование в HTML, незаполненное поле, неизвестный шаблон, выбор культуры с откатом на русский, название из `brand/brand.json` и его отсутствие в шаблонах; `Platform.Architecture.Tests`: 46 — границы модулей на сборках платформы, модули-образцы с нарушениями, разрешённые и запрещённые ссылки, схемы; из них 15 — FR-ID-1 E18-01: почта, пароли и внешние идентификаторы только в `identity`, нет внешних ключей между схемами, таблицы только в своей схеме, нарочно нарушающий контекст, распознавание имён столбцов; `Platform.Api.Tests`: 13 — курсорная пагинация и часы; `Platform.Configuration.Tests`: 12, проверка настроек и файлы секретов; `Platform.Seeding.Tests`: 5, зерно, идентификаторы и фабрики) |
| Сервер, интеграционные | 163 (`Platform.Modules.Identity.IntegrationTests`: 43, из них 3 — хаб реального времени E01-11 с токенами провайдера: билет за токен открывает хаб, просроченный токен не даёт ни билета, ни хаба; 34 — вход E18-02: discovery и ключи, поток с PKCE и токены с `sub` = `account_id`, API видит аккаунт, окно входа и `prompt=none`, удалённый аккаунт, чужой клиент и адрес возврата, нет PKCE и `plain`, неверный верификатор, повтор, чужой и просроченный код, просроченный и поддельный токен, лимит по аккаунту, issuer из настройки, внешний провайдер по `Auth:Authority`, 10 без базы — ключи и клиенты из настроек; 6 — модуль Identity E18-01: UUID v7 по часам, порядок идентификаторов, проверка версии, миграция только в схему `identity`, запись и чтение аккаунта, демо-аккаунты один раз с идентификаторами `Id("account", n)`; `Platform.Mail.Tests`: 6 — доставка E01-12: письмо через очередь воркеров, повтор после двух сбоев, неверный адрес у вызывающего, 3 на Mailpit в Testcontainers — SMTP из настроек с входом и без, имя отправителя; `Platform.Api.Tests`: 30 в процессе, без Docker, из них 8 — наблюдаемость E01-09: пробы, метрики Prometheus, трасса из `traceparent`, ошибка в логе и спане под trace id, пробы не трассируются, JSON-логи; `Platform.Configuration.Tests`: 15, настоящие хосты и сверка с репозиторием, из них 5 — телеметрия воркеров, 3 — настройки почты воркеров; `Platform.Persistence.IntegrationTests`: 9 на PostgreSQL (миграции, сброс Respawn; E18-01 — в базе реальных модулей нет столбцов учётных данных вне `identity` и внешних ключей между схемами, и запросы это ловят), 7 без базы; `Platform.Messaging.IntegrationTests`: 6 на контейнерах, 11 без Docker; `Platform.Seeding.Tests`: 5, команда сидера без базы (окружения, настройки); `Platform.Api.IntegrationTests`: 31 (прежняя запись 17 отставала на 1), из них 13 — канал реального времени E01-11 на Redis: событие своей темы и не чужой, подписка и отписка на пост, отказ в чужом аккаунте и неизвестных темах (5), без билета и с поддельным, просроченный билет, билет заголовком `Bearer`, билет только за токен, backplane между двумя экземплярами, новое соединение снова в теме аккаунта; API на PostgreSQL и Redis и сидер на PostgreSQL, из них 2 — трасса до спана базы и готовность) |
| Клиент, компонентные | 219 (`frontend`: 207; из них 9 — канал реального времени E01-11: новый билет на каждое подключение, бесконечные задержки переподключения, повторная подписка, повтор первого подключения и после отказа SignalR, остановка при выходе, события по темам, запрос билета у API с токеном и 401; 12 — вход E18-02: запрос кода с PKCE S256, обмен кода с верификатором, отказ при чужих state, nonce и issuer, возврат только на страницу сайта, настройки клиента; 5 — трассировка E01-09: разбор `traceparent`, трасса страницы, новый спан на вызов, один trace id на вызовы страницы, новая трасса в браузере; 4 — клиент API E01-08: команда генерации из снимка, файл совпадает с генератором, нет ручных обращений к API, базовый адрес и problem details; 5 — настройка Playwright E01-16: два профиля, каждый тест в обоих, трасса и снимок при падении, отчёты JUnit, продакшен-сборка; 32 — каркас телефона E02-06: пять вкладок по ТЗ и их адреса, текущая вкладка, счётчики и их озвучивание, прокрутка наверх повторным нажатием, экран «Меню» для гостя и вошедшего без «коллекции», «подписки» и монетизации, открытие из панели, контракт стилей — панель ниже 925 px, плавающая кнопка ниже 640 px, безопасные зоны и «чёлка», 44 px, вкладки не расширяют страницу, место под панелью и тосты над ней; 41 — каркас десктопа E02-05: ориентиры, ссылка к содержимому, порядок фокуса, пункты меню без монетизации и «коллекции», текущий пункт на разных адресах, шапка гостя и вошедшего, переключатель темы, состояния виджетов, контракт стилей — ширины колонок из токенов, прилипание, скрытие колонок по точкам перелома; 62 — базовые компоненты E02-04: кнопка, поле, меню, вкладки, окно и шторка, тосты, аватар, полнота витрины `/ui` и переключение темы; прежние 34 — бренд в одном месте, копии `BrandLogo` совпадают, нет картинок, иконки и лицензия, контраст WCAG AA, отличие от цветов DTF, `BrandLogo`; токены: файл совпадает с генератором, роли палитр, контраст всех текстовых цветов, типографика чтения, линтер стилей ловит 10 видов сырых значений и пропускает токены, линтер в обоих клиентах, только известные переменные; витрина токенов; `admin`: 12 — те же 4 проверки клиента API, `BrandLogo`, лицензия иконок, те же 5 проверок настройки Playwright) |
| Сквозные (в каждом из двух профилей) | 18 в профиле, 36 прогонов (`frontend`: 17, из них 2 — реальное время E01-11 на фейковом API: гость не подключается, вошедший подключается по билету и переподключается после обрыва с новым; 2 — вход E18-02 на фейковом провайдере: код с PKCE, токенов нет в хранилищах и cookie страницы, ошибка обратного вызова; раскладка профиля, прилипание колонок, 1100 px, 800 px, экран «Меню», прокрутка наверх, безопасные зоны, витрины `/brand`, `/tokens`, `/ui` в светлой и тёмной теме; `admin`: 1 — дымовой) |

Число не должно уменьшаться; уменьшение объясняется в PR.

## Открытые вопросы к владельцу

Ни один не блокирует M0–M3.

| Вопрос | Когда нужен ответ | Связанные задачи |
| --- | --- | --- |
| Финальное название и бренд (сейчас временные kitabber и палитра ADR-0011; меняются правкой `brand/brand.json`) | До открытой беты (M5) | E02-01 |
| Подтвердить ADR-0002: свой провайдер OpenID Connect с первого дня | До начала M1 | E18-01, E18-02 |
| Для выкладки входа на stage (ADR-0018) — завести секреты `IDENTITY_SIGNING_KEY`, `IDENTITY_ENCRYPTION_KEY`, `SESSION_PASSWORD` (команды — DEPLOY.md) | До первой выкладки на stage | E18-02 |
| Какие проекты будут объединять аккаунты и на каком они стеке | До M7 | E18-06 |
| Куда отправлять телеметрию: коллектор OpenTelemetry и хранилища (Grafana, Prometheus, Loki, Tempo у себя или облачный сервис), трекер ошибок, оповещения дежурному. Код готов (ADR-0015): нужен адрес `OTEL_EXPORTER_OTLP_ENDPOINT` и при необходимости ключ `OTEL_EXPORTER_OTLP_HEADERS` | До открытой беты (M5) | E01-09 |
| Хостинг (сервер с Docker), домен, почтовый сервис, хранилище; что задать для stage — в [DEPLOY.md](DEPLOY.md#что-нужно-от-владельца) | До первой выкладки на stage | E01-03, E01-12 |
| Почтовый сервис для stage и production: подойдёт любой с SMTP (ADR-0017), нужны домен отправителя и записи SPF, DKIM, DMARC; варианты и рекомендация — в PR #309 | До первой выкладки на stage | E01-12 |
| Где держать секреты стендов: выкладка сделана на секретах окружений GitHub (рекомендация ADR-0007) — подтвердить или выбрать другое | До первой выкладки на stage | E01-03 |
| Ключи внешних провайдеров входа (Яндекс, VK, Google, Telegram) | До M2 | E03-07 |
| Подтвердить ADR-0013: витрина компонентов — страница `/ui` сайта вместо Histoire или Storybook, как предлагала задача #14 | До E02-05 (#15) | E02-04 |
| Итоговый текст правил сообщества | До M4 | E15-17 |
| Бот Telegram, служебный аккаунт и `api_id`, сервер вне России, тестовый канал | До M6 | E19-01 |
| Переносить ли мост с Telegram раньше (до открытой беты) | До конца M3 | эпик E19 |
| Предложения в спецификациях экранов ([docs/ui](ui/README.md), пометка «предложение, требует подтверждения»); пункты меню «коллекция» и «подписка» из раздела 5 ТЗ не показываются. Каркас десктопа #15 сделан по ним: промежуток колонок `--space-6`, высота шапки `--space-16`, адреса меню (`frontend/utils/navigation.ts`), тема хранится в браузере, а не в аккаунте. Каркас телефона #16: нижняя панель и экран «Меню» (`/menu`, отдельная страница) показываются ниже 925 px, плавающая «Написать» — ниже 640 px; адреса `/menu`, `/profile`, `/drafts`, `/bookmarks`, `/settings`; на экране «Меню» есть темы и «Выйти»; на этих предложениях построены и сквозные тесты #17 | До экранов лент (M1): пока ответа нет, тесты каркаса фиксируют предложения | E02-03, E01-16 |
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
