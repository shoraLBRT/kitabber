# Настройки и секреты

Все настройки серверной части — в одном месте. Правила — [ADR-0007](adr/0007-configuration-and-secrets.md).
Тест `SettingsCatalogTests` проверяет, что каждая объявленная в коде настройка описана здесь (ключом и именем
переменной окружения); новая настройка без строки в этом документе роняет `scripts/verify.ps1`.

## Как задаются настройки

Ключ настройки — путь через двоеточие (`ConnectionStrings:Redis`). Переменная окружения — тот же путь через
двойное подчёркивание (`ConnectionStrings__Redis`). Источники, от слабого к сильному (следующий перекрывает
предыдущий):

1. `appsettings.json` хоста — общее для всех окружений, без секретов;
2. `appsettings.{Окружение}.json` — слой окружения;
3. user secrets (`dotnet user-secrets`) — только в Development;
4. переменные окружения;
5. аргументы командной строки (`--Ключ значение` или `--Ключ=значение`);
6. файлы секретов из каталога `Secrets:Directory`: один файл на настройку, имя файла — имя переменной
   окружения, содержимое — значение (так монтируют секреты Docker и Kubernetes).

Хост проверяет настройки до запуска. Если обязательной настройки нет, значение не из списка допустимых или
окружение неизвестно, хост не стартует и печатает все проблемы сразу, называя ключ и переменную окружения.
Значения секретов в сообщениях не выводятся.

## Окружения

Окружение задаёт `ASPNETCORE_ENVIRONMENT` для API и `DOTNET_ENVIRONMENT` для воркеров и мигратора (или аргумент
`--environment`). Без него .NET выбирает `Production`. Другие имена хост отвергает.

| Окружение | Где | Секреты в `appsettings.{Окружение}.json` |
| --- | --- | --- |
| `Development` | машина разработчика с `compose.yaml` (`launchSettings.json` ставит его сам) | можно: только локальные пароли из `.env.example` |
| `Test` | автотесты: фейки, транспорт и кэш в памяти | можно: только тестовые значения |
| `Staging` | стенд stage (E01-03) | нельзя: хост не стартует, если секрет пришёл из файла настроек |
| `Production` | боевой стенд | нельзя, так же |

## Где берутся секреты на стендах

Секрет — пароль, ключ или строка подключения с паролем (в таблицах ниже — «да» в колонке «Секрет»). В git
секретов нет: `appsettings.json`, `appsettings.Staging.json` и `appsettings.Production.json` их не содержат (это
проверяет тест), а сканер gitleaks в `scripts/verify.ps1` ищет утёкшие ключи во всех файлах репозитория.

На стенде секреты приходят переменными окружения или файлами в каталоге `Secrets:Directory`. Код не зависит от
того, кто их туда кладёт. Выкладка E01-03 пишет их в env-файл стенда из секретов окружения GitHub Actions (список
и как подключить — [DEPLOY.md](DEPLOY.md#что-нужно-от-владельца)); другие варианты хранилища — в ADR-0007.

## Хост (все процессы)

| Ключ | Переменная окружения | Секрет | Обязательна | По умолчанию | Что это |
| --- | --- | --- | --- | --- | --- |
| `Secrets:Directory` | `Secrets__Directory` | нет | нет | — | каталог файлов секретов; каталог, которого нет, — ошибка настройки |

## Наблюдаемость (API и воркеры)

Логи, метрики и трассировка — [ADR-0015](adr/0015-observability.md). Переменные `OTEL_*` — стандартные переменные
OpenTelemetry: SDK читает и остальные (`OTEL_EXPORTER_OTLP_PROTOCOL`, `OTEL_RESOURCE_ATTRIBUTES` и другие).

| Ключ | Переменная окружения | Секрет | Обязательна | По умолчанию | Что это |
| --- | --- | --- | --- | --- | --- |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `OTEL_EXPORTER_OTLP_ENDPOINT` | нет | нет | — | коллектор OpenTelemetry (OTLP/gRPC), например `http://otel-collector:4317`: туда уходят трассы, метрики и логи; без адреса из процесса ничего не отправляется |
| `OTEL_EXPORTER_OTLP_HEADERS` | `OTEL_EXPORTER_OTLP_HEADERS` | да | нет | — | заголовки для коллектора: `key=value,key2=value2`, например ключ облачного хранилища трасс |
| `Logging:Console:FormatterName` | `Logging__Console__FormatterName` | нет | нет | `json` (`simple` в Development) | формат логов в консоли: `json` — один JSON-объект на строку с `TraceId` и `SpanId` в `Scopes`; `simple` — для разработчика |

## API (`backend/src/Api`)

Плюс настройки наблюдаемости (выше). Модули добавляют настройки в `ApiSettings` вместе с регистрацией и описывают
здесь.

| Ключ | Переменная окружения | Секрет | Обязательна | По умолчанию | Что это |
| --- | --- | --- | --- | --- | --- |
| `Cache:Provider` | `Cache__Provider` | нет | нет | `Redis` | хранилище кэша, счётчиков лимитов частоты, билетов хаба и backplane реального времени (ADR-0019): `Redis` или `InMemory` (для тестов; не делится между экземплярами) |
| `ConnectionStrings:Redis` | `ConnectionStrings__Redis` | да | при кэше `Redis` | — | Redis в формате StackExchange.Redis: `host:port,password=...` |
| `Messaging:Transport` | `Messaging__Transport` | нет | нет | `RabbitMq` | транспорт шины: `RabbitMq` или `InMemory` (для тестов); API отправляет через шину письма из outbox модуля Identity (ADR-0020) |
| `ConnectionStrings:RabbitMq` | `ConnectionStrings__RabbitMq` | да | при транспорте `RabbitMq` | — | брокер: `amqp://user:password@host:port/vhost` |
| `RateLimiting:Enabled` | `RateLimiting__Enabled` | нет | нет | `true` | лимиты частоты запросов к API (ADR-0016); `false` выключает их |
| `RateLimiting:Policies:Default:PermitLimit` | `RateLimiting__Policies__Default__PermitLimit` | нет | нет | `300` | политика `Default` — все запросы под `/api`: сколько запросов разрешено за окно |
| `RateLimiting:Policies:Default:WindowSeconds` | `RateLimiting__Policies__Default__WindowSeconds` | нет | нет | `60` | длина окна политики `Default` в секундах |
| `RateLimiting:Policies:Default:Partition` | `RateLimiting__Policies__Default__Partition` | нет | нет | `Account` | кого считает один счётчик `Default`: `Account` (аккаунт; анонима — по IP) или `Ip` |
| `RateLimiting:Policies:Writes:PermitLimit` | `RateLimiting__Policies__Writes__PermitLimit` | нет | нет | `60` | политика `Writes` — все `POST`, `PUT`, `PATCH`, `DELETE` под `/api`: сколько запросов за окно |
| `RateLimiting:Policies:Writes:WindowSeconds` | `RateLimiting__Policies__Writes__WindowSeconds` | нет | нет | `60` | длина окна политики `Writes` в секундах |
| `RateLimiting:Policies:Writes:Partition` | `RateLimiting__Policies__Writes__Partition` | нет | нет | `Account` | кого считает один счётчик `Writes`: `Account` или `Ip` |
| `RateLimiting:Policies:identity.email-codes:PermitLimit` | `RateLimiting__Policies__identity.email-codes__PermitLimit` | нет | нет | `5` | политика `identity.email-codes` — письма с кодом подтверждения почты (`PUT /api/v1/account/email`, ADR-0020): сколько за окно |
| `RateLimiting:Policies:identity.email-codes:WindowSeconds` | `RateLimiting__Policies__identity.email-codes__WindowSeconds` | нет | нет | `3600` | длина окна политики `identity.email-codes` в секундах |
| `RateLimiting:Policies:identity.email-codes:Partition` | `RateLimiting__Policies__identity.email-codes__Partition` | нет | нет | `Account` | кого считает один счётчик `identity.email-codes`: `Account` или `Ip` |
| `RateLimiting:Policies:identity.registrations:PermitLimit` | `RateLimiting__Policies__identity.registrations__PermitLimit` | нет | нет | `10` | политика `identity.registrations` — регистрации и повторные коды (`POST /api/v1/registration`, `POST /api/v1/registration/{id}/code`, E03-01): сколько за окно |
| `RateLimiting:Policies:identity.registrations:WindowSeconds` | `RateLimiting__Policies__identity.registrations__WindowSeconds` | нет | нет | `3600` | длина окна политики `identity.registrations` в секундах |
| `RateLimiting:Policies:identity.registrations:Partition` | `RateLimiting__Policies__identity.registrations__Partition` | нет | нет | `Ip` | кого считает один счётчик `identity.registrations`: `Account` или `Ip` |
| `RateLimiting:Policies:identity.sign-in:PermitLimit` | `RateLimiting__Policies__identity.sign-in__PermitLimit` | нет | нет | `30` | политика `identity.sign-in` — попытки входа по паролю и ввод кода входа (`POST /api/v1/sign-in/password`, `POST /api/v1/sign-in/code/{id}/confirmation`, E03-02): сколько за окно |
| `RateLimiting:Policies:identity.sign-in:WindowSeconds` | `RateLimiting__Policies__identity.sign-in__WindowSeconds` | нет | нет | `900` | длина окна политики `identity.sign-in` в секундах |
| `RateLimiting:Policies:identity.sign-in:Partition` | `RateLimiting__Policies__identity.sign-in__Partition` | нет | нет | `Ip` | кого считает один счётчик `identity.sign-in`: `Account` или `Ip` |
| `RateLimiting:Policies:identity.sign-in.address:PermitLimit` | `RateLimiting__Policies__identity.sign-in.address__PermitLimit` | нет | нет | `10` | политика `identity.sign-in.address` — попытки входа по паролю на один адрес почты из любых сетей (E03-02): сколько за окно |
| `RateLimiting:Policies:identity.sign-in.address:WindowSeconds` | `RateLimiting__Policies__identity.sign-in.address__WindowSeconds` | нет | нет | `900` | длина окна политики `identity.sign-in.address` в секундах |
| `RateLimiting:Policies:identity.sign-in.address:Partition` | `RateLimiting__Policies__identity.sign-in.address__Partition` | нет | нет | `Ip` | не действует: счётчик этой политики — адрес почты (хеш), его выбирает эндпоинт |
| `RateLimiting:Policies:identity.sign-in-codes:PermitLimit` | `RateLimiting__Policies__identity.sign-in-codes__PermitLimit` | нет | нет | `10` | политика `identity.sign-in-codes` — запросы кода входа (`POST /api/v1/sign-in/code`, E03-02): сколько за окно |
| `RateLimiting:Policies:identity.sign-in-codes:WindowSeconds` | `RateLimiting__Policies__identity.sign-in-codes__WindowSeconds` | нет | нет | `3600` | длина окна политики `identity.sign-in-codes` в секундах |
| `RateLimiting:Policies:identity.sign-in-codes:Partition` | `RateLimiting__Policies__identity.sign-in-codes__Partition` | нет | нет | `Ip` | кого считает один счётчик `identity.sign-in-codes`: `Account` или `Ip` |
| `RateLimiting:Policies:identity.sign-in-codes.address:PermitLimit` | `RateLimiting__Policies__identity.sign-in-codes.address__PermitLimit` | нет | нет | `5` | политика `identity.sign-in-codes.address` — письма с кодом входа на один адрес почты, сверх таймера повторной отправки (E03-02): сколько за окно |
| `RateLimiting:Policies:identity.sign-in-codes.address:WindowSeconds` | `RateLimiting__Policies__identity.sign-in-codes.address__WindowSeconds` | нет | нет | `3600` | длина окна политики `identity.sign-in-codes.address` в секундах |
| `RateLimiting:Policies:identity.sign-in-codes.address:Partition` | `RateLimiting__Policies__identity.sign-in-codes.address__Partition` | нет | нет | `Ip` | не действует: счётчик этой политики — адрес почты (хеш), его выбирает эндпоинт |
| `Http:TrustedProxies` | `Http__TrustedProxies` | нет | нет | — | сети обратных прокси, чьему `X-Forwarded-For` верим, через запятую в CIDR: `10.0.0.0/8,172.16.0.0/12`; без настройки доверяем только прокси на той же машине (loopback) |

Политику модуля задают в коде (`AddRateLimitPolicy`), её ключи — `RateLimiting:Policies:<имя>:...` — модуль
добавляет в `ApiSettings` и в эту таблицу. Политика в настройках, которой нет в коде, или неверное значение
останавливают старт.

Служебные адреса (вне `/api/v1` и вне OpenAPI): `/health/live` — процесс отвечает, ничего не проверяет;
`/health/ready` — проверки с тегом `ready` (база модулей, кэш, шина), 503 при сбое; `/metrics` — метрики в
текстовом формате Prometheus. Прокси стенда отдаёт наружу `/health/*`, но не `/metrics`.

### Вход: модуль Identity в API

Identity — провайдер OpenID Connect ([ADR-0002](adr/0002-identity.md), [ADR-0018](adr/0018-openid-connect-provider.md)).
Адреса провайдера: `/.well-known/openid-configuration`, `/.well-known/jwks`, `/connect/authorize`, `/connect/token`
(вне `/api/v1` и вне OpenAPI). API принимает токены доступа провайдера из `Auth:Authority` — по умолчанию своего.

| Ключ | Переменная окружения | Секрет | Обязательна | По умолчанию | Что это |
| --- | --- | --- | --- | --- | --- |
| `ConnectionStrings:Platform` | `ConnectionStrings__Platform` | да | да | — | PostgreSQL платформы, как у мигратора: аккаунты, клиенты, коды и токены в схеме `identity`, блоги в схеме `blogs` |
| `Identity:Issuer` | `Identity__Issuer` | нет | нет | адрес запроса | публичный адрес провайдера, `iss` всех токенов, например `https://example.org/`; на стенде — адрес сайта |
| `Identity:SigningKey` | `Identity__SigningKey` | да | вне Development и Test | ключ создаётся при старте | закрытый ключ RSA (от 2048 бит), которым подписываются токены: PEM или base64 от DER (PKCS#8 или PKCS#1) |
| `Identity:EncryptionKey` | `Identity__EncryptionKey` | да | вне Development и Test | ключ создаётся при старте | base64 от 32 случайных байт (AES-256): шифрует коды авторизации |
| `Identity:SignInPath` | `Identity__SignInPath` | нет | нет | `/login` | окно входа, куда провайдер отправляет человека без сессии; после входа он возвращается к `/connect/authorize` |
| `Identity:Clients:Web:RedirectUris` | `Identity__Clients__Web__RedirectUris` | нет | нет | — | адреса возврата сайта (Nuxt) через запятую, точное совпадение: `https://example.org/auth/callback`; без них клиент `web` не регистрируется |
| `Identity:Clients:Admin:RedirectUris` | `Identity__Clients__Admin__RedirectUris` | нет | нет | — | адреса возврата админ-панели через запятую; без них клиент `admin` не регистрируется |
| `Identity:SyncClients` | `Identity__SyncClients` | нет | нет | `true` | `false` — API не записывает клиентов в базу при старте (сборка OpenAPI и тесты без базы) |
| `Auth:Authority` | `Auth__Authority` | нет | нет | — (свой провайдер) | чьи токены доступа принимает API; внешний адрес (`https://accounts.example.org/`) переключает API на другой провайдер OpenID Connect — ключи берутся из его `/.well-known/openid-configuration` |
| `Auth:Audience` | `Auth__Audience` | нет | нет | `platform-api` | аудитория (`aud`), которую должен нести токен доступа |

Ключи провайдера. В Development и Test их нет в репозитории: при старте создаются новые, и токены живут до
перезапуска. На стенде без обоих ключей API не стартует: созданные ключи различались бы между экземплярами и
менялись бы при каждом перезапуске. Как получить:

```bash
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -outform DER | base64 -w0   # Identity__SigningKey
openssl rand -base64 32                                                                    # Identity__EncryptionKey
```

Смена ключа подписи выводит из строя выданные токены доступа (живут 15 минут); ротация без перерыва — отдельная
задача.

## Воркеры (`backend/src/Workers`)

Плюс настройки наблюдаемости (выше).

| Ключ | Переменная окружения | Секрет | Обязательна | По умолчанию | Что это |
| --- | --- | --- | --- | --- | --- |
| `ConnectionStrings:Platform` | `ConnectionStrings__Platform` | да | да | — | PostgreSQL платформы, как у мигратора: блоги и inbox потребителей модуля Blogs в схеме `blogs` ([ADR-0025](adr/0025-blogs-subsite-and-profile.md)) |
| `Messaging:Transport` | `Messaging__Transport` | нет | нет | `RabbitMq` | транспорт шины: `RabbitMq` или `InMemory` (для тестов) |
| `ConnectionStrings:RabbitMq` | `ConnectionStrings__RabbitMq` | да | при транспорте `RabbitMq` | — | брокер: `amqp://user:password@host:port/vhost` |
| `Cache:Provider` | `Cache__Provider` | нет | нет | `Redis` | хранилище кэша: `Redis` или `InMemory` (для тестов) |
| `ConnectionStrings:Redis` | `ConnectionStrings__Redis` | да | при кэше `Redis` | — | Redis в формате StackExchange.Redis: `host:port,password=...` |
| `Mail:Provider` | `Mail__Provider` | нет | нет | `Smtp` | почтовый сервис ([ADR-0017](adr/0017-mail.md)): `Smtp` или `InMemory` (фейк для тестов, письма остаются в памяти) |
| `Mail:Smtp:Host` | `Mail__Smtp__Host` | нет | при почте `Smtp` | — | SMTP-сервер; в Development — Mailpit, `localhost` |
| `Mail:Smtp:Port` | `Mail__Smtp__Port` | нет | нет | `587` | порт SMTP: `587` со `StartTls`, `465` с `SslOnConnect`; в Development — `1025` (Mailpit) |
| `Mail:Smtp:Security` | `Mail__Smtp__Security` | нет | нет | `StartTls` | защита соединения: `StartTls`, `SslOnConnect` или `None` (только для Mailpit) |
| `Mail:Smtp:Username` | `Mail__Smtp__Username` | нет | нет | — | логин SMTP; без него сервер используется без входа |
| `Mail:Smtp:Password` | `Mail__Smtp__Password` | да | нет | — | пароль SMTP или ключ почтового сервиса |
| `Mail:From:Address` | `Mail__From__Address` | нет | при почте `Smtp` | — | адрес отправителя всех писем, например `noreply@example.org`; в Development — `noreply@localhost` |
| `Mail:From:Name` | `Mail__From__Name` | нет | нет | название из `brand/brand.json` | имя отправителя |

Письма в Development ловит Mailpit из `compose.yaml`: веб-интерфейс — `http://localhost:8025` (порт
`MAILPIT_UI_PORT` из `.env`).

## Мигратор (`backend/src/Migrator`)

| Ключ | Переменная окружения | Секрет | Обязательна | По умолчанию | Что это |
| --- | --- | --- | --- | --- | --- |
| `ConnectionStrings:Platform` | `ConnectionStrings__Platform` | да | да | — | PostgreSQL платформы в формате Npgsql: `Host=...;Port=...;Database=...;Username=...;Password=...` |

## Сидер демо-данных (`backend/tools/Seed`)

Сначала применяет миграции всех модулей, потом пишет демо-данные (ADR-0009). В `Staging` и `Production` без
`Seed:AllowProtectedEnvironment=true` не пишет ничего и выходит с кодом 3.

| Ключ | Переменная окружения | Секрет | Обязательна | По умолчанию | Что это |
| --- | --- | --- | --- | --- | --- |
| `ConnectionStrings:Platform` | `ConnectionStrings__Platform` | да | да | — | PostgreSQL платформы, как у мигратора |
| `Seed:RandomSeed` | `Seed__RandomSeed` | нет | нет | `20260115` | зерно демо-данных: одно зерно — одни и те же данные |
| `Seed:AllowProtectedEnvironment` | `Seed__AllowProtectedEnvironment` | нет | нет | `false` | `true` разрешает писать демо-данные в `Staging` и `Production` |

## Локальное окружение и клиенты

- `.env.example` — порты и пароли инфраструктуры в `compose.yaml` (E01-04); копия `.env` в git не попадает.
  Значения там — только для локальной машины. `appsettings.Development.json` хостов указывают на те же пароли.
- Сайт (`frontend/`, сервер Nuxt) берёт настройки из `runtimeConfig` через переменные `NUXT_*`:

  | Переменная | Секрет | По умолчанию | Что это |
  | --- | --- | --- | --- |
  | `NUXT_API_BASE` | нет | — | адрес API для отрисовки на сервере; на стенде `http://api:8080` |
  | `NUXT_PUBLIC_API_BASE` | нет | тот же адрес сайта | адрес API для браузера |
  | `NUXT_OIDC_ISSUER` | нет | в `nuxt dev` — `http://localhost:5103/` | провайдер входа (E18-02), на стенде — адрес сайта со слешем; должен совпадать с `Identity:Issuer` |
  | `NUXT_OIDC_CLIENT_ID` | нет | `web` | клиент сайта у провайдера |
  | `NUXT_OIDC_REDIRECT_URI` | нет | в `nuxt dev` — `http://localhost:3000/auth/callback` | адрес `/auth/callback` сайта, ровно как в `Identity:Clients:Web:RedirectUris` |
  | `NUXT_SESSION_PASSWORD` | да | в `nuxt dev` создаётся при старте | ключ шифрования cookie сессии и входа, не короче 32 символов; без него собранный сайт не впускает |

  Токены хранит только сервер Nuxt (в памяти процесса, пока нет #29); браузер получает cookie `auth.session`
  (HttpOnly, SameSite=Lax, Secure на HTTPS) с одним случайным идентификатором сессии.
- `admin/` настроек пока не читает. Когда появятся — через `VITE_*` на этапе сборки; секретов в админке нет.
