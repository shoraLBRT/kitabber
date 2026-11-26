# ADR-0005. Очереди, outbox и кэш: MassTransit 8 на RabbitMQ, outbox в схеме модуля, Redis за `IDistributedCache`

- Статус: принято (задача E01-06, #5); владелец может пересмотреть в PR
- Задачи: E01-06; опирается на [ADR-0001](0001-stack.md) («RabbitMQ (MassTransit, outbox)») и
  [ADR-0004](0004-database-migrations.md) (схема на модуль)

## Контекст

ADR-0001 выбрал RabbitMQ, MassTransit и outbox, но не решил, какую версию MassTransit брать, где лежат таблицы
outbox при схеме на модуль, как тесты обходятся без брокера и как подключается Redis. С версии 9 MassTransit
распространяется по коммерческой лицензии (massient.com/license); версия 8 — Apache-2.0.

## Решение

1. **MassTransit 8.x (Apache-2.0), не 9.** Версии закреплены в `backend/Directory.Packages.props`
   (`MassTransit`, `MassTransit.RabbitMQ`, `MassTransit.EntityFrameworkCore` 8.5.x); Renovate не предлагает 9+
   (`allowedVersions: "<9"` в `renovate.json`). Переход на 9 — это платная лицензия, решает владелец.
2. **Шина подключается одной строкой** — `AddPlatformMessaging(configuration, bus => …)` из `Platform.Messaging`.
   Транспорт — `Messaging:Transport`: `RabbitMq` (по умолчанию, `ConnectionStrings:RabbitMq` вида
   `amqp://user:password@host:port/vhost`) или `InMemory` — фейк брокера для тестов. Очереди называются по
   потребителю в kebab-case и долговечны (durable): пока воркер остановлен, сообщения ждут в очереди. Упавшее
   сообщение повторяется три раза (100 мс, 0,5 с, 1 с), потом уходит в очередь `<имя>_error`. Хост стартует, только
   когда шина подключена и очереди привязаны (`WaitUntilStarted`), иначе через минуту падает.
3. **Outbox — в схеме модуля.** Контекст модуля вызывает `modelBuilder.AddOutboxTables()`: таблицы
   `inbox_state`, `outbox_message`, `outbox_state` попадают в схему модуля и в его миграции (ADR-0004). Модуль
   регистрирует outbox строкой `bus.AddModuleOutbox<TContext>()` и публикует через `IModuleOutbox<TContext>`: событие
   пишется в эту таблицу и уходит в брокер только после `SaveChanges` вместе с изменениями модуля; без
   `SaveChanges` событие пропадает вместе с транзакцией.
4. **Ровно один раз — за счёт inbox.** Брокер доставляет «хотя бы раз», поэтому потребитель модуля объявляется
   через `ModuleConsumerDefinition<TConsumer, TContext>`: идентификатор сообщения записывается в `inbox_state`
   модуля в той же транзакции, что и изменения потребителя, и повторная доставка не обрабатывается.
   Потребитель сам `SaveChanges` не вызывает.
5. **Хост воркеров** — `Platform.Workers` (`WorkerHost`): шина, потребители и outbox модулей, кэш. Задача модуля
   добавляет в `WorkerHost.AddModules` свои потребители и `AddModuleOutbox`. API подключит шину, когда первый
   модуль начнёт публиковать события.
6. **Кэш — `IDistributedCache`** (`AddPlatformCache` из `Platform.Caching`): `Cache:Provider` = `Redis`
   (по умолчанию, `ConnectionStrings:Redis` в формате StackExchange.Redis, ключи с префиксом `platform:`) или
   `InMemory` — фейк для тестов. Модули знают только интерфейс. Буфер записи для пакетного сброса в базу —
   `IWriteBuffer` ([ADR-0083](0083-redis-counters-with-batched-flush.md)). Атомарные счётчики с истечением — `IRateCounter`
   с тем же провайдером; на них стоят лимиты частоты ([ADR-0016](0016-rate-limiting.md)).
7. **Несколько публикующих модулей в одном хосте (#281)** — у каждого свой публикатор, а не своя шина.
   `AddModuleOutbox<TContext>()` регистрирует для контекста службу доставки и scoped-публикатор
   `IModuleOutbox<TContext>` (`IPublishEndpoint` и `ISendEndpointProvider`) поверх
   `EntityFrameworkScopedBusContextProvider<IBus, TContext>` из MassTransit: что модуль публикует или отправляет через
   него, попадает в outbox его схемы, в том числе из потребителя. Код модуля публикует только через свой
   `IModuleOutbox<TContext>` и шлёт почту через `IMailer<TContext>` поверх него (#393): письмо лежит в outbox схемы
   модуля и уходит после его коммита, каким бы по счёту модуль ни был в хосте. Обычные `IPublishEndpoint` области
   (scope) и `IMailer` — для служб вне модулей — остаются outbox'ом **первого** модуля, зарегистрированного в хосте
   (в хосте без модулей — шиной); следующие вызовы его не меняют. Архитектурный тест `ModuleOutboxTests` не даёт коду
   модуля брать обычные `IPublishEndpoint`, `ISendEndpointProvider` и `IMailer`.
   Повторная регистрация того же контекста — ошибка на старте.
   У службы доставки и публикатора каждого контекста свой `IBusOutboxNotification`: общий из MassTransit рассчитан на
   одного ожидающего, и две службы доставки мешают друг другу. Outbox и inbox модуля настраиваются через
   `UseModulePostgres()` — PostgreSQL без кэша имён таблиц: MassTransit кэширует их статически по типу сущности
   (`OutboxState`, `InboxState`), и служба доставки второго модуля читала `outbox_state` из схемы первого — его
   события так и оставались в outbox.

## Почему так

- MassTransit уже выбран в ADR-0001 и закрывает outbox, inbox, повторы и тестовый транспорт готовым кодом; ветка 8
  бесплатна. Альтернативы при отказе от MassTransit — Wolverine (MIT, свой outbox на PostgreSQL) или голый
  `RabbitMQ.Client` с собственным outbox; обе дороже сейчас, чем закреплённая 8.x.
- Outbox в схеме модуля продолжает правило ADR-0004: модуль не пишет в чужую схему даже служебными таблицами, а
  событие и изменения сохраняются одной транзакцией одного `DbContext`.
- Явный выбор транспорта и провайдера кэша вместо тихого «нет строки подключения — работаем в памяти»: на стенде
  забытая настройка роняет старт, а не теряет сообщения.

## Последствия

- Обновления безопасности MassTransit 8 придётся отслеживать отдельно: поддержка ветки 8 у авторов ограничена.
  Если её прекратят, нужен выбор владельца: лицензия 9, Wolverine или свой outbox.
- Отдельная шина на модуль (`AddMassTransit<IModuleBus>`) отвергнута: на каждый модуль — своё подключение к брокеру и
  своя настройка эндпоинтов, а `IPublishEndpoint` модуля пришлось бы брать через `Bind<IModuleBus, …>`. Свой
  публикатор поверх готового провайдера MassTransit даёт то же разделение одной шиной.
- Почта и события модуля не зависят от порядка регистрации модулей в хосте: Identity публикует события аккаунта и
  письма через `IModuleOutbox<IdentityDbContext>` и `IMailer<IdentityDbContext>` (#393). Обычные `IPublishEndpoint`
  и `IMailer` остаются для служб вне модулей; в хосте с модулями они пишут в outbox первого из них.
- Проверяется автотестом `ModuleMailTests`: письмо второго модуля хоста лежит в outbox его схемы, уходит после его
  коммита и пропадает с его откатом, даже когда первый модуль сохраняется.
- Проверяется автотестом `MultiModuleOutboxTests`: два модуля в одном хосте публикуют в одной области, каждое
  событие лежит в outbox своей схемы и уходит после коммита своего модуля; откат одного не теряет и не выпускает
  событие другого.
- Проверяется автотестами `backend/tests/Platform.Messaging.IntegrationTests` (PostgreSQL, RabbitMQ и Redis в
  Testcontainers; тестовый транспорт — без Docker).
