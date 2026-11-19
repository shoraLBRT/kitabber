# ADR-0008. Тестовый каркас бэкенда: общая библиотека, контейнер на сборку, Respawn, управляемые часы, VSTest

- Статус: принято (задача E01-15, #8); владелец может пересмотреть в PR
- Задачи: E01-15; дальше на нём строятся E01-17 (#9, сидер и фабрики тестовых объектов) и тесты всех модулей;
  опирается на [ADR-0001](0001-stack.md), [ADR-0004](0004-database-migrations.md),
  [ADR-0005](0005-messaging-and-cache.md), [ADR-0006](0006-rest-api-conventions.md) и
  [TESTING.md](../TESTING.md)

## Контекст

До этой задачи каждый тестовый проект держал свои фикстуры Testcontainers (`PostgresFixture` в двух вариантах,
RabbitMQ, Redis) и свою фабрику API. Модулей скоро будет больше десятка; нужен один каркас: как поднять API на
настоящих PostgreSQL и Redis, как очищать базу между тестами, как управлять временем — и при этом уложить CI в
10 минут. Отдельно был отложен вопрос, переходить ли с VSTest на Microsoft.Testing.Platform (MTP).

## Решение

1. **Общая библиотека `backend/tests/Platform.Testing`** (не тестовый проект). Её подключает каждый тестовый
   проект бэкенда:
   - `Containers/` — `PostgresFixture`, `RedisFixture`, `RabbitMqFixture` на базе `ContainerFixture<T>`; образ
     берётся из `compose.yaml`, так что тесты и локальное окружение работают на одной версии;
   - `Api/PlatformApiFactory` — хост API в процессе (`WebApplicationFactory<Program>`) в окружении `Test`,
     настройки через `WithSetting`, модули и заглушки через `WithModule`, без Docker;
   - `Api/IntegrationApiFactory` — то же на PostgreSQL и Redis из Testcontainers: своя база на тестовый класс
     (миграции всех зарегистрированных модулей накатываются один раз, `ModuleMigrator`), своя нумерованная база
     Redis, `ResetAsync()` перед каждым тестом;
   - `Api/TestAuthentication` — тестовая схема входа (заголовки `X-Test-Account`, `X-Test-Role`) вместо OIDC;
   - `Database/DatabaseReset` — очистка таблиц модулей через Respawn;
   - `Time/TestClock` — управляемые часы.
2. **Контейнер — один на тестовую сборку, база — одна на тестовый класс.** Проект объявляет нужные контейнеры
   атрибутом `[assembly: AssemblyFixture(typeof(PostgresFixture))]`; все классы сборки делят их. Тест, которому
   нужна чистая база без API, создаёт её сам (`CreateDatabaseAsync`). Классы идут параллельно, тесты внутри класса —
   последовательно. Testcontainers reuse (контейнер на несколько запусков) не используем: в CI он ничего не даёт,
   а локально оставляет контейнеры.
3. **База сбрасывается Respawn, а не пересозданием.** Перед каждым тестом `ResetAsync()` удаляет строки всех таблиц
   в схемах модулей; схемы и история миграций (`__ef_migrations_history`) остаются, поэтому миграции идут один раз
   на класс. Справочные данные из миграций (`HasData`) Respawn тоже удаляет — таблицы, которые надо сохранить,
   модуль перечисляет в `KeepTables` (`схема.таблица`). Redis очищается тем же вызовом (`FLUSHDB` своей базы).
4. **Время — только через `TimeProvider`.** Хост API регистрирует `TimeProvider.System` (`AddPlatformApi`),
   воркеры — так же (`WorkerHost`). В тестах фабрика подменяет его на `FakeTimeProvider`
   (`Microsoft.Extensions.TimeProvider.Testing`), который стоит на `TestClock.Start` (15 января 2026, 09:00 UTC) и
   движется только по `Advance`/`SetUtcNow`. Часы одни на фабрику, то есть на тестовый класс, и назад не идут:
   тест сравнивает время относительно `Time.GetUtcNow()`.
5. **Остаёмся на VSTest** (`IsTestingPlatformApplication=false`, `xunit.runner.visualstudio`). Почему не MTP:
   - `scripts/verify.ps1` гоняет `dotnet test` по решению с `--logger trx`; в .NET 10 `dotnet test` в режиме MTP
     включается для всего решения сразу через `global.json` (`test.runner`), а TRX там пишет отдельное расширение
     с другими ключами (`--report-trx`), то есть меняются команда verify, разбор отчётов и сводка CI одновременно;
   - xUnit v3 уже даёт нужное (фикстуры сборки, динамический пропуск, `TestContext`) и под VSTest;
   - выигрыш MTP (скорость старта, запуск теста как exe) на нашем объёме не заметен.
   Вернуться к вопросу стоит, когда VSTest перестанет поддерживаться для xUnit v3 или MTP-режим `dotnet test`
   начнёт понимать `--logger trx`.
6. **Место для тестовых данных.** Уточнено в [ADR-0009](0009-demo-data-and-factories.md): фабрики живут в коде
   модуля (их использует и сидер), в `Platform.Testing` — обвязка (`TestData`, `Data`, `SaveAsync`, `SeedDemoAsync`).
   Исходный текст: фабрики тестовых объектов и сидер (E01-17) добавляются в `Platform.Testing` и
   подключаются к хосту через `WithModule`; до них тесты создают данные через API или контекст модуля.

## Последствия

- Новый модуль пишет интеграционный тест API так: класс-фикстура наследует `IntegrationApiFactory` и вызывает
  `WithModule(Модуль.Register)`, тестовый класс вызывает `ResetAsync()` в `InitializeAsync`. Образец —
  `backend/tests/Platform.Api.IntegrationTests` (успешный путь, 401/403, ошибка валидации, кэш в Redis, часы,
  сброс базы).
- Номерных баз Redis 15, поэтому тестовых классов с `IntegrationApiFactory` в одной сборке — не больше 15; при
  нехватке фикстура падает с понятным сообщением, и тогда классы надо укрупнить или разнести по сборкам.
- Без Docker тесты на контейнерах локально пропускаются (`Skipped`), в CI падают — как и раньше.
- Хост API ещё не подключает модули и базу сам: `IntegrationApiFactory` задаёт `ConnectionStrings:Platform`,
  `ConnectionStrings:Redis` и `Cache:Provider=Redis`, а модуль в тесте регистрирует себя. Когда появятся модули
  продукта, хост будет делать то же сам, и тесты не изменятся.
