# Тестирование

Главное правило: **критерий приёмки задачи считается выполненным, только если его проверяет автотест.**
Проверки «руками» допустимы лишь там, где задача это прямо оговаривает (например, клавиатура на настоящем
телефоне), и тогда шаги проверки записываются в PR.

## Одна команда

```powershell
pwsh scripts/verify.ps1
```

В Windows без PowerShell 7 та же проверка запускается так:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/verify.ps1
```

Команда запускает всё, что должно быть зелёным перед PR, и то же самое запускает CI (`ci / verify`). Каждая
задача, добавляющая новый вид проверки, подключает его к этому скрипту. Сейчас скрипт проверяет документацию,
конфигурацию, структуру репозитория, локальное окружение (`compose.yaml`), сборку и линтеры `backend/`,
`frontend/` и `admin/`; тесты подключаются сами, как только появляются тестовые проекты и скрипты `test` / `e2e`.

Смоук-тест окружения (`scripts/dev-env-test.ps1 -Smoke`) поднимает все сервисы в Docker, поэтому на машине без
запущенного Docker шаг помечается `skipped`, а не падает; пропустить его нарочно можно через
`VERIFY_SKIP_DOCKER=1`. В CI (`CI` задана) пропуск запрещён: без Docker шаг падает. Пропущенный шаг виден в
таблице шагов; выдавать его за пройденный нельзя.

То же правило у серверных тестов на Testcontainers (`backend/tests/*.IntegrationTests`): без Docker или с
`VERIFY_SKIP_DOCKER=1` они локально помечаются пропущенными (`Skipped` в отчёте), в CI падают.

Исключение — смоук выкладки (`scripts/deploy-test.ps1 -Smoke`, E01-03): он собирает все образы и выкладывает,
обновляет и откатывает стенд stage на Docker, это минуты, и в 10 минут `ci / verify` он не помещается. Поэтому в CI
он идёт отдельным заданием `images / build` на каждом PR, а в `scripts/verify.ps1` шаг `deploy smoke` пропускается,
пока не задано `VERIFY_IMAGES=1`. Что задание существует и запускает смоук, проверяют шаги `deploy config` и
`verify self-test`. Подробности — [DEPLOY.md](DEPLOY.md#проверка).

## Каркас серверных тестов

Всё общее лежит в библиотеке `backend/tests/Platform.Testing`; её подключает каждый тестовый проект бэкенда.
Правила и причины — [ADR-0008](adr/0008-backend-test-framework.md).

- **Контейнеры** (`Platform.Testing.Containers`): `PostgresFixture`, `RedisFixture`, `RabbitMqFixture`, образы — из
  `compose.yaml`. Один контейнер на тестовую сборку: проект объявляет его атрибутом
  `[assembly: AssemblyFixture(typeof(PostgresFixture))]`, тестовые классы получают фикстуру в конструкторе.
  Отдельный контейнер на класс или на тест не заводите — это минуты в CI.
- **API в процессе без Docker** — `PlatformApiFactory`: окружение `Test`, настройки `WithSetting`, модули и
  заглушки `WithModule`, тестовый вход `TestAuthentication` (`request.As("account-1", "moderator")`).
- **API на настоящих PostgreSQL и Redis** — `IntegrationApiFactory`. Образец —
  `backend/tests/Platform.Api.IntegrationTests` (`NotesApiTests`):

  ```csharp
  public sealed class NotesApi : IntegrationApiFactory
  {
      public NotesApi(PostgresFixture postgres, RedisFixture redis)
          : base(postgres, redis) => WithModule(NotesModule.Register);
  }

  public sealed class NotesApiTests(NotesApi api) : IClassFixture<NotesApi>, IAsyncLifetime
  {
      public async ValueTask InitializeAsync() => await api.ResetAsync(); // затем api.CreateClient()
  }
  ```

  Фабрика — одна на тестовый класс: своя база (миграции модулей накатываются один раз) и своя номерная база Redis.
  `ResetAsync()` перед каждым тестом очищает таблицы модулей (Respawn; история миграций остаётся) и Redis.
  Справочные таблицы из миграций, которые должны пережить сброс, перечисляются в `KeepTables`. Клиент создаётся
  после первого `ResetAsync()`: до него хост ещё не знает строк подключения.
- **База без API** — `PostgresFixture.CreateDatabaseAsync()` (новая пустая база) и `DatabaseReset` для очистки.
- **Время** — `TestClock`: фабрика подменяет `TimeProvider` на `FakeTimeProvider` (`api.Time`), он стоит на
  `TestClock.Start` и движется только по `Advance`/`SetUtcNow`. Часы общие на класс и назад не идут: сравнивайте
  с `api.Time.GetUtcNow()`, а не с константой.
- **Тестовые данные** — фабрики модуля (`ObjectFactory<T>` из `Platform.Seeding`, лежат в коде модуля рядом с
  сущностью; ими же пишет демо-данные сидер). В API-тесте: `new NoteFactory(api.Data).Build(n => n.Title = "…")`
  и `api.SaveAsync<NotesDbContext>(…)`; `api.Data` — фиксированное зерно на часах фабрики, поэтому идентификаторы,
  тексты и время одинаковы на каждом прогоне. Руками задаётся только то, что тест проверяет. Без хоста —
  `TestData.Create()`. Демо-данные модулей целиком — `api.SeedDemoAsync()`. Правила — ADR-0009.
- **Раннер** — xUnit v3 через VSTest (`IsTestingPlatformApplication=false`): так `dotnet test --logger trx` в
  `scripts/verify.ps1` пишет TRX для сводки CI. Почему не Microsoft.Testing.Platform — в ADR-0008.

## Отчёты о тестах

Тестовые прогоны пишут отчёты в `artifacts/test-results/<часть>/`: `dotnet test` — TRX (это уже настроено в
`scripts/verify.ps1`), Vitest и Playwright — JUnit XML (подключает E01-16). В конце проверка печатает
таблицу шагов и сводку по отчётам; в CI та же таблица попадает в сводку запуска (`GITHUB_STEP_SUMMARY`), а
сами отчёты — в артефакт `test-results`. Каталог `artifacts/` в git не попадает.

## Уровни

| Уровень | Чем | Что проверяет | Где лежит |
| --- | --- | --- | --- |
| Модульные | xUnit | Правила предметной области без базы и сети | `backend/tests/*.UnitTests` |
| Интеграционные | xUnit, `WebApplicationFactory`, Testcontainers | API модуля на настоящих PostgreSQL, Redis, RabbitMQ | `backend/tests/*.IntegrationTests` |
| Архитектурные | NetArchTest | Границы модулей, запреты из ADR | `backend/tests/Architecture.Tests` |
| Контракт API | Снимок OpenAPI (ADR-0006) | Любое изменение API видно в диффе снимка `backend/src/Api/openapi/v1.json` | `backend/tests/Platform.Api.Tests` (`OpenApiSnapshotTests`) |
| Компонентные | Vitest, Vue Test Utils | Компоненты и блоки редактора | `frontend/tests/**/*.spec.ts`, `admin/tests/**/*.spec.ts` (`pnpm test`) |
| Сквозные | Playwright | Сценарии пользователя в браузере | `frontend/e2e` |

## Десктоп и телефон

Каждый сквозной тест выполняется в двух проектах Playwright:

- `desktop` — 1440×900;
- `mobile` — 390×844, сенсорный ввод.

Тест, который проходит только в одном профиле, считается упавшим. Отличия раскладки проверяются явно
(например, «на mobile видна нижняя панель»).

## Внешние сервисы: только фейки

Тесты и локальная разработка никогда не обращаются во внешний мир.

| Сервис | Локально и в тестах |
| --- | --- |
| Почта | Mailpit: тест читает письмо через его API |
| Хранилище | MinIO (образ `cgr.dev/chainguard/minio`, см. [ARCHITECTURE.md](ARCHITECTURE.md)) |
| Telegram | Фейковый `ITelegramGateway`, проигрывает записанные сценарии из `tools/fakes/telegram` |
| Внешние провайдеры входа | Фейковый провайдер OIDC |
| Капча, проверка изображений, ИИ, озвучка | Фейковые реализации интерфейсов с управляемым ответом |
| Push | Фейковый отправитель, складывает сообщения в память |
| Брокер очередей | RabbitMQ в Testcontainers; без брокера — тестовый транспорт `Messaging:Transport=InMemory` (ADR-0005) |
| Кэш | Redis в Testcontainers; без него — `Cache:Provider=InMemory` (ADR-0005) |

Настоящие ключи нужны только на стендах и задаются переменными окружения (файл `docs/CONFIG.md` появится в
задаче E01-13).

## Детерминизм

- Время — только через `TimeProvider`; в тестах часы управляемые (`TestClock`, `FakeTimeProvider`).
- Данные — фабрики объектов и сидер с фиксированным зерном (`SeedData`, ADR-0009); `Guid.NewGuid()` и
  `Random.Shared` в тестовых данных не используются.
- База сбрасывается между интеграционными тестами (`IntegrationApiFactory.ResetAsync`, Respawn).
- Тест, который падает случайно, чинится или удаляется в том же PR; «перезапустить и забыть» нельзя.

## Что обязано быть в каждом PR с кодом

- Тесты на каждый критерий приёмки задачи.
- Для API: интеграционный тест на успешный путь, на права доступа и на ошибку валидации.
- Для экрана: сквозной тест в обоих профилях и состояния «пусто», «загрузка», «ошибка».
- Для правил (рейтинг, карма, фильтры, конвертер Telegram): таблица эталонных примеров.

## Базовая линия

Число тестов записано в [PROJECT_STATE.md](PROJECT_STATE.md) и не должно уменьшаться. Если оно уменьшилось,
потому что проверяемая вещь удалена, это объясняется в PR.
