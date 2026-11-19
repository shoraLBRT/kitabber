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
  src/Http/                Platform.Http — соглашения REST: /api/v1, ProblemDetails, курсорная пагинация (ADR-0006)
  src/Workers/             Platform.Workers — хост фоновых воркеров: шина, потребители и outbox модулей (ADR-0005)
  src/Messaging/           Platform.Messaging — шина MassTransit на RabbitMQ, outbox и inbox модуля (ADR-0005)
  src/Caching/             Platform.Caching — кэш IDistributedCache на Redis (ADR-0005)
  src/Persistence/         Platform.Persistence — база модулей: ModuleDbContext, мигратор (ADR-0004)
  src/Migrator/            Platform.Migrator — команда миграций: migrate, status, rollback <схема>
  src/Seeding/             Platform.Seeding — демо-данные: зерно, фабрики объектов, демо-сидеры модулей (ADR-0009)
  src/Modules/             модули из ADR-0001 (появляются с задачами модулей)
  tests/Platform.Testing/  общий каркас тестов: контейнеры, фабрики API, Respawn, часы (ADR-0008)
  tests/                   тестовые проекты: *.Tests без Docker, *.IntegrationTests на Testcontainers
  tools/Seed/              Platform.Seed — команда демо-данных: dotnet run --project backend/tools/Seed (ADR-0009)
frontend/                  веб-клиент на Nuxt 3 (Vue 3, TypeScript)
admin/                     админ-панель на Vue 3 + Vite (TypeScript)
brand/                     временный бренд: название, знак, палитра, набор иконок — brand.json (ADR-0011);
                           дизайн-токены tokens.json → tokens.css и правила линтера стилей (ADR-0012)
tools/                     инструменты не на .NET: генерация клиента API, фейковые сервисы (появится в своих задачах)
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

Базовые компоненты интерфейса сайта (ADR-0013) лежат в `frontend/components/ui/Ui*.vue`: кнопка, поле, меню,
вкладки, модальное окно (на телефоне — шторка), шторка, тосты (`useToast()` и `UiToaster` в `app.vue`) и аватар;
помощники фокуса — `frontend/utils/focus.ts`. Витрина — страница `/ui`. Стили — только из токенов (ADR-0012).

Каркас сайта (E02-05) — `frontend/components/shell/Shell*.vue`: `ShellLayout` собирает шапку, левое меню, центральную
колонку (`<main id="content">`) и правую колонку виджетов и переключает раскладки по точкам перелома из токенов.
Его подключает раскладка Nuxt `frontend/layouts/default.vue`; витрины дизайна идут в раскладке `bare`. Пункты меню и
их адреса — один список `frontend/utils/navigation.ts`, общий для десктопа и телефона; выбор темы —
`frontend/composables/useTheme.ts`. Каркас телефона (#16) встаёт в слот `mobile` у `ShellLayout`.

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
Настройки: `Messaging:Transport` (`RabbitMq` | `InMemory`), `ConnectionStrings:RabbitMq`
(`amqp://user:password@host:port/`), `Cache:Provider` (`Redis` | `InMemory`), `ConnectionStrings:Redis`;
для локального окружения они заданы в `backend/src/Workers/appsettings.Development.json`. Запуск:
`dotnet run --project backend/src/Workers`. Правила — [ADR-0005](adr/0005-messaging-and-cache.md).

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
| frontend | ESLint (`@nuxt/eslint`, плоская конфигурация) и `prettier --check` | `frontend/eslint.config.mjs`, общий `.prettierrc.json` |
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
- backend: сборку без предупреждений, форматирование, тесты (в том числе API, миграции и очереди на PostgreSQL, Redis и RabbitMQ в Testcontainers);
- frontend и admin: установку по lock-файлу, линтеры, проверку типов, сборку (`pnpm build`), тесты, если
  они есть;
- саму себя (`scripts/verify-selftest.ps1`): упавший шаг роняет проверку, линтеры отвергают плохой код, workflow
  `ci` вызывает этот скрипт и укладывается в 10 минут.

Обвязка шагов (таблица результатов, отчёты о тестах, код выхода) — в `scripts/verify-lib.ps1`. Workflow
`.github/workflows/ci.yml` только готовит машину (.NET по `global.json`, pnpm, Node 22, gitleaks с проверкой контрольной суммы, кэши NuGet и pnpm),
запускает `scripts/verify.ps1` и выкладывает `artifacts/test-results` артефактом `test-results`; своих проверок
в нём нет.
