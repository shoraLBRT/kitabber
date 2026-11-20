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
| `api` | `backend/src/Api`, порт 8080, `/health/live`, `/health/ready`, `/metrics` (ADR-0015) | `backend/Dockerfile`, цель `api` |
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
Caddy. Наружу смотрит только Caddy (порты 80 и 443), он сам получает сертификаты Let's Encrypt:

- `SITE_ADDRESS` (например `stage.example.org`): `/api/*`, `/openapi/*` и `/health/*` → `api` (`/metrics` наружу не отдаётся), остальное →
  `frontend`;
- `ADMIN_ADDRESS` (например `admin.stage.example.org`) → `admin`.

Хосты бэкенда работают в окружении `Staging`: секрет в файле настроек там — ошибка запуска (ADR-0007). Строки
подключения собираются в compose-файле из паролей, пароли приходят из env-файла. Остальная инфраструктура
(OpenSearch, хранилище файлов, ClickHouse, почта) добавится на стенд вместе с задачами, которые начнут ею
пользоваться; управляемые сервисы хостинга можно подставить вместо контейнеров, поменяв строки подключения.

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
  проверяет через Caddy сайт, `/health/live` API и админку (включая клиентский маршрут).

Что автотест не доказывает: что публикация в GHCR и выкладка на реальный сервер прошли — это видно только в
запуске `images` на `main` и `deploy` после подключения стенда.

## Что нужно от владельца

Чтобы подключить stage (production — то же самое в окружении `production`):

1. **Сервер** с Docker Engine и плагином compose, открытыми портами 80 и 443, пользователем для выкладки в группе
   `docker`. Хватит 2 vCPU и 4 ГБ памяти на нынешний состав.
2. **Домен**: две записи DNS на сервер — для сайта и для админки.
3. **GitHub → Settings → Environments → `stage`**:

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

4. **GitHub → Settings → Variables → Repository**: `DEPLOY_STAGE` = `true` — включает автовыкладку на stage после
   слияния в `main`. До этого задание пропускается.
5. Для `production` — то же окружение `production` со своими значениями и **Required reviewers**.
6. По желанию: сделать пакеты GHCR публичными или оставить приватными (выкладка входит в GHCR с `GITHUB_TOKEN`
   своего запуска, ей хватает права `packages: read`).

Хранилище секретов — секреты окружений GitHub Actions, которые выкладка пишет в env-файл (рекомендация
ADR-0007 для первого стенда). Если владелец выберет другое хранилище, меняется только шаг «Write the env file» в
`deploy.yml`.
