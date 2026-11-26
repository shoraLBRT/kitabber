# Архитектура: структура репозитория

Стек и границы модулей — в [ADR-0001](adr/0001-stack.md). Здесь — как это разложено по каталогам и какими
инструментами проверяется. Документ обновляется в том же PR, что меняет структуру.

## Каталоги

```text
backend/                   решение .NET (Platform.slnx)
  Directory.Build.props    общие настройки проектов: net10.0, nullable, анализаторы, предупреждения = ошибки
  Directory.Packages.props версии NuGet-пакетов (центральное управление пакетами)
  src/Api/                 Platform.Api — веб-хост API (ASP.NET Core); снимок OpenAPI — openapi/v1.json
  src/Configuration/       Platform.Configuration — окружения, проверка настроек на старте, файлы секретов (ADR-0007)
  src/Http/                Platform.Http — соглашения REST: /api/v1, ProblemDetails, курсорная пагинация (ADR-0006), лимиты частоты (ADR-0016)
  src/Workers/             Platform.Workers — хост фоновых воркеров: шина, потребители и outbox модулей (ADR-0005)
  src/Messaging/           Platform.Messaging — шина MassTransit на RabbitMQ, outbox и inbox модуля (ADR-0005)
  src/Caching/             Platform.Caching — кэш IDistributedCache и счётчики IRateCounter на Redis (ADR-0005)
  src/Mail/                Platform.Mail — шаблоны писем (Templates/<культура>/), очередь SendEmail, SMTP и фейк (ADR-0017)
  src/Realtime/            Platform.Realtime — канал реального времени: хаб SignalR, темы, билеты хаба, Redis backplane (ADR-0019)
  src/Persistence/         Platform.Persistence — база модулей: ModuleDbContext, мигратор (ADR-0004)
  src/Observability/       Platform.Observability — OpenTelemetry: трассы, метрики, логи, выдача по OTLP (ADR-0015)
  src/Migrator/            Platform.Migrator — команда миграций: migrate, status, rollback <схема>
  src/Seeding/             Platform.Seeding — демо-данные: зерно, фабрики объектов, демо-сидеры модулей (ADR-0009)
  src/Modules/             модули из ADR-0001 (появляются с задачами модулей); Identity — аккаунты (ADR-0002); Editor — схемы блоков и очистка HTML (ADR-0023); Media — загрузка файлов и хранилище S3 (ADR-0030); Blogs — блоги и темы (ADR-0025); Posts — посты (ADR-0029); Comments — дерево комментариев (ADR-0048); Reactions — лайки постов и комментариев (ADR-0061), его контракт `Reactions.Contracts` — счётчики лайков для лучшего комментария ленты (ADR-0062); Preferences — настройки аккаунта на всех устройствах, пока тема (ADR-0070); Notifications — уведомления из событий Comments, Reactions и Blogs, только в хосте воркеров, его контракт `Notifications.Contracts` — подавление уведомлений, его реализует чёрный список Preferences (ADR-0092)
  tests/Platform.Testing/  общий каркас тестов: контейнеры, фабрики API, Respawn, часы (ADR-0008)
  tests/                   тестовые проекты: *.Tests без Docker, *.IntegrationTests на Testcontainers
  tests/ArchitectureFixtures/  модули-образцы с нарочным нарушением границ (Platform.Architecture.Tests)
  tools/Seed/              Platform.Seed — команда демо-данных: dotnet run --project backend/tools/Seed (ADR-0009)
frontend/                  веб-клиент на Nuxt 3 (Vue 3, TypeScript); api/ — типизированный клиент API (ADR-0014)
admin/                     админ-панель на Vue 3 + Vite (TypeScript); src/api/ — клиент API (ADR-0014)
brand/                     временный бренд: название, знак, палитра, набор иконок — brand.json (ADR-0011);
                           дизайн-токены tokens.json → tokens.css и правила линтера стилей (ADR-0012)
tools/                     инструменты не на .NET: фейковые сервисы (появится в своих задачах)
deploy/                    compose-файл стенда stage и шаблон его значений; выкладка — docs/DEPLOY.md
compose.yaml               локальное окружение: вся инфраструктура в Docker (E01-04)
.env.example               порты и пароли локального окружения; копия .env в git не попадает
docs/                      ТЗ, ADR, состояние проекта; docs/ui/ — спецификации ключевых экранов (E02-03)
dotnet-tools.json          локальные инструменты .NET (dotnet-ef); ставятся командой dotnet tool restore
scripts/verify.ps1         единственная команда проверки
```

Образы в `compose.yaml` закреплены по версии. Исключение — MinIO: официальные образы `minio/minio` и
`quay.io/minio/minio` больше не публикуются, поэтому берётся `cgr.dev/chainguard/minio` (MinIO, собранный
Chainguard из исходников; решение владельца, PR #278). Бесплатно доступен только тег `latest`, поэтому образ
закреплён по digest, а digest обновляет Renovate.

`frontend/` и `admin/` — независимые пакеты pnpm, у каждого свой `pnpm-lock.yaml` и свой
`pnpm-workspace.yaml` (там же список пакетов, которым разрешены скрипты установки, `allowBuilds`).
Тесты клиента: компонентные (Vitest) — в `tests/`, сквозные (Playwright, проекты `desktop` и `mobile`, только
Chromium) — в `e2e/` и `playwright.config.ts` каждого пакета; сквозные идут против продакшен-сборки (подробно —
[TESTING.md](TESTING.md#сквозные-тесты-playwright)).

Базовые компоненты интерфейса сайта (ADR-0013) лежат в `frontend/components/ui/Ui*.vue`: кнопка, поле, меню,
вкладки, модальное окно (на телефоне — шторка), шторка, тосты (`useToast()` и `UiToaster` в `app.vue`) и аватар;
помощники фокуса — `frontend/utils/focus.ts`. Витрина — страница `/ui`. Стили — только из токенов (ADR-0012).

Строки интерфейса сайта (E02-10, ADR-0063) — только из словаря `frontend/i18n/locales/ru.ts`: `t('ключ', параметры)`
из `frontend/i18n`, формы множественного числа по `Intl.PluralRules`, `formatNumber` и `formatDate` по языку из
`frontend/i18n/config.ts`, текст с разметкой — компонент `I18nText`. Второй язык — файл словаря рядом с `ru.ts` и
строка в `i18n/locales/index.ts`; недостающие тексты берутся из русского. Правило ESLint `i18n/no-raw-text`
(`frontend/eslint/i18n.mjs`) не пропускает текст мимо словаря в шаблонах и кириллицу в коде.

Согласие на cookie (E02-13, ADR-0066): выбор — в необходимой cookie `cookie_consent`, баннер `ShellCookieBanner` в
`app.vue`, настройки — `/settings/cookies`. Код, который ставит необязательную cookie (аналитика, сбор ошибок),
запускается только через `whenConsented(категория, start, cookies)` из `frontend/composables/useCookieConsent.ts`.

Контент справа налево (E02-14, ADR-0068): текстовые блоки, заголовки и комментарии — `dir="auto"`, RTL-фрагменты
внутри LTR-строки изолирует `frontend/utils/bidi.ts` (RLI…PDI), арабский шрифт Noto Naskh Arabic объявлен в
`frontend/assets/fonts.css` только для арабских букв и стоит первым в семействах токенов.

Учёт просмотров (E07-06, ADR-0071): браузер шлёт отчёты (открытие, показы карточек, время чтения) на `/bff/views`
через `frontend/composables/useViews.ts`, сервер сайта передаёт их в `POST /api/v1/posts/{id}/open`, `…/impressions`,
`…/{id}/reading` с токеном, строкой браузера и адресом. В модуле Posts учёт — за внутренним `IViewCounter`
(`BufferedViewCounter`: Redis-буфер `IWriteBuffer` с пакетным сбросом `ViewFlusher`, ADR-0083); аналитическая база (E16-18) подключается на его месте.

«Горячесть» (E08-05, ADR-0086): фоновая служба `HotnessService` в API раз в минуту запускает `HotnessCalculator`
модуля Posts под advisory-блокировкой PostgreSQL. Он читает рейтинг постов через `Reactions.Contracts.IReactionRatings`
и число комментариев через `Comments.Contracts.ICommentDirectory`, а просмотры — из своей таблицы, и пишет счета в
`posts.post_hotness`; лента `GET /api/v1/feeds/popular` читает их оттуда. Коэффициенты — настройки `Posts:Hotness:*`.

«Моя лента» (E08-04, ADR-0089): `GET /api/v1/feeds/my` берёт подписки читателя через `Blogs.Contracts.IBlogSubscriptions`
и отбрасывает то, что называют источники `Posts.Contracts.IFeedExclusionSource` (чёрный список, скрытое) — их реализуют
и регистрируют модули, которые ведут эти списки. Лента у каждого своя и в общий кэш лент (ADR-0080) не попадает.

Каркас сайта (E02-05) — `frontend/components/shell/Shell*.vue`: `ShellLayout` собирает шапку, левое меню, центральную
колонку (`<main id="content">`) и правую колонку виджетов и переключает раскладки по точкам перелома из токенов.
Его подключает раскладка Nuxt `frontend/layouts/default.vue`; витрины дизайна идут в раскладке `bare`. Пункты меню и
их адреса — один список `frontend/utils/navigation.ts`, общий для десктопа и телефона; выбор темы —
`frontend/composables/useTheme.ts`. Каркас телефона (E02-06) стоит в слоте `mobile` у `ShellLayout`: нижняя панель
`ShellTabBar` (ниже 925 px) и плавающая «Написать» `ShellWriteButton` (ниже 640 px); экран «Меню» `ShellMenuScreen` —
страница `frontend/pages/menu.vue`. Высота панели — `--size-tab-bar` с отступами и рамкой; одно и то же выражение
используют отступ под содержимым, кнопка и тосты.

Редактор (E05-01, ADR-0026): `frontend/editor/` — модель документа в формате ADR-0023 и перевод из данных Editor.js
и обратно (`document.ts`), история отмены и повтора снимками документа (`history.ts`), каретка по смещению в тексте
(`caret.ts`) и движок `EditorEngine` с реализацией на Editor.js (`engine.ts`). Компоненты —
`frontend/components/editor/`: `BlockEditor` (движок, история, клавиши, состояния) и `EditorWindow` (окно 800×700 px
или весь экран на телефоне); страница нового поста — `frontend/pages/write.vue`. Новый тип блока подключает свой
инструмент Editor.js в `engine.ts` и свой перевод в `document.ts`. Встроенная разметка текстовых полей (E05-02,
ADR-0028) — `inline.ts`: белый список сервера, проверка ссылок (`normalizeLink`), очистка поля (`cleanInline`, её
зовёт `toPostDocument`) и вставки (`cleanPastedHtml`, её подставляет движок на `paste` и `drop`; списки она отдаёт
`ul`/`ol`, их берёт блок «Список» — ADR-0042); инструменты
панели «маркер» и «ссылка» — `tools.ts`.

## База данных

Одна база PostgreSQL, у каждого модуля своя схема, свой `DbContext` и своя таблица истории миграций
`<схема>.__ef_migrations_history`. Имена в базе — snake_case. Миграции накатывает отдельная команда, а не старт
API:

```sh
dotnet run --project backend/src/Migrator             # migrate: все ожидающие миграции всех модулей
dotnet run --project backend/src/Migrator -- status   # применено и ожидает по модулям
dotnet run --project backend/src/Migrator -- rollback <схема>  # откатить последнюю миграцию модуля
```

Список модулей мигратора — `backend/src/Migrator/ModuleCatalog.cs`. Правила (справочные данные в миграциях,
обратимость, как добавить модуль) — в [ADR-0004](adr/0004-database-migrations.md).

## Демо-данные

```sh
dotnet run --project backend/tools/Seed                              # миграции, затем демо-данные всех модулей
dotnet run --project backend/tools/Seed -- --Seed:RandomSeed=42      # другое зерно — другие данные
```

Данные выводятся из одного зерна и часов (`SeedData`), повторный запуск ничего не добавляет. Модуль кладёт рядом с
сущностью фабрику (`ObjectFactory<T>`) и демо-сидер (`IDemoSeeder`) и добавляет строку в
`backend/tools/Seed/SeedCatalog.cs`; те же фабрики строят объекты в тестах. В `Staging` и `Production` сидер
отказывается без `Seed:AllowProtectedEnvironment=true`. Правила — [ADR-0009](adr/0009-demo-data-and-factories.md).

## Очереди, фоновые воркеры и кэш

События между модулями идут через RabbitMQ (MassTransit 8, Apache-2.0; 9+ — коммерческая лицензия). Хост
`Platform.Workers` (`backend/src/Workers/WorkerHost.cs`) подключает шину и кэш; задача модуля добавляет в
`WorkerHost.AddModules` свои потребители и outbox:

```csharp
bus.AddConsumers(typeof(SomeModule).Assembly);   // потребители модуля
bus.AddModuleOutbox<SomeDbContext>();             // outbox в схеме модуля
```

Контекст модуля, который публикует события, вызывает `modelBuilder.AddOutboxTables()`; его потребители
объявляются через `ModuleConsumerDefinition<TConsumer, TContext>` (повторная доставка отбрасывается inbox).
Код модуля публикует через `IModuleOutbox<SomeDbContext>`: в хосте с несколькими публикующими модулями каждый пишет
в outbox своей схемы.
Настройки: `Messaging:Transport` (`RabbitMq` | `InMemory`), `ConnectionStrings:RabbitMq`
(`amqp://user:password@host:port/`), `Cache:Provider` (`Redis` | `InMemory`), `ConnectionStrings:Redis`;
для локального окружения они заданы в `backend/src/Workers/appsettings.Development.json`. Запуск:
`dotnet run --project backend/src/Workers`. Правила — [ADR-0005](adr/0005-messaging-and-cache.md).

Страницы лент кэшируются целиком под ключом с поколением; изменение поста, видного в ленте, пишет новое поколение, а
срок жизни страницы (30 секунд) ограничивает отставание данных других модулей на карточке — `FeedPageCache`,
[ADR-0080](adr/0080-feed-cache.md).

## REST API

Соглашения — [ADR-0006](adr/0006-rest-api-conventions.md), код — `Platform.Http`. Хост подключает их двумя вызовами
(`AddPlatformApi()`, `UsePlatformApi()`), модуль — так:

```csharp
services.AddValidation();                    // валидаторы публичных типов запроса этой сборки
services.AddApiEndpoints<PostsEndpoints>();  // PostsEndpoints : IApiEndpoints, MapV1(v1) без префикса /api/v1
```

Ошибки — problem details с полем `code` (`ApiProblems.*` или `ApiProblemException`), списки — `PageQuery` →
`ToRequest<TCursor>()` → `ToPage(...)`. Документ OpenAPI: `/openapi/v1.json` у работающего хоста и
`backend/src/Api/obj/openapi/Platform.Api.json` после сборки; снимок `backend/src/Api/openapi/v1.json` обновляется
запуском тестов с `UPDATE_OPENAPI_SNAPSHOT=1` и коммитится вместе с изменением API.

Лимиты частоты ([ADR-0016](adr/0016-rate-limiting.md)) хост включает `AddPlatformRateLimiting(configuration)`; каждый
запрос под `/api` считается в политики `Default` и (для записей) `Writes`. Эндпоинту с особым риском (вход, отправка
формы, загрузка) модуль даёт свою политику:

```csharp
services.AddRateLimitPolicy("posts.create", new RateLimitPolicy(10, TimeSpan.FromMinutes(10)));
posts.MapPost("/", Create).WithRateLimit("posts.create");
```

Клиенты берут типы API только из снимка ([ADR-0014](adr/0014-typed-api-client.md)): `pnpm run api:generate` в
`frontend/` и `admin/` пересобирает `schema.d.ts`, вызовы — через `createApiClient()` (`openapi-fetch`). Тест
`tests/api-client.spec.ts` каждого клиента падает, если `schema.d.ts` отстал от снимка или в коде есть ручной
`fetch` к API.

Почта ([ADR-0017](adr/0017-mail.md)): код модуля вызывает `IMailer.SendAsync(адрес, "login-code", культура, значения)`
— письмо рендерится сразу из `backend/src/Mail/Templates` и уходит в очередь сообщением `SendEmail`; воркеры
доставляют его через `IEmailSender` (SMTP или фейк) с повтором шины. Новый шаблон — три файла в папке культуры и
образцовые значения в `TemplateTests.Samples`; снимок пишет тест (`UPDATE_SNAPSHOTS=1`).

Вход ([ADR-0018](adr/0018-openid-connect-provider.md)): модуль `Identity` — провайдер OpenID Connect (OpenIddict) в
процессе API, код авторизации с PKCE S256. Каждый запрос к API с `Authorization: Bearer` проверяется провайдером из
`Auth:Authority` (по умолчанию своим), `HttpContext.User` несёт `sub` = `account_id`; эндпоинт модуля требует входа
через `.RequireAuthorization()`. Сайт входит через свой сервер: `frontend/server/routes/auth/` (`sign-in`,
`callback`), токены — в памяти сервера Nuxt, браузеру — cookie `auth.session`; страница знает только
`useAccount().value.signedIn`, а клиент API на сервере сам добавляет токен (`plugins/api.ts`).

Реальное время ([ADR-0019](adr/0019-realtime-channel.md)): один хаб SignalR `/hubs/realtime` в API
(`Platform.Realtime`), только для вошедших. Событие адресовано теме: `account:<id>` (соединение входит в неё само) или
`post:<id>` (клиент подписывается методом `Subscribe`; кто может подписаться на тему вида, решает её
`IRealtimeTopicPolicy`). Модуль отправляет событие через `IRealtimePublisher.PublishAsync(тема, тип, данные)`; клиенты
только слушают, действия идут через REST. Несколько экземпляров API делят события через Redis backplane (когда
`Cache:Provider=Redis`). Браузер открывает хаб по билету: страница просит его у своего сервера
(`POST /auth/realtime-ticket`), сервер Nuxt — у API (`POST /api/v1/realtime/tickets`) с токеном человека; билет живёт
60 секунд и открывает только хаб. Клиент сайта — `frontend/realtime/channel.ts` и `plugins/realtime.client.ts`,
компонентам — `useRealtimeTopic(тема, обработчик)`.

## Настройки и секреты

Правила — [ADR-0007](adr/0007-configuration-and-secrets.md), перечень настроек — [CONFIG.md](CONFIG.md), код —
`Platform.Configuration`. Библиотека объявляет то, что читает (`MessagingSettings.All`, `CacheSettings.All`), хост
собирает объявления своих библиотек и вызывает `builder.ConfigurePlatform(settings)` до `Build()`:

```csharp
public static IReadOnlyList<Setting> Settings { get; } = [.. MessagingSettings.All, .. CacheSettings.All];
builder.ConfigurePlatform(Settings);   // нет настройки → InvalidConfigurationException с ключом и переменной
```

Секреты стендов приходят переменными окружения или файлами из `Secrets:Directory`; в git их нет (gitleaks в
`scripts/verify.ps1`, `.gitleaks.toml`).

## Экраны

Раскладка, размеры и состояния ключевых экранов (лента, пост, редактор, блог, тема, комментарии, уведомления,
настройки) на 1440 и 390 px — [docs/ui](ui/README.md). Размеры и цвета там записаны именами токенов ADR-0012;
каркасы #15 и #16 и экраны продукта строятся по этим спецификациям.

## Имена в коде

Название продукта временное и задаётся в одном месте, поэтому в именах сборок, пространств имён и пакетов его
нет: сборки называются `Platform.*`, пакеты — `platform-frontend` и `platform-admin`. В интерфейсе название,
знак и палитру берут из `brand/brand.json` (ADR-0011); тест `frontend/tests/brand.spec.ts` запрещает писать
название где-либо ещё в `frontend/` и `admin/`. Модули получат имена
вида `Platform.Modules.<Модуль>` и `Platform.Modules.<Модуль>.Contracts`.

## Границы модулей

Правила проверяет `backend/tests/Platform.Architecture.Tests` (входит в `scripts/verify.ps1`, шаг тестов
backend). Сборки различаются по имени, поэтому правила сразу действуют на будущие модули:
`Platform.Modules.<Модуль>` — внутренности модуля, `Platform.Modules.<Модуль>.Contracts` — его контракты и
события, `Platform.Api`, `Platform.Workers`, `Platform.Migrator`, `Platform.Seed` — хосты, остальные
`Platform.*` — общие сборки.

1. Модуль не ссылается на внутренности другого модуля — только на его `.Contracts` (и на события в них).
2. Контракты не ссылаются ни на чьи внутренности, в том числе своего модуля; на чужие контракты — можно.
3. Общие сборки (`Persistence`, `Messaging` и др.) не ссылаются на внутренности модулей.
4. Внутренности модулей подключают только хосты: они собирают приложение.
5. Библиотеки Telegram (`Telegram.Bot`, `WTelegramClient`, `TdLib`) — только в `Platform.Telegram.Gateway*`
   (сервис-шлюз за `ITelegramGateway`, ADR-0003).
6. Библиотеки хранения и хеширования учётных данных (ASP.NET Core Identity, сервер OpenIddict, BCrypt и др.) —
   только во внутренностях модуля `Identity` (ADR-0002); проверка токенов (`OpenIddict.Validation`) разрешена всем.
7. `DbContext` модуля (`IModuleDbContext`) живёт только во внутренностях модуля, и его схема — имя модуля в
. Таблицы модуля лежат только в его схеме, внешних ключей между схемами нет: модуль ссылается на чужой объект
   только по идентификатору. Столбцы почты, паролей и внешних идентификаторов входа (`email`, `password`,
   `external_id`, `provider_key` и подобные, список — `ModuleBoundaries`) есть только в схеме `identity`; остальные
   модули знают лишь `account_id` (FR-ID-1). Правило читает модели EF Core модулей без базы; то же в реальной
   базе после всех миграций проверяет `MigrationTests` в `Platform.Persistence.IntegrationTests`.

Проверка читает ссылки сборок из метаданных (`System.Reflection.Metadata`, без сторонних библиотек), начиная с
хостов, и загружает сборки модулей, чтобы найти их `DbContext`. Неиспользуемую ссылку на проект компилятор не
пишет в сборку: нарушением считается только настоящее использование. Что правила действительно ловят нарушения,
доказывают модули-образцы в `backend/tests/ArchitectureFixtures`: `FixtureFeed` использует внутренний тип
`FixtureNotes` и занимает его схему — тест ждёт ровно эти два нарушения. Для правила 8 образец —
`LeakyPostsDbContext` в `CredentialBoundaryTests`: почта, хеш пароля, таблица в схеме `identity` и внешний ключ
на неё. Новое правило добавляется в
`ModuleBoundaries` вместе с примером нарушения.

## Инструменты

| Что | Версия | Где задана |
| --- | --- | --- |
| .NET SDK | 10.0.400 и новее в той же линейке | `global.json` |
| Node.js | 22 и новее | `engines` в `package.json` |
| pnpm | 12.21.0 | `packageManager` в `package.json` |

## Линтеры и форматирование

| Часть | Проверка | Настройки |
| --- | --- | --- |
| Все файлы | Кодировка, переводы строк, отступы | `.editorconfig` |
| backend | `dotnet build -warnaserror` с анализаторами (`AnalysisLevel=latest-recommended`, стиль кода при сборке) и `dotnet format --verify-no-changes` | `.editorconfig` (раздел `[*.cs]`), `backend/Directory.Build.props` |
| frontend | ESLint (`@nuxt/eslint`, плоская конфигурация, правило строк интерфейса `i18n/no-raw-text`) и `prettier --check` | `frontend/eslint.config.mjs`, общий `.prettierrc.json` |
| admin | ESLint (`eslint-plugin-vue`, `@vue/eslint-config-typescript`) и `prettier --check` | `admin/eslint.config.mjs`, общий `.prettierrc.json` |

Prettier отвечает за форматирование, ESLint — за ошибки; правила ESLint, спорящие с Prettier, выключены
через `eslint-config-prettier`. Автоисправление: `pnpm run format` в каталоге приложения и `dotnet format` в
`backend/`.

## Что запускает проверка

`scripts/verify.ps1` (его же запускает CI) проверяет:

- документы и ссылки в них, JSON-файлы в корне;
- спецификации экранов (`scripts/ui-specs-test.ps1`): восемь экранов [docs/ui](ui/README.md) описаны для 1440 и
  390 px с состояниями «Пусто», «Загрузка», «Ошибка», ссылаются на существующие разделы FR и токены; якоря
  относительных ссылок в `docs/` ведут на существующие заголовки;
- структуру репозитория: части выше на месте и описаны в этом документе;
- локальное окружение (`scripts/dev-env-test.ps1`): в `compose.yaml` есть все сервисы с healthcheck, порты и
  пароли берутся только из `.env.example`, README даёт три команды запуска; смоук-тест поднимает окружение на
  свободных портах, ждёт healthcheck, достаёт каждый сервис с хоста, проверяет пароли и всё удаляет;
- секреты (`scripts/secret-scan.ps1`): gitleaks по всем файлам, которые git закоммитил бы; сначала он должен
  найти подложенный токен; локально без gitleaks шаг пропускается, в CI обязателен;
- backend: сборку без предупреждений, форматирование, тесты (в том числе границы модулей — раздел выше; API, миграции и очереди на PostgreSQL, Redis и RabbitMQ в Testcontainers);
- frontend и admin: установку по lock-файлу, линтеры, проверку типов, сборку (`pnpm build`), тесты, если
  они есть;
- саму себя (`scripts/verify-selftest.ps1`): упавший шаг роняет проверку, линтеры отвергают плохой код, workflow
  `ci` вызывает этот скрипт и укладывается в 20 минут.

Обвязка шагов (таблица результатов, отчёты о тестах, код выхода) — в `scripts/verify-lib.ps1`. Workflow
`.github/workflows/ci.yml` только готовит машину (.NET по `global.json`, pnpm, Node 22, gitleaks с проверкой контрольной суммы, кэши NuGet, pnpm и браузеров Playwright),
запускает `scripts/verify.ps1` и выкладывает `artifacts/test-results` артефактом `test-results`; своих проверок
в нём нет.
