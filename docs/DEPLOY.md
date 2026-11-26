# Выкладка и откат

Как образы попадают в реестр и на стенды (E01-03, #10). Хостинг ещё не выбран (открытый вопрос в
[PROJECT_STATE.md](PROJECT_STATE.md)): всё ниже работает без сервера, а подключение стенда — это значения в
настройках GitHub, перечисленные в разделе «Что нужно от владельца». Настройки приложений —
[CONFIG.md](CONFIG.md), правила секретов — [ADR-0007](adr/0007-configuration-and-secrets.md), миграции —
[ADR-0004](adr/0004-database-migrations.md), решение о выкладке — [ADR-0010](adr/0010-images-and-deployment.md).

## Образы

Пять образов, список — только в `scripts/images.ps1`. Реестр — GHCR, имя — `ghcr.io/<владелец>/<репозиторий>/<образ>`
(в нижнем регистре).

| Образ | Что внутри | Dockerfile |
| --- | --- | --- |
| `api` | `backend/src/Api`, порт 8080, `/health/live`, `/health/ready`, `/metrics` (ADR-0015); лимиты частоты в Redis, адрес клиента из `X-Forwarded-For` Caddy (`Http__TrustedProxies`, ADR-0016) | `backend/Dockerfile`, цель `api` |
| `workers` | `backend/src/Workers` | `backend/Dockerfile`, цель `workers` |
| `migrator` | `backend/src/Migrator`, по умолчанию `migrate` | `backend/Dockerfile`, цель `migrator` |
| `frontend` | сервер Nuxt (`.output`), порт 3000 | `frontend/Dockerfile` |
| `admin` | сборка Vite за nginx без root, порт 8080 | `admin/Dockerfile` |

Базовые образы закреплены по дайджесту (`тег@sha256:…`); Renovate обновляет тег и дайджест вместе. Образы
бэкенда работают от непривилегированного пользователя базового образа, секретов в образах нет.

Теги:

- `sha-<7 знаков коммита>` — у каждой сборки из `main`; **этим тегом выкладывают и откатывают**;
- `latest` — последняя сборка из `main`;
- `<версия>` и `<мажор>.<минор>` (например `0.2.0` и `0.2`) — когда release-please выпускает релиз
  (`release.yml` вызывает `images.yml` с тегом релиза).

Собрать локально: `pwsh scripts/images.ps1` (теги `local/kitabber/<образ>:local`).

## Рабочие процессы

| Файл | Когда | Что делает |
| --- | --- | --- |
| `.github/workflows/images.yml`, задание `build` | каждый PR и каждое слияние в `main` | собирает все образы, прогоняет смоук выкладки (`scripts/deploy-test.ps1 -Smoke`); в `main` публикует в GHCR |
| `.github/workflows/images.yml`, задание `deploy-stage` | слияние в `main`, если переменная репозитория `DEPLOY_STAGE` = `true` | выкладывает только что опубликованный `sha-…` на stage через `deploy.yml` |
| `.github/workflows/deploy.yml` | вручную (Actions → deploy → Run workflow) или из `images.yml` | выкладывает выбранный тег на `stage` или `production` |
| `.github/workflows/release.yml`, задание `images` | release-please создал релиз | публикует образы с номером версии |

Публикация использует только `GITHUB_TOKEN` с правом `packages: write`, ключей владельца не нужно. Пока
`DEPLOY_STAGE` не задана, `deploy-stage` пропускается, и CI зелёный без единого секрета хостинга.

## Стенд stage

Файл — `deploy/compose.stage.yaml`, шаблон значений — `deploy/stage.env.example`. Состав: `api`, `workers`,
`frontend`, `admin`, `migrator` (профиль `tools`, запускается только выкладкой), PostgreSQL, Redis, RabbitMQ и
Caddy, а также телеметрия (ниже). Наружу смотрит только Caddy (порты 80 и 443), он сам получает сертификаты Let's Encrypt:

- `SITE_ADDRESS` (например `stage.example.org`): `/api/*`, `/openapi/*`, `/health/*` и хаб реального времени `/hubs/*`
  (WebSocket проходит через Caddy без настроек, ADR-0019) → `api` (`/metrics` наружу не отдаётся), остальное →
  `frontend`; `/img/*` → `imgproxy` (ресайз и конвертация изображений по подписанным ссылкам, ADR-0037; бакет
  он читает тем же ключом `MEDIA_STORAGE_*`, ответы кэшируются на год — CDN можно поставить перед сайтом);
- `ADMIN_ADDRESS` (например `admin.stage.example.org`) → `admin`;
- `GRAFANA_ADDRESS` (например `grafana.stage.example.org`) → `grafana` (вход только паролем администратора).

Хосты бэкенда работают в окружении `Staging`: секрет в файле настроек там — ошибка запуска (ADR-0007). Строки
подключения собираются в compose-файле из паролей, пароли приходят из env-файла. OpenSearch (поиск, ADR-0094)
работает на стенде одним узлом внутри сети стенда, без опубликованного порта; после первого развёртывания и после
смены схемы индексов индексы заполняются командой `docker compose run --rm workers search-reindex`. Остальная
инфраструктура (хранилище файлов, ClickHouse, почта) добавится на стенд вместе с задачами, которые начнут ею
пользоваться; управляемые сервисы хостинга можно подставить вместо контейнеров, поменяв строки подключения.

### Телеметрия стенда

[ADR-0069](adr/0069-stage-telemetry.md). В том же compose-файле: коллектор OpenTelemetry (`otel-collector`),
Prometheus, Loki, Tempo и Grafana. API и воркеры шлют трассы, метрики и логи по OTLP на
`http://otel-collector:4317`; коллектор отдаёт трассы в Tempo, логи в Loki, метрики в Prometheus (приёмник OTLP
Prometheus). Prometheus ещё снимает `/metrics` API внутри сети (задание `platform-api-scrape`). Портов наружу у
них нет; Grafana открыта только через Caddy на `GRAFANA_ADDRESS`, анонимного входа и регистрации нет.

- **Хранение — 14 дней** в каждом хранилище (Prometheus ещё не больше 10 ГБ); тома `prometheus`, `loki`, `tempo`,
  `grafana`. Консольный вывод контейнеров docker хранит не больше 5 × 20 МБ на контейнер.
- **API пишет журнал запросов** (`Microsoft.AspNetCore.Hosting.Diagnostics` на уровне `Information`): строка на
  запрос с trace id. Строки проб и `/metrics` коллектор отбрасывает.
- **Grafana**: источники данных Prometheus, Loki и Tempo уже настроены; из строки лога — переход к трассе
  (поле `trace_id`), из спана — к логам его трассы. Папка **Kitabber**: дашборды «API» (запросы, задержки, доля
  5xx, логи), «.NET runtime» (память, GC, процессор, пул потоков, исключения) и «PostgreSQL (Npgsql)» (пул
  соединений, длительность и ошибки команд). Дашборды и источники — код в compose-файле, правки в интерфейсе не
  сохраняются.
- **Алерт** `ApiServerErrors`: больше 5% ответов API — 5xx пять минут подряд. Правило в Prometheus, Grafana
  показывает его в Alerting. Канала оповещений пока нет (см. «Что нужно от владельца»).
- **Пароль Grafana** Grafana берёт из `GRAFANA_ADMIN_PASSWORD` только при первом запуске, когда создаёт
  администратора. Сменить потом:
  `docker compose -f deploy/compose.stage.yaml --env-file stage.env exec grafana grafana cli admin reset-admin-password <новый>`
  и обновить секрет.
- Nuxt пока не шлёт телеметрию (JS-SDK OpenTelemetry не подключён, ADR-0015); коллектор принимает и OTLP/HTTP на
  порту 4318 для него.

Тот же файл годится для `production` с другим env-файлом и другим `STAGE_PROJECT`; отдельный боевой файл
появится, когда боевому стенду понадобится иное (реплики, внешние базы).

## Выкладка

`scripts/deploy.ps1 -EnvFile <env-файл> -Tag <тег>` по шагам:

1. проверяет compose-файл (`docker compose config`);
2. скачивает образы тега;
3. запускает мигратор **новой** версии: `docker compose run --rm migrator migrate`. Если миграция упала,
   выкладка останавливается, а работающая версия продолжает работать (это проверяет смоук);
4. заменяет контейнеры (`docker compose up --detach --wait`) и ждёт всех healthcheck.

API при старте не мигрирует (ADR-0004), поэтому шаг 3 обязателен и идёт до шага 4. Миграции пишутся так, чтобы
старая версия API работала на новой схеме (сначала добавляем, удаляем — следующим релизом); иначе откат образа
без отката схемы невозможен.

Docker берётся обычный: локальный или на сервере стенда через `DOCKER_HOST=ssh://пользователь@хост`. Так
работает `deploy.yml`: он пишет env-файл из значений GitHub-окружения на раннер и управляет Docker сервера по
SSH, ничего не копируя на сервер.

Руками с машины владельца:

```powershell
$env:DOCKER_HOST = 'ssh://deploy@stage.example.org'
docker login ghcr.io            # если пакеты приватные
pwsh scripts/deploy.ps1 -EnvFile stage.env -Tag sha-1a2b3c4
```

**stage** выкладывается сам после каждого слияния в `main` (когда подключён). **production** — только вручную:
Actions → deploy → Run workflow → `production` и тег, проверенный на stage. Окружение `production` в GitHub
должно требовать подтверждения владельца (Settings → Environments → production → Required reviewers).

## Откат

Откат — это выкладка более раннего тега тем же способом: Actions → deploy → Run workflow → стенд и предыдущий
успешный `sha-…` (он есть в сводке прошлого запуска deploy и в списке пакетов GHCR), или
`scripts/deploy.ps1 -Tag <предыдущий тег>`. Скрипт печатает, какой тег работал до выкладки.

Схема базы при этом **не откатывается**: миграции идут только вперёд, старая версия работает на новой схеме.
Если откатить нужно и схему (миграция оказалась ошибочной), сделайте это **до** выкладки старого тега и
мигратором **новой** версии — только он знает, как отменить свои миграции:

```powershell
docker compose -f deploy/compose.stage.yaml --env-file stage.env run --rm migrator rollback <схема>
pwsh scripts/deploy.ps1 -EnvFile stage.env -Tag <предыдущий тег>
```

`rollback <схема>` отменяет одну последнюю миграцию одного модуля (ADR-0004); повторите для каждой миграции,
которую нужно отменить. Перед откатом схемы на production сделайте резервную копию базы.

## Проверка

- `scripts/deploy-test.ps1` (шаг `deploy config` в `scripts/verify.ps1`): базовые образы закреплены по
  дайджесту; stage-compose запускает ровно образы из `scripts/images.ps1` и проходит `docker compose config` с
  шаблоном; `images.yml` собирает на PR, публикует в GHCR из `main` с тегами `sha-…`, `latest` и версии и не
  требует секретов, кроме `GITHUB_TOKEN`; `deploy-stage` закрыт переменной; `deploy.yml` запускается только
  вручную или вызовом; каждое значение, которое читают рабочие процессы, описано в этом документе.
- `scripts/deploy-test.ps1 -Smoke` (задание `images / build` на каждом PR; локально — шаг `deploy smoke` при
  `VERIFY_IMAGES=1`): собирает образы, выкладывает stage-compose на этот Docker, обновляет до другого тега,
  проверяет, что упавшая миграция останавливает выкладку и оставляет работающую версию, откатывает и каждый раз
  проверяет через Caddy сайт, `/health/live` API и админку (включая клиентский маршрут). После первой выкладки —
  телеметрию: Grafana без пароля отвечает 401; запрос к API с заданным `traceparent` даёт через источники
  данных Grafana трассу в Tempo, строку лога этой трассы в Loki и метрику запросов в Prometheus; Prometheus
  снимает `/metrics` API, правило `ApiServerErrors` загружено, три дашборда на месте.
- Статическая часть проверяет и телеметрию: хосты бэкенда шлют OTLP коллектору, порты публикует только Caddy,
  Caddy не отдаёт наружу ничего, кроме Grafana, пароль Grafana — из `GRAFANA_ADMIN_PASSWORD`, срок хранения
  Prometheus, Loki и Tempo — от 7 до 15 дней, дашборды — JSON и ссылаются только на настроенные источники.

Что автотест не доказывает: что публикация в GHCR и выкладка на реальный сервер прошли — это видно только в
запуске `images` на `main` и `deploy` после подключения стенда.

## Что нужно от владельца

Чтобы подключить stage (production — то же самое в окружении `production`):

1. **Сервер** с Docker Engine и плагином compose, открытыми портами 80 и 443, пользователем для выкладки в группе
   `docker`. Хватит 2 vCPU и 4 ГБ памяти на нынешний состав.
2. **Домен**: три записи DNS на сервер — для сайта, админки и Grafana. С телеметрией серверу нужно ещё около
   1 ГБ памяти и до 20 ГБ диска под 14 дней хранения.
3. **Почтовый сервис** с SMTP (любой, [ADR-0017](adr/0017-mail.md)) и домен отправителя: в DNS этого домена —
   записи SPF, DKIM и DMARC, которые даст почтовый сервис; без них письма попадут в спам. Значения SMTP — в
   таблице ниже. Имя отправителя по умолчанию — название продукта из `brand/brand.json`.
4. **GitHub → Settings → Environments → `stage`**:

   | Имя | Вид | Что это |
   | --- | --- | --- |
   | `DEPLOY_HOST` | переменная | адрес сервера для SSH |
   | `DEPLOY_USER` | переменная | пользователь для выкладки на сервере |
   | `SITE_ADDRESS` | переменная | домен сайта, например `stage.example.org` |
   | `ADMIN_ADDRESS` | переменная | домен админки, например `admin.stage.example.org` |
   | `DEPLOY_SSH_KEY` | секрет | закрытый ключ SSH этого пользователя (отдельный ключ только для выкладки) |
   | `DEPLOY_KNOWN_HOSTS` | секрет | строка `ssh-keyscan <хост>` — отпечаток сервера, проверенный вами |
   | `POSTGRES_PASSWORD` | секрет | пароль PostgreSQL стенда (длинный случайный) |
   | `REDIS_PASSWORD` | секрет | пароль Redis стенда |
   | `RABBITMQ_PASSWORD` | секрет | пароль RabbitMQ стенда |
   | `SMTP_HOST` | переменная | SMTP-сервер почтового сервиса, например `smtp.example.org` (ADR-0017) |
   | `SMTP_PORT` | переменная | порт SMTP: `587` или `465` |
   | `SMTP_SECURITY` | переменная | защита соединения: `StartTls` (порт 587) или `SslOnConnect` (порт 465) |
   | `SMTP_USERNAME` | переменная | логин SMTP |
   | `SMTP_PASSWORD` | секрет | пароль SMTP или ключ почтового сервиса |
   | `MAIL_FROM` | переменная | адрес отправителя писем, например `noreply@stage.example.org` |
   | `IDENTITY_SIGNING_KEY` | секрет | ключ подписи токенов провайдера входа (ADR-0018): `openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -outform DER \| base64 -w0`; свой на каждый стенд, менять — только вместе с выходом всех из системы |
   | `IDENTITY_ENCRYPTION_KEY` | секрет | ключ шифрования кодов авторизации: `openssl rand -base64 32` |
   | `SESSION_PASSWORD` | секрет | ключ cookie сессий сайта, не короче 32 символов: `openssl rand -base64 32` |
   | `MEDIA_STORAGE_URL` | переменная | адрес S3 API хранилища медиа (ADR-0030), например `https://s3.example.org` |
   | `MEDIA_STORAGE_REGION` | переменная | регион S3, например `us-east-1` |
   | `MEDIA_STORAGE_BUCKET` | переменная | бакет медиа; создаёте его вы, приложение бакет на стенде не создаёт |
   | `MEDIA_STORAGE_ACCESS_KEY` | переменная | идентификатор ключа доступа к бакету (чтение, запись и удаление объектов) |
   | `MEDIA_STORAGE_SECRET_KEY` | секрет | секретный ключ доступа к бакету |
   | `IMGPROXY_KEY` | секрет | ключ подписи ссылок на изображения (ADR-0037), hex: `openssl rand -hex 32`; им же подписывает API, смена делает недействительными выданные ссылки |
   | `IMGPROXY_SALT` | секрет | соль подписи ссылок на изображения, hex: `openssl rand -hex 32` |
   | `GRAFANA_ADDRESS` | переменная | домен Grafana стенда, например `grafana.stage.example.org` (ADR-0069) |
   | `GRAFANA_ADMIN_PASSWORD` | секрет | пароль пользователя `admin` в Grafana (длинный случайный); действует с первого запуска, смена — командой из раздела «Телеметрия стенда» |

   Адрес провайдера входа — адрес сайта: выкладка сама пишет `SITE_URL=https://<SITE_ADDRESS>`. Сервер Nuxt
   читает `https://<SITE_ADDRESS>/.well-known/openid-configuration` по публичному адресу, поэтому сервер стенда
   должен открывать собственный домен изнутри.

5. **GitHub → Settings → Variables → Repository**: `DEPLOY_STAGE` = `true` — включает автовыкладку на stage после
   слияния в `main`. До этого задание пропускается.
6. Для `production` — то же окружение `production` со своими значениями и **Required reviewers**.
7. Позже: **канал оповещений** для алерта `ApiServerErrors` (Telegram или почта) — его подключат отдельной
   задачей, когда вы выберете канал. Выбор хранилища телеметрии для production (свой сервер или Grafana Cloud) —
   до открытой беты.
8. По желанию: сделать пакеты GHCR публичными или оставить приватными (выкладка входит в GHCR с `GITHUB_TOKEN`
   своего запуска, ей хватает права `packages: read`).

Хранилище секретов — секреты окружений GitHub Actions, которые выкладка пишет в env-файл (рекомендация
ADR-0007 для первого стенда). Если владелец выберет другое хранилище, меняется только шаг «Write the env file» в
`deploy.yml`.
