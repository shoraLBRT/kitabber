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
   регистрирует outbox строкой `bus.AddModuleOutbox<TContext>()`: в пределах запроса `IPublishEndpoint` пишет
   событие в эту таблицу, и оно уходит в брокер только после `SaveChanges` вместе с изменениями модуля; без
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
   `InMemory` — фейк для тестов. Модули знают только интерфейс. Атомарные счётчики с истечением — `IRateCounter`
   с тем же провайдером; на них стоят лимиты частоты ([ADR-0016](0016-rate-limiting.md)).

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
- Bus outbox в хосте — один: `AddModuleOutbox` подменяет `IPublishEndpoint` области (scope) на outbox последнего
  зарегистрированного контекста. Пока публикующий модуль один, этого достаточно; как публиковать из нескольких
  модулей в одном хосте, решает отдельная задача (см. PROJECT_STATE.md).
- Проверяется автотестами `backend/tests/Platform.Messaging.IntegrationTests` (PostgreSQL, RabbitMQ и Redis в
  Testcontainers; тестовый транспорт — без Docker).
