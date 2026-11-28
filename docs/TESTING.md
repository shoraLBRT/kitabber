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
задача, добавляющая новый вид проверки, подключает его к этому скрипту. Сейчас скрипт проверяет документацию (ссылки и якоря,
спецификации экранов [docs/ui](ui/README.md) — `scripts/ui-specs-test.ps1`), конфигурацию, структуру репозитория, локальное окружение (`compose.yaml`), сборку и линтеры `backend/`,
`frontend/` и `admin/`; тесты подключаются сами, как только появляются тестовые проекты и скрипты `test` / `e2e`.

Смоук-тест окружения (`scripts/dev-env-test.ps1 -Smoke`) поднимает все сервисы в Docker, поэтому на машине без
запущенного Docker шаг помечается `skipped`, а не падает; пропустить его нарочно можно через
`VERIFY_SKIP_DOCKER=1`. В CI (`CI` задана) пропуск запрещён: без Docker шаг падает. Пропущенный шаг виден в
таблице шагов; выдавать его за пройденный нельзя.

То же правило у серверных тестов на Testcontainers (`backend/tests/*.IntegrationTests`): без Docker или с
`VERIFY_SKIP_DOCKER=1` они локально помечаются пропущенными (`Skipped` в отчёте), в CI падают.

Исключение — смоук выкладки (`scripts/deploy-test.ps1 -Smoke`, E01-03): он собирает все образы и выкладывает,
обновляет и откатывает стенд stage на Docker, это минуты, и в 30 минут `ci / verify` он не помещается. Поэтому в CI
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

## Архитектурные тесты

`backend/tests/Platform.Architecture.Tests` проверяет границы модулей (правила — в
[ARCHITECTURE.md](ARCHITECTURE.md#границы-модулей)). Тест без Docker, идёт в общем шаге тестов backend. Каждое
правило проверено на разрешённом и запрещённом примере, а на модулях-образцах из
`backend/tests/ArchitectureFixtures` — на настоящих собранных сборках. Модули-образцы не подключает ни один хост,
и в проверку платформы они не попадают.

## Отчёты о тестах

Тестовые прогоны пишут отчёты в `artifacts/test-results/<часть>/`: `dotnet test` — TRX (это уже настроено в
`scripts/verify.ps1`), Vitest — JUnit XML в `<клиент>/`, Playwright — JUnit XML в `<клиент>-e2e/` (E01-16). В конце проверка печатает
таблицу шагов и сводку по отчётам; в CI та же таблица попадает в сводку запуска (`GITHUB_STEP_SUMMARY`), а
сами отчёты — в артефакт `test-results`. Каталог `artifacts/` в git не попадает.

## Уровни

| Уровень | Чем | Что проверяет | Где лежит |
| --- | --- | --- | --- |
| Модульные | xUnit | Правила предметной области без базы и сети | `backend/tests/*.UnitTests` |
| Интеграционные | xUnit, `WebApplicationFactory`, Testcontainers | API модуля на настоящих PostgreSQL, Redis, RabbitMQ | `backend/tests/*.IntegrationTests` |
| Архитектурные | NetArchTest | Границы модулей, запреты из ADR | `backend/tests/Architecture.Tests` |
| Контракт API | Снимок OpenAPI (ADR-0006) | Любое изменение API видно в диффе снимка `backend/src/Api/openapi/v1.json` | `backend/tests/Platform.Api.Tests` (`OpenApiSnapshotTests`) |
| Компонентные | Vitest, Vue Test Utils | Компоненты и блоки редактора: поведение, клавиатура, ARIA, фокус; у каждого компонента `components/ui/` свой тест и раздел витрины `/ui` (проверяет `tests/ui/showcase.spec.ts`, ADR-0013); каркас сайта — `tests/shell/`: ширины и скрытие колонок проверяются по контракту стилей (happy-dom не раскладывает страницу), настоящую раскладку проверяют сквозные тесты `frontend/e2e/shell.spec.ts` | `frontend/tests/**/*.spec.ts`, `admin/tests/**/*.spec.ts` (`pnpm test`) |
| Стили | stylelint (ADR-0012) | Цвета и размеры в компонентах только из дизайн-токенов | `stylelint.config.mjs` клиентов, правила `brand/stylelint-rules.mjs` (`pnpm run lint`) |
| Сквозные | Playwright | Сценарии пользователя в браузере в профилях `desktop` и `mobile`; раскладка каркаса на 1440, 1100, 800 и 390 px, витрины в обеих темах | `frontend/e2e`, `admin/e2e` (`pnpm e2e`) |

## Десктоп и телефон

Каждый сквозной тест выполняется в двух проектах Playwright:

- `desktop` — 1440×900;
- `mobile` — 390×844, сенсорный ввод.

Тест, который проходит только в одном профиле, считается упавшим. Отличия раскладки проверяются явно
(например, «на mobile видна нижняя панель»).

## Сквозные тесты (Playwright)

- **Где.** `frontend/e2e/*.spec.ts` и `admin/e2e/*.spec.ts`, настройка — `playwright.config.ts` пакета; проекты
  `desktop` и `mobile` описаны выше, браузер — только Chromium (headless shell). Настройку проверяет компонентный
  тест `tests/e2e-config.spec.ts` каждого пакета: оба профиля, ни один проект не сужает набор тестов, трасса и
  снимок при падении, пути отчётов.
- **Против чего.** Продакшен-сборка, которую verify собирает шагом «build»: сайт — `node .output/server/index.mjs`
  на порту 3100, админка — `vite preview` на 4174; сервер поднимает `webServer` Playwright. Локально:
  `pnpm build`, затем `pnpm e2e` (один раз — `pnpm exec playwright install chromium --only-shell`; verify ставит
  браузер сам, а в CI берёт его из кэша). Системные пакеты Chromium в CI ставит не verify, а `ci.yml` через
  `scripts/ci/playwright-deps.sh` (#302): из списка Playwright выбираются пакеты, которых нет в образе раннера, и
  ставятся из кэша .deb-файлов через `dpkg`; к зеркалу Ubuntu шаг идёт, только если кэша нет или он не покрывает
  набор. Ключ кэша — сам набор недостающих пакетов, поэтому смена версии Playwright (Renovate) или образа раннера
  обновляет его сама.
- **Фикстуры.** `frontend/e2e/fixtures.ts`: тест падает на ошибке скрипта или консоли (в том числе на ошибке
  гидратации); ожидаемо отсутствующие файлы перечисляются опцией `missingResources`. Там же помощники:
  `press` (касание в mobile, щелчок в desktop), `expectNoHorizontalScroll`, `makeScrollable`, `waitForHydration`.
- **DOM меняется только после гидратации.** `page.goto` возвращается по событию load, а Nuxt может ещё
  гидратировать страницу; правка DOM в этот момент — несовпадение гидратации, и Vue перерисовывает поддерево,
  выбрасывая правку (#300). Перед `page.evaluate`, меняющим разметку, вызывайте `waitForHydration(page)`
  (`makeScrollable` делает это сам).
- **Ширины между профилями** (например, 1100 и 800 px) тест задаёт сам через `page.setViewportSize` и выполняется
  в обоих профилях. Безопасные зоны «чёлки» эмулируются через DevTools-протокол Chromium
  (`Emulation.setSafeAreaInsetsOverride`).
- **Отчёты.** JUnit — `artifacts/test-results/<пакет>-e2e/junit.xml`; при падении трасса (`trace.zip`, открывается
  `pnpm exec playwright show-trace`) и снимок экрана — в `artifacts/test-results/<пакет>-e2e/failures/`, в CI это
  артефакт `test-results`.
- **Доступность.** Формы и окна проверяет axe (`@axe-core/playwright`, правила WCAG 2.1 A и AA) прямо в сквозном
  тесте: пустыми и с ошибками, в обоих профилях; нарушений должно быть ноль. Пример — `frontend/e2e/auth-window.spec.ts`.
- **Снимков для сравнения нет.** Если они появятся, эталоны снимаются на раннере CI (ubuntu), а не на Windows.

## Внешние сервисы: только фейки

Тесты и локальная разработка никогда не обращаются во внешний мир.

| Сервис | Локально и в тестах |
| --- | --- |
| Почта | Mailpit (`MailpitFixture` в Testcontainers): тест читает письмо через его API (`MailpitClient`); без SMTP — фейк `Mail:Provider=InMemory` (`InMemoryEmailSender`, ADR-0017). Тексты писем проверяются снимками `backend/tests/Platform.Mail.Tests/Snapshots` (`UPDATE_SNAPSHOTS=1`) |
| Хранилище | MinIO (образ `cgr.dev/chainguard/minio`, см. [ARCHITECTURE.md](ARCHITECTURE.md)) |
| Telegram | Фейковый `ITelegramGateway`, проигрывает записанные сценарии из `tools/fakes/telegram` |
| Внешние провайдеры входа | Фейковый провайдер OIDC |
| Капча, проверка изображений, ИИ, озвучка | Фейковые реализации интерфейсов с управляемым ответом |
| Push | Фейковый отправитель, складывает сообщения в память |
| Брокер очередей | RabbitMQ в Testcontainers; без брокера — тестовый транспорт `Messaging:Transport=InMemory` (ADR-0005) |
| Кэш и счётчики лимитов | Redis в Testcontainers; без него — `Cache:Provider=InMemory` (ADR-0005, ADR-0016; `PlatformApiFactory` берёт его по умолчанию, счётчики идут по часам `factory.Time`) |
| Коллектор телеметрии (OTLP) | `TestTelemetry` из `Platform.Testing.Observability`: спаны и записи логов в памяти; `WithModule(telemetry.Register)`. Слушатель спанов общий на процесс — ищите свои спаны по trace id (`TestTelemetry.NewTraceparent()`) (ADR-0015) |

Настоящие ключи нужны только на стендах и задаются переменными окружения (файл `docs/CONFIG.md` появится в
задаче E01-13).

## Детерминизм

- Время — только через `TimeProvider`; в тестах часы управляемые (`TestClock`, `FakeTimeProvider`).
- Данные — фабрики объектов и сидер с фиксированным зерном (`SeedData`, ADR-0009); `Guid.NewGuid()` и
  `Random.Shared` в тестовых данных не используются.
- База сбрасывается между интеграционными тестами (`IntegrationApiFactory.ResetAsync`, Respawn).
- Хост, который тест собирает сам (`WorkerHost.CreateBuilder`), запускается и мигрирует внутри
  `HostStartGate.RunAsync`, как `IntegrationApiFactory`: одновременно стартует не больше половины ядер хостов,
  иначе на CI соединение с базой исчерпывает `Timeout` Npgsql ещё до сокета.
- Тест, который падает случайно, чинится или удаляется в том же PR; «перезапустить и забыть» нельзя.

## Что обязано быть в каждом PR с кодом

- Тесты на каждый критерий приёмки задачи.
- Для API: интеграционный тест на успешный путь, на права доступа и на ошибку валидации.
- Для экрана: сквозной тест в обоих профилях и состояния «пусто», «загрузка», «ошибка».
- Для правил (рейтинг, карма, фильтры, конвертер Telegram): таблица эталонных примеров.

## Базовая линия

Итоговое число тестов записано в [PROJECT_STATE.md](PROJECT_STATE.md), раскладка по файлам — в
[TEST_BASELINE.md](TEST_BASELINE.md); число не должно уменьшаться. Если оно уменьшилось,
потому что проверяемая вещь удалена, это объясняется в PR.
