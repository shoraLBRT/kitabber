# Состояние проекта

Обновляется в каждом PR. Последнее обновление: 22 ноября 2026 (публикация с проверками и выбором темы, E05-09, #49, ADR-0046; отрисовка блоков на странице поста, E07-03, #51, ADR-0045; заголовок → текст сохраняет разметку, свой блок «Текст», E05-03, #331, #38, ADR-0044; лента «Свежее» с курсорной пагинацией, E08-01, #54, ADR-0043; вставка списков из Word, Google Docs и веб-страниц блоком «Список», E05-02, #37, ADR-0042; адрес поста со слагом из заголовка, E07-02, #50, ADR-0041; компонент изображения со `srcset`, ленивой загрузкой и местом под картинку, E06-04, #47, ADR-0040; меню блоков с поиском и перестановка блоков, E05-07, #41, ADR-0039; капча и лимиты неудачных входов, E03-12, #32, ADR-0038; раздача изображений через imgproxy и показ аватара блога, E06-02, #46, ADR-0037; восстановление и смена пароля, E03-05, #31, ADR-0036; блоки «Цитата» и «Разделитель», E05-05, #40; окно входа и регистрации, E03-04, #30; блок «Список» с вложенностью в один уровень, E05-04, #39, ADR-0033; владелец принял ADR-0023…0031, быстрый пост и лучший комментарий в ленте добавлены в ТЗ; модель поста и API, E07-01, #48; загрузка медиа и хранилище S3, E06-01, #43; блок «Текст» с панелью форматирования, E05-02, #37; блоки «Заголовок» и «Подзаголовок», E05-03, #38; правка имени, описания и аватара блога, E04-03, #44).

## Текущая веха

**M1. MVP** — задачи и критерий выхода в [ROADMAP.md](ROADMAP.md).

**M0. Фундамент — завершена.** Критерий выхода (PR проверяется автоматически; пустой каркас открывается на 1440 и
390 px) показан автотестом: сквозные тесты Playwright (E01-16) открывают каркас в профилях desktop и mobile в
`ci / verify`. Последняя задача вехи, P2 #302 (кэш системных пакетов Playwright в CI), закрыта.

## Следующая задача

Первая открытая задача по [ROADMAP.md](ROADMAP.md) с закрытыми зависимостями.

## Что уже есть

- Требования: [spec.md](spec.md), [telegram-bridge.md](telegram-bridge.md),
  [unified-accounts.md](unified-accounts.md), [content-policy.md](content-policy.md).
- Решения: [ADR-0001](adr/0001-stack.md) (стек, принято), [ADR-0002](adr/0002-identity.md) (аккаунты,
  предложено), [ADR-0003](adr/0003-telegram-bridge.md) (Telegram, предложено).
  [ADR-0004](adr/0004-database-migrations.md) (миграции EF Core, схема и история миграций на модуль, принято в
  задаче E01-05).
  [ADR-0005](adr/0005-messaging-and-cache.md) (MassTransit 8 на RabbitMQ, outbox в схеме модуля, Redis, принято в
  задаче E01-06).
  [ADR-0006](adr/0006-rest-api-conventions.md) (REST: `/api/v1`, ProblemDetails с кодом, курсорная пагинация, снимок
  OpenAPI, принято в задаче E01-07).
  [ADR-0007](adr/0007-configuration-and-secrets.md) (настройки через переменные окружения, проверка на старте,
  gitleaks; принято в задаче E01-13; выкладка E01-03 берёт секреты стендов из окружений GitHub Actions).
  [ADR-0008](adr/0008-backend-test-framework.md) (общий каркас тестов, контейнер на сборку, Respawn, управляемые
  часы, остаёмся на VSTest; принято в задаче E01-15).
  [ADR-0009](adr/0009-demo-data-and-factories.md) (демо-данные и фабрики: одно зерно, фабрики в коде модуля,
  сидер-команда `backend/tools/Seed`; принято в задаче E01-17).
  [ADR-0010](adr/0010-images-and-deployment.md) (образы в GHCR, стенд на Docker Compose, выкладка тегом по SSH, откат
  прежним тегом; предложено в задаче E01-03).
  [ADR-0011](adr/0011-brand-source-and-icons.md) (бренд в `brand/brand.json`, знак из данных, иконки Lucide;
  предложено в задаче E02-01).
  [ADR-0012](adr/0012-design-tokens.md) (дизайн-токены и линтер стилей; предложено в задаче E02-02).
  [ADR-0013](adr/0013-ui-components.md) (свои базовые компоненты без headless-библиотеки, шторка как режим модального
  окна, витрина — страница `/ui` вместо Histoire или Storybook; предложено в задаче E02-04).
  [ADR-0014](adr/0014-typed-api-client.md) (клиент API: `openapi-typescript` и `openapi-fetch` в каждом клиенте,
  проверка расхождения со снимком OpenAPI; принято, задача E01-08).
  [ADR-0015](adr/0015-observability.md) (наблюдаемость: OpenTelemetry с выдачей по OTLP, `traceparent` от Nuxt,
  JSON-логи, `/health/live` и `/health/ready`, `/metrics` для Prometheus; принято, задача E01-09).
  [ADR-0016](adr/0016-rate-limiting.md) (лимиты частоты: фиксированные окна в Redis, по аккаунту и по IP, политики
  из настроек, доверенные прокси; принято, задача E01-10).
  [ADR-0017](adr/0017-mail.md) (почта: шаблоны-файлы с локализацией и снимками, рендер у отправителя, доставка
  воркерами через очередь, SMTP на MailKit за `IEmailSender`, Mailpit в тестах; принято, задача E01-12).
  [ADR-0018](adr/0018-openid-connect-provider.md) (вход: OpenIddict 7 в модуле Identity, только код авторизации с
  PKCE S256, публичные клиенты из настроек, JWT-токены доступа, проверка в API по `Auth:Authority`, ключи — секреты
  стенда, сайт входит через свой сервер на `openid-client`; принято, задача E18-02).
  [ADR-0019](adr/0019-realtime-channel.md) (реальное время: один хаб SignalR в API, темы `account:` и `post:` с
  политиками подписки, билет хаба на 60 секунд от сервера сайта вместо токена в браузере, бесконечное тихое
  переподключение, Redis backplane; принято, задача E01-11).
  [ADR-0020](adr/0020-email-normalization-and-confirmation.md) (почта аккаунта: нормализация с точками только у
  Gmail, уникальность по всем аккаунтам, подтверждение шестизначным кодом через outbox, политика `confirmed-email`,
  ключ слияния — только подтверждённая почта; принято, задача E18-03).
  [ADR-0021](adr/0021-registration-and-passwords.md) (регистрация: пароль Argon2id строкой PHC, три анонимных вызова
  с идентификатором регистрации, подтверждение ставит сессию провайдера и сайт входит обычным кодом авторизации,
  таймер повторной отправки 60 секунд на сервере, новая регистрация заменяет неподтверждённую; принято, задача
  E03-01).
  [ADR-0022](adr/0022-sign-in-by-password-and-code.md) (вход по паролю и по коду: ответы не выдают, есть ли адрес,
  код уходит на любой адрес и создаёт аккаунт, если подтверждённого нет; неподтверждённый адрес держится только до
  чужого подтверждения; лимиты на адрес сети и на адрес почты; принято, задача E03-02).
  [ADR-0023](adr/0023-block-schemas-and-html-cleaning.md) (блоки поста: документ `{version, blocks}`, строгие схемы
  по версиям, ошибка с местом, очистка встроенного HTML белым списком на HtmlSanitizer; принято, задача E05-08).
  [ADR-0024](adr/0024-session-refresh-and-sign-out.md) (сессия сайта: refresh-токены по `offline_access` с ротацией,
  повтор погашенного после 10-секундного окна отзывает цепочку, сайт обновляет токен сам до истечения, сессии сайта в
  Redis, выход отзывает refresh-токен и завершает сессию провайдера; принято, задача E03-03).
  [ADR-0025](adr/0025-blogs-subsite-and-profile.md) (блоги: Subsite с типом в схеме `blogs`, событие
  `AccountActivated` при первом подтверждении почты, личный блог создают воркеры, запасной путь — `/profiles/me`,
  форма никнейма; принято, задача E04-01).
  [ADR-0026](adr/0026-block-editor-core.md) (ядро редактора на клиенте: Editor.js за интерфейсом движка, модель
  документа в формате ADR-0023, отмена и повтор снимками документа между блоками, окно 800×700 и весь экран на
  телефоне; принято, задача E05-01).
  [ADR-0028](adr/0028-text-block-formatting.md) (блок «Текст»: панель Editor.js над выделением, свои инструменты
  «маркер» и «ссылка» с проверкой адреса и `rel` сервера, очистка вставки и текстовых полей по белому списку
  ADR-0023; принято, задача E05-02).
  [ADR-0029](adr/0029-post-model-and-api.md) (пост: схема `posts`, тело — очищенный документ блоков в `jsonb`,
  черновик и опубликованный, происхождение `native`/`telegram` с уникальным источником, мягкое удаление, права
  автора, блоги через `Blogs.Contracts`; принято, задача E07-01).
  [ADR-0034](adr/0034-profile-editing-and-avatar.md) (правка профиля: `PUT /profiles/me` заменяет имя, описание и
  аватар, аватар — id своего изображения из `Media.Contracts`, кадрирование в браузере в квадрат 512 px JPEG, показ —
  с раздачей медиа E06-02; предложено, задача E04-03).
  [ADR-0036](adr/0036-password-reset-and-change.md) (сброс пароля по коду отзывает все сессии через метку в cookie
  провайдера и отзыв токенов OpenIddict, смена с текущим паролем сессии оставляет; предложено, задача E03-05).
  [ADR-0037](adr/0037-image-delivery-imgproxy.md) (раздача изображений: imgproxy читает бакет, ссылки подписывает
  API, WebP и AVIF по расширению, вырезка или ширина без увеличения, кэш на год с `ETag`; ключ объекта виден в
  ссылке — уточнение ADR-0030; предложено, задача E06-02).
  [ADR-0039](adr/0039-block-menu-and-moving-blocks.md) (меню блоков Editor.js, на телефоне — шторка со своим поиском;
  перестановка блоков за «⋮⋮» на событиях указателя и `Ctrl+Shift+↑/↓` через `blocks.move`; предложено, задача E05-07).
  [ADR-0040](adr/0040-image-component.md) (компонент изображения: место по пропорциям, `srcset` по ширинам из API,
  нативная ленивая загрузка, один формат WebP без `<picture>`; предложено, задача E06-04).
  [ADR-0041](adr/0041-post-address-and-slug.md) (адрес поста `<адрес блога>/<id>-<слаг>`, пост находится по UUID,
  неверный слаг или блог — 301 на канонический адрес, слаг не хранится и строится из заголовка; предложено, задача
  E07-02).
  [ADR-0042](adr/0042-pasted-lists.md) (вставленные списки Word, Google Docs и веб-страниц — блок «Список» с
  уровнями; предложено, задача E05-02).
- Схемы блоков на сервере (E05-08, #36): модуль `Platform.Modules.Editor` без своей схемы базы и контракт
  `Platform.Modules.Editor.Contracts` (`IBlockDocumentValidator`) для модуля постов (#48) и публикации (#49).
  Версия схем 1: `paragraph`, `header` (уровни 2 и 3), `list`, `quote`, `delimiter`; лишнее поле, неизвестный тип,
  неверный id или версия отклоняются с путём вида `blocks[2].data.level`; текстовые поля чистятся до `b`, `strong`,
  `i`, `em`, `mark`, `a` (только `http`, `https`, `mailto`, с `rel="nofollow ugc noopener noreferrer"`) и `br`.
- Ядро блочного редактора на клиенте (E05-01, #35, [ADR-0026](adr/0026-block-editor-core.md)): страница `/write`
  («Написать» в шапке и плавающая кнопка ведут сюда), окно `EditorWindow` 800×700 px на десктопе с «развернуть» и
  «закрыть», весь экран на телефоне; `BlockEditor` — Editor.js 2.31 за интерфейсом `EditorEngine`
  (`frontend/editor/`), документ в формате ADR-0023 (`toPostDocument`, `toEditorData`), отмена и повтор между
  блоками (`Ctrl+Z`, `Ctrl+Shift+Z`, `Ctrl+Y` и кнопки в шапке) с возвратом каретки, состояния «загрузка» (скелетон)
  и «ошибка» («Повторить»). Пока подключён только абзац; остальные блоки, меню блоков, черновики и
  «Опубликовать» — задачи E05-03…E05-10, #48, #49 (публикация есть, см. ниже). Токены `--size-editor-width` и
  `--size-editor-height`.
- Блоки «Заголовок» (H2) и «Подзаголовок» (H3) (E05-03, #38, [ADR-0031](adr/0031-heading-blocks-and-anchors.md)):
  свой инструмент `frontend/editor/header.ts`, два пункта меню блоков и «Превратить в»; якорь — `h-<id блока>`
  (`headerAnchor`), он переживает правку текста, отмену и повтор. Превращение текст ↔ заголовок сохраняет текст и
  разметку в обе стороны: блок «Текст» — свой абзац `frontend/editor/text.ts` с плоским `sanitize` вместо
  встроенного абзаца Editor.js 2.31 (#331, [ADR-0044](adr/0044-own-text-block.md), предложено). Задача #38
  закрыта. Распознавания `h2`/`h3` при вставке нет.
- Блоки «Маркированный список» и «Нумерованный список» (E05-04, #39, [ADR-0033](adr/0033-list-block-nesting.md),
  предложено, ждёт подтверждения): свой инструмент `frontend/editor/list.ts`, вложенность в один уровень —
  необязательное поле `levels` блока `list` версии 1 (пустое — все пункты наверху, так читаются прежние документы;
  сервер проверяет длину и первый пункт). `Enter` делит пункт, в пустом пункте поднимает вложенный или завершает
  список; `Tab`/`Shift+Tab` вкладывают и поднимают; тип переключается в меню блока и в «Превратить в» без потери
  пунктов, разметки и уровней. Вставленные списки Word, Google Docs и веб-страниц становятся этим блоком (ADR-0042).
- Блоки «Цитата» и «Разделитель» (E05-05, #40; схемы `quote` и `delimiter` версии 1 — ADR-0023, сервер их уже
  проверял): свои инструменты `frontend/editor/quote.ts` и `frontend/editor/delimiter.ts`. Цитата — два поля,
  текст и подпись источника (`text`, `caption`), с панелью форматирования в обоих; `Enter` в тексте ведёт в
  подпись, `Enter` в подписи продолжает текстом после цитаты; цитата → текст дописывает подпись через « — ».
  Разделитель без содержимого и полей ввода; после добавления каретка идёт в блок под ним. Удаляется одной
  клавишей: `Backspace` в начале следующего блока, `Delete` в конце предыдущего, или `Backspace`/`Delete`, когда он
  выделен щелчком или стрелкой, — тогда без пустого блока на его месте. Страницы чтения пока нет.
- Меню блоков и перестановка (E05-07, #41, [ADR-0039](adr/0039-block-menu-and-moving-blocks.md), предложено, ждёт
  подтверждения): меню «+» и «/» в пустом блоке — меню Editor.js с поиском, стрелками и `Enter`; на телефоне —
  нижняя шторка, в которую `frontend/editor/menu.ts` добавляет поле поиска (у Editor.js его там нет). Блок
  перетаскивается за «⋮⋮» (указатель: мышь, перо, палец) и переносится `Ctrl+Shift+↑/↓` — `frontend/editor/move.ts`;
  перенос — один шаг истории. Панель блоков над клавиатурой телефона — E05-10 (#42).
- Блок «Текст» (E05-02, #37, [ADR-0028](adr/0028-text-block-formatting.md)): всплывающая панель над выделением
  (под ним, если сверху нет места) — жирный, курсив, маркер, ссылка; ссылка принимает только `http`, `https`,
  `mailto` и получает `rel="nofollow ugc noopener noreferrer"`, иначе ошибка под полем; вставка из Word, Google Docs
  и веб-страниц чистится до белого списка ADR-0023 (`frontend/editor/inline.ts`, `tools.ts`), документ чистит
  каждое текстовое поле тем же списком. Списки из буфера вставляются блоком «Список» с уровнями, маркер Word
  выбрасывается ([ADR-0042](adr/0042-pasted-lists.md)). Панель форматирования над клавиатурой телефона — E05-10 (#42).
  `POST /api/v1/editor/documents/validation` (только вошедшим) возвращает очищенный документ или 400 с ошибками.
  Не сделано здесь: медиа и остальные блоки (добавляют свои задачи), лимиты размера поста и блока (сделаны в #49, ADR-0046).
- Бэклог в GitHub Issues: 262 задачи в 19 эпиках и 8 вехах; порядок — в [ROADMAP.md](ROADMAP.md).
- Процесс: `CLAUDE.md`, `scripts/verify.ps1`, CI (`ci / verify`), проверка заголовка PR, release-please,
  конфигурация Renovate (в том числе regex-менеджер для версии gitleaks в `ci.yml`, #285).

- Каркас монорепозитория (E01-01): решение .NET `backend/Platform.slnx` (хосты `Api` и `Workers`, net10.0),
  веб-клиент на Nuxt 3 в `frontend/`, админка на Vue 3 + Vite в `admin/`; общие `.editorconfig`,
  `.prettierrc.json`, `global.json`. Структура и линтеры описаны в [ARCHITECTURE.md](ARCHITECTURE.md).
- `scripts/verify.ps1` проверяет структуру репозитория, сборку и `dotnet format` бэкенда, ESLint, Prettier,
  проверку типов и `pnpm build` обоих клиентов. CI ставит .NET по `global.json`, pnpm и Node 22.
- CI (E01-02): `ci / verify` запускает `scripts/verify.ps1` с кэшами NuGet и pnpm и тайм-аутом 10 минут;
  таблица шагов и сводка отчётов о тестах (`artifacts/test-results`, TRX и JUnit) видны в сводке запуска, отчёты
  выкладываются артефактом. Самопроверка `scripts/verify-selftest.ps1` (шаг verify) доказывает, что упавший шаг
  роняет проверку, линтеры отвергают плохой код и workflow соблюдает эти ограничения. Системные пакеты Chromium
  (#302) ставятся из кэша .deb-файлов (`scripts/ci/playwright-deps.sh`), а не из зеркала Ubuntu на каждом прогоне;
  подробности — [TESTING.md](TESTING.md).
- Локальное окружение (E01-04): `compose.yaml` в корне поднимает PostgreSQL, Redis, RabbitMQ, OpenSearch,
  MinIO, imgproxy, Mailpit и ClickHouse командой `docker compose up -d --wait`; порты и пароли — из `.env`
  (шаблон `.env.example`), запуск за три команды описан в README. `scripts/dev-env-test.ps1` проверяет файл
  статически и смоук-тестом поднимает окружение на свободных портах (healthcheck, доступ с хоста, пароли).
  Без запущенного Docker смоук локально помечается `skipped`, в CI обязателен. MinIO — образ Chainguard
  `cgr.dev/chainguard/minio`, закреплённый по digest: официальные образы MinIO больше не публикуются.

- База данных (E01-05): EF Core на PostgreSQL. `Platform.Persistence` даёт `ModuleDbContext<T>`: у модуля своя
  схема (имя модуля в snake_case), свой `DbContext` и своя история миграций `<схема>.__ef_migrations_history`;
  имена в базе — snake_case. Команда `dotnet run --project backend/src/Migrator` (`migrate`, `status`,
  `rollback <схема>`) накатывает миграции всех модулей из `ModuleCatalog`; API их не накатывает. Справочные данные —
  в миграциях (`HasData`), демо-данные — сидер E01-17. Правила — [ADR-0004](adr/0004-database-migrations.md).
  Модулей пока нет, поэтому `ModuleCatalog` пуст; накат на чистую базу, независимость схем и откат последней
  миграции проверяются на двух образцовых модулях в `backend/tests/Platform.Persistence.IntegrationTests`
  (PostgreSQL в Testcontainers; без Docker локально тесты пропускаются, в CI падают). Тесты пока запускаются через
  VSTest (`IsTestingPlatformApplication=false`), см. ADR-0008.

- Очереди, воркеры и кэш (E01-06): `Platform.Messaging` — шина MassTransit 8.5 (Apache-2.0; 9+ платная, Renovate
  её не предлагает) на RabbitMQ или тестовом транспорте `InMemory` (`Messaging:Transport`); outbox и inbox модуля
  лежат в его схеме (`AddOutboxTables`, `AddModuleOutbox<T>`, `ModuleConsumerDefinition<,>`). `Platform.Caching` —
  `IDistributedCache` на Redis или в памяти (`Cache:Provider`). Хост `Platform.Workers` (`WorkerHost`) собирает шину,
  потребители модулей и кэш; модулей пока нет, `WorkerHost.AddModules` пуст, API шину ещё не подключает.
  `backend/tests/Platform.Messaging.IntegrationTests` (PostgreSQL, RabbitMQ, Redis в Testcontainers) проверяют:
  событие из транзакции доставляется ровно один раз и не уходит без коммита, повторная доставка отбрасывается
  inbox, сообщения, опубликованные при остановленном воркере, обрабатываются после перезапуска; тестовый
  транспорт и кэш в памяти работают без Docker. Правила — [ADR-0005](adr/0005-messaging-and-cache.md). Ограничение:
  bus outbox — один на хост; несколько публикующих модулей в одном хосте — задача #281.

- Каркас API (E01-07): `Platform.Http` — версия в пути `/api/v1` (модуль реализует `IApiEndpoints` и
  регистрируется `AddApiEndpoints<T>()`), все ошибки — RFC 9457 problem details с полями `code` и `traceId`
  (`ApiProblems`, `ApiProblemException`, коды `ApiErrorCodes`; 401/403/404/405, ошибки валидации и чтения тела,
  непредвиденные исключения без их текста), валидация DataAnnotations встроенным генератором .NET 10 (каждый модуль
  вызывает `AddValidation()`, типы запроса публичные), курсорная пагинация `PageQuery` → `PageRequest<TCursor>` →
  `CursorPage<T>` (`limit` 1–100, по умолчанию 20; курсор — base64url JSON ключа сортировки). OpenAPI `v1`
  отдаётся по `/openapi/v1.json`, пишется при сборке и сравнивается со снимком `backend/src/Api/openapi/v1.json`
  (обновление — `UPDATE_OPENAPI_SNAPSHOT=1`). Тесты `backend/tests/Platform.Api.Tests` (хост в процессе, без
  Docker): формат ошибок, права доступа на тестовой схеме аутентификации, обход курсоров, снимок. Правила —
  [ADR-0006](adr/0006-rest-api-conventions.md). Эндпоинтов продукта пока нет, поэтому снимок пуст (`paths: {}`).
- Типизированный клиент API (E01-08, [ADR-0014](adr/0014-typed-api-client.md)): `frontend/api/` и `admin/src/api/` —
  `schema.d.ts`, сгенерированный `openapi-typescript` 7 из снимка `backend/src/Api/openapi/v1.json` командой
  `pnpm run api:generate`, и `createApiClient()` на `openapi-fetch`. `tests/api-client.spec.ts` в обоих клиентах
  запускает ту же команду и сравнивает с закоммиченным файлом, запрещает ручные `fetch`/`$fetch`/`useFetch` и строки
  `/api/` вне клиента и проверяет, что ошибка приходит как problem details. Пока `paths` пуст. Клиент сайта
  доступен как `useNuxtApp().$api` (`plugins/api.ts`, E01-09); адрес API — `runtimeConfig.apiBase`
  (`NUXT_API_BASE`, для отрисовки на сервере; на стенде `http://api:8080`) и `runtimeConfig.public.apiBase`
  (`NUXT_PUBLIC_API_BASE`, для браузера; по умолчанию тот же адрес сайта).
- Наблюдаемость (E01-09, [ADR-0015](adr/0015-observability.md)): библиотека `backend/src/Observability`
  (`AddPlatformObservability`) в API и воркерах — трассы, метрики и логи OpenTelemetry по источникам `Platform.*`,
  `Npgsql`, `MassTransit`, исходящему HTTP и среде .NET; выдача по OTLP только при `OTEL_EXPORTER_OTLP_ENDPOINT`.
  Логи в консоль — JSON с `TraceId`/`SpanId` (в Development — `simple`). Клиент API сайта ставит W3C `traceparent`
  на каждый запрос, при серверном рендеринге — в трассе страницы; API продолжает трассу до спана Npgsql, `traceId`
  в problem details тот же. У API: `/health/live` (без проверок), `/health/ready` (проверки с тегом `ready`: база
  модуля из `AddModuleDbContext`, кэш из `AddPlatformCache`, шина MassTransit; 503 при сбое) и `/metrics` в формате
  Prometheus (пакет beta). Прежний `/health` убран; healthcheck API на стенде — `/health/ready`, Caddy отдаёт
  `/health/*`, но не `/metrics`. Фейк коллектора для тестов — `TestTelemetry`. Тесты — `ObservabilityTests`
  (`Platform.Api.Tests`), `ObservabilityIntegrationTests` (`Platform.Api.IntegrationTests`: трасса до базы и
  готовность на PostgreSQL и Redis), `WorkerObservabilityTests` (`Platform.Configuration.Tests`),
  `frontend/tests/api-trace.spec.ts`. Коллектора, хранилищ и оповещений пока нет — см. открытые вопросы.
- Лимиты частоты запросов (E01-10, [ADR-0016](adr/0016-rate-limiting.md)): `Platform.Http.RateLimiting` — промежуточное
  ПО после аутентификации считает каждый запрос под `/api` в политики `Default` (300 в минуту) и `Writes` (`POST`,
  `PUT`, `PATCH`, `DELETE`, 60 в минуту) по аккаунту, аноним — по IP (IPv6 — по /64); сверх лимита — 429,
  `Retry-After` и problem details `too_many_requests`. Модуль объявляет свою политику (`AddRateLimitPolicy`) и вешает
  её на эндпоинт (`.WithRateLimit`), исключение — `.WithoutRateLimit()`. Значения — настройки
  `RateLimiting:Policies:<имя>:...` и `RateLimiting:Enabled` ([CONFIG.md](CONFIG.md)). Счётчики — `IRateCounter` из
  `Platform.Caching` (Lua-скрипт `INCR` + `PEXPIRE` в Redis, фейк в памяти на `TimeProvider`), общие для всех
  экземпляров API; при сбое Redis запрос проходит. API теперь подключает кэш (`ConnectionStrings:Redis`).
  `X-Forwarded-For` принимается только от loopback и сетей `Http:TrustedProxies` (на стенде — частные сети Docker);
  Nuxt при серверной отрисовке передаёт API входящий `x-forwarded-for` (`frontend/api/forwarded.ts`). Тесты —
  `RateLimitingTests` (`Platform.Api.Tests`), `RateLimitingIntegrationTests` (два экземпляра API на одном Redis),
  счётчики в `CacheTests`, `frontend/tests/api-forwarded.spec.ts`. Капча после серии неудачных входов — E03-12 (#32, ADR-0038), ниже.
- Почта (E01-12, [ADR-0017](adr/0017-mail.md)): `Platform.Mail` — `IMailer.SendAsync(адрес, шаблон, культура,
  значения)` рендерит письмо и публикует `SendEmail`; воркеры (`SendEmailConsumer`) доставляют его через
  `IEmailSender` с повтором шины, после повторов — очередь `send-email_error`. Отправители: `SmtpEmailSender`
  (MailKit 4, MIT; `Mail:Smtp:*`, `Mail:From:*`) и фейк `InMemoryEmailSender` (`Mail:Provider=InMemory`). Шаблоны —
  файлы `backend/src/Mail/Templates/<культура>/` (тема, текст, HTML, общий макет), поля `{{имя}}`, название и цвета —
  из `brand/brand.json`; культура откатывается к родителю, затем к русскому. Пока один шаблон — `login-code` (код
  входа); шаблоны остальных писем добавят их задачи. Development шлёт в Mailpit (`localhost:1025`, интерфейс —
  `:8025`); тесты — `Platform.Mail.Tests` (снимки, очередь и повтор, Mailpit в Testcontainers: `MailpitFixture`,
  `MailpitClient` в `Platform.Testing`). Стенд берёт SMTP из окружения GitHub (`SMTP_*`, `MAIL_FROM`,
  [DEPLOY.md](DEPLOY.md#что-нужно-от-владельца)). API шину и почту ещё не подключает — это сделает первая задача,
  которой нужна почта из API.

- Настройки и секреты (E01-13): `Platform.Configuration` — окружения `Development`/`Test`/`Staging`/`Production`
  (другое имя не стартует), слои `appsettings.json` → `appsettings.{Окружение}.json` → user secrets → переменные
  окружения → командная строка → файлы секретов из `Secrets:Directory`. Библиотеки объявляют свои настройки
  (`Setting`: секрет, допустимые значения, когда обязательна), хост проверяет их в `ConfigurePlatform` до сборки и
  не стартует, называя каждую отсутствующую настройку и её переменную окружения; в Staging и Production секрет из
  файла настроек — тоже ошибка. Все настройки — в [CONFIG.md](CONFIG.md), тест сверяет документ с кодом и
  проверяет, что в `appsettings.json`/`Staging`/`Production` нет секретов. `scripts/secret-scan.ps1` (шаг
  `secret scan`) гоняет gitleaks по всем файлам, которые git закоммитил бы, после проверки на подложенном токене;
  CI ставит gitleaks 8.28.0 и сверяет архив с контрольными суммами релиза; версию обновляет Renovate (regex-менеджер в `renovate.json`, #285, самопроверка verify проверяет, что он находит версию в `ci.yml`); локально шаг пропускается без gitleaks. Тесты — `backend/tests/Platform.Configuration.Tests`.
  Правила и варианты хранилища секретов для стендов — [ADR-0007](adr/0007-configuration-and-secrets.md); выбор
  хранилища — за владельцем; выкладка E01-03 пишет env-файл из секретов окружения GitHub, код от этого не зависит.

- Каркас серверных тестов (E01-15): библиотека `backend/tests/Platform.Testing` — контейнеры PostgreSQL, Redis,
  RabbitMQ из образов `compose.yaml` (один на тестовую сборку, `AssemblyFixture`), `PlatformApiFactory` (API в
  процессе, окружение `Test`, `WithSetting`/`WithModule`), `IntegrationApiFactory` (API на PostgreSQL и Redis:
  база и номерная база Redis на тестовый класс, миграции модулей один раз, `ResetAsync()` — Respawn и `FLUSHDB`
  перед каждым тестом), `TestAuthentication` (заголовки `X-Test-Account`/`X-Test-Role`), `DatabaseReset`,
  `TestClock` (`FakeTimeProvider`). API регистрирует `TimeProvider.System`, фабрика подменяет его. Все четыре
  прежних тестовых проекта переведены на каркас; образец — `backend/tests/Platform.Api.IntegrationTests`
  (образцовый модуль заметок: успешный путь, 401/403, ошибка валидации, кэш в Redis, часы, сброс). Раннер —
  по-прежнему VSTest: переход на Microsoft.Testing.Platform отклонён (TRX и сводка CI), причины — в
  [ADR-0008](adr/0008-backend-test-framework.md). Правила — [TESTING.md](TESTING.md).
- Модуль Identity (E18-01, [ADR-0002](adr/0002-identity.md), FR-ID-1): первый модуль платформы,
  `backend/src/Modules/Identity` (`Platform.Modules.Identity`), схема `identity`, таблица `accounts` (`id`,
  `created_at`) и миграция `CreateAccounts`. Аккаунт — только глобальный идентификатор: `account_id` — UUID v7 по
  часам `TimeProvider` (`Account.Create`, `AccountIds`); профиль (имя, никнейм, аватар) будет в модуле Blogs (#33)
  и сошлётся на аккаунт только по `account_id`. Почта (#26), внешние идентичности (#88), вход по OIDC (#24) и
  события `account.*` (#89) добавят свои задачи. Модуль подключён к мигратору (`ModuleCatalog`) и сидеру
  (`SeedCatalog`: 20 демо-аккаунтов `AccountFactory`; их идентификаторы — `Id("account", n)` из зерна, то есть
  UUID v8, а не v7, чтобы фабрики других модулей брали авторов без чтения схемы `identity`). Граница FR-ID-1 проверяется дважды: архитектурный тест
  читает модели EF Core всех модулей (столбцы почты, паролей и внешних идентификаторов только в `identity`, нет
  внешних ключей между схемами, таблицы только в своей схеме), интеграционный тест мигратора смотрит то же в
  реальной базе (`information_schema`, `pg_constraint`), чтобы поймать и миграцию на голом SQL.
- Почта аккаунта (E18-03, [ADR-0020](adr/0020-email-normalization-and-confirmation.md), FR-ID-3): в
  `identity.accounts` — `email` (как введена), `normalized_email` (уникальный индекс `ux_accounts_normalized_email`
  по всем аккаунтам) и `email_confirmed_at`; миграция `AddAccountEmail` добавляет их, таблицу кодов
  `email_confirmations` и таблицы outbox модуля. Нормализация `EmailAddresses.Normalize`: NFC и нижний регистр имени,
  IDN в punycode для домена, точки убираются только у Gmail (`googlemail.com` → `gmail.com`), `+метки` остаются.
  `AccountEmails` (модуль Identity): задать почту — шестизначный код письмом `email-confirmation` через outbox
  (30 минут, 5 попыток, хранится только хеш с `account_id`), подтвердить, найти аккаунт по **подтверждённой** почте —
  ключ будущего слияния (E18-06). Подтверждённая почта остаётся, пока новая не подтверждена. API
  `/api/v1/account/email` (`GET`, `PUT`, `POST /confirmation`), лимит писем `identity.email-codes` — 5 в час на
  аккаунт. Право публиковать — политика `confirmed-email` (`AccountPolicies` в `Platform.Http`): Identity проверяет
  базу на каждом запросе, отказ — 403 `email_not_confirmed`; эндпоинты постов и комментариев должны её требовать.
  API теперь подключает шину (`AddPlatformMessaging`, в тестах `Messaging:Transport=InMemory`) и почту
  (`AddPlatformMail`). Демо-аккаунты — с подтверждёнными `demo<n>@example.com`. Окна ввода почты и кода на сайте
  нет (окно входа E03-04 — ниже); регистрация по почте — E03-01 (ниже).
- Регистрация по почте и паролю (E03-01, [ADR-0021](adr/0021-registration-and-passwords.md), FR-AUTH): в
  `identity.accounts` — `password_hash` (Argon2id, строка PHC; миграция `AddAccountPassword`), `PasswordHasher`
  (пароль 6–128 символов, `Hash`, `Verify` — готов для входа #28). `Registrations` и анонимные
  `/api/v1/registration`: `POST` (аккаунт с неподтверждённой почтой, код письмом, 202 `{ registrationId,
  resendAfterSeconds }`), `POST /{id}/code` (новый код не раньше 60 секунд, иначе 429 `code_too_soon` с
  `Retry-After`), `POST /{id}/confirmation` (204 и cookie провайдера `identity.session`; 400
  `confirmation_invalid`). Лимит `identity.registrations` — 10 в час на адрес сети. Неподтверждённую регистрацию
  заменяет новая регистрация того же адреса после таймера; подтвердить можно только последнюю. Сайт: страница
  `/register` (`components/auth/RegistrationForm.vue`, вызовы — `api/registration.ts`): почта и пароль, шаг
  кода с обратным отсчётом, ошибки API; после подтверждения — `/auth/sign-in`, и провайдер выдаёт код без окна
  входа. `nuxt dev` проксирует `/api` на API, чтобы cookie провайдера ставилась на том же хосте. Тесты —
  `Registration/RegistrationTests` (письма доставляются SMTP-отправителем в Mailpit, коды читаются оттуда; поток до
  токена доступа, неверный и просроченный код, таймер, валидация, занятый адрес, замена чужой регистрации, лимит),
  `PasswordHasherTests`, `frontend/tests/auth/RegistrationForm.spec.ts`, сквозной `frontend/e2e/registration.spec.ts`
  на фейковом API (`FAKE_RESEND_SECONDS=3`, письма — `GET /__letters`). Регистрация есть и в окне входа (E03-04, ниже).
- Блоги и профиль (E04-01, [ADR-0025](adr/0025-blogs-subsite-and-profile.md), FR-PROFILE): модуль
  `Platform.Modules.Blogs`, схема `blogs`, сущность `Subsite` (`Personal` с `owner_account_id` или `Topic`), один личный
  блог на аккаунт и один блог на никнейм (уникальные индексы). Правила `SubsiteRules`: название до 25 символов,
  описание до 160 (считаются графемы), никнейм — латиница, цифры и `_`, 3–32, с буквы, в нижнем регистре, не `id123`.
  Identity публикует `AccountActivated` (`Platform.Modules.Identity.Contracts`) через свой outbox, когда почта аккаунта
  подтверждена впервые (регистрация или первый вход по коду); `AccountActivatedConsumer` в воркерах создаёт личный блог
  с inbox в схеме `blogs` (воркерам теперь нужна `ConnectionStrings:Platform`; модули подключаются в
  `WorkerHost.CreateBuilder`, а `WorkerHost.Configure` остаётся без модулей для тестов). API: `GET /api/v1/profiles/{id}`
  и `GET /api/v1/profiles/by-nickname/{nickname}` открыты всем (404 `profile_not_found`, 400 `validation_failed`),
  `GET /api/v1/profiles/me` — вошедшему, создаёт личный блог, если его ещё нет; `registeredAt` видит только владелец.
  Демо-данные: личные блоги 20 демо-аккаунтов с никнеймами `demo0`…`demo19`. Правила никнейма — E04-05.
- Редактирование профиля (E04-03, #44, [ADR-0034](adr/0034-profile-editing-and-avatar.md), FR-PROFILE):
  `PUT /api/v1/profiles/me` (вошедшему, `ProfileInput`: `name`, `description`, `avatarId`; каждое поле заменяется,
  пустое очищает) проверяет пределы `SubsiteRules` (400 `validation_failed` с полями, ничего не сохраняется) и
  аватар — изображение этого же аккаунта через контракт `Platform.Modules.Media.Contracts` (`IMediaLibrary`); чужой
  и несуществующий файл отвечают одинаково. В `Subsite` — `avatar_media_id` (миграция `AddSubsiteAvatar`), в
  `Profile` — `avatarId`. Клиент: страница `/settings/blog` (раздел «Блог» экрана «Настройки», docs/ui/settings.md):
  аватар, название со счётчиком `N/25`, описание со счётчиком `N/160` (графемы, `Intl.Segmenter`), ошибка предела
  сразу при вводе и ошибки полей API под полями; картинка кадрируется в окне (`AvatarCropper`: перетаскивание,
  стрелки, масштаб) в квадрат 512 px JPEG (`utils/avatar-crop.ts`) и при «Сохранить» уходит через `/bff/media` в
  `POST /api/v1/media`, потом профиль — через `PUT /bff/own-profile`; тосты «Изменения сохранены» и «Не удалось
  сохранить». Гость уходит на вход и возвращается. Аватар показывается в шапке блога по `Profile.avatarUrl`
  (подписанная ссылка imgproxy на квадрат 128 px WebP, E06-02, ADR-0037); без аватара — заглушка с буквой. Нет
  здесь: никнейма (E04-05), остальных
  настроек раздела, вопроса при уходе с несохранёнными изменениями, уборки старых файлов аватара.
- Пост (E07-01, [ADR-0029](adr/0029-post-model-and-api.md), FR-POST): модуль `Platform.Modules.Posts`, схема `posts`,
  сущность `Post` — автор (`author_account_id`), блог (`blog_id`, личный блог или тема), необязательный заголовок,
  тело `jsonb` (только очищенный `IBlockDocumentValidator` документ), статус `Draft`/`Published`, `origin`
  (`Native` по умолчанию, `Telegram` — с `source_channel`, `source_message` и необязательной `source_url`,
  уникальный индекс `ux_posts_source`), мягкое удаление (`deleted_at` и фильтр запросов). Контракт
  `Platform.Modules.Blogs.Contracts` (`IBlogDirectory`: блог по id и личный блог аккаунта). API `/api/v1/posts`:
  `POST` создаёт черновик (без `blogId` — в личном блоге автора), `GET /{id}` открыт всем для опубликованных и только
  автору для черновика, `PUT /{id}` и `DELETE /{id}` — только автор (403 `posts.not_author`, чужой черновик — 404
  `posts.post_not_found`, чужой личный блог — 403 `posts.blog_forbidden`). Демо-данные: по 2 опубликованных поста на
  демо-аккаунт. Публикация с проверками и лимитами — #49, снятие с публикации — #146.
- Адрес поста (E07-02, #50, [ADR-0041](adr/0041-post-address-and-slug.md), FR-POST): `/u/<блог>/<id>-<слаг>` в личном
  блоге и `/<тема>/<id>-<слаг>` в теме — одна страница `frontend/pages/post.vue` (путь и псевдоним в
  `definePageMeta`, последний сегмент обязан начинаться с UUID поста). Пост без заголовка — `<адрес блога>/<id>`.
  Слаг строит `PostSlug.From` (транслитерация, до 60 символов), API отдаёт его полем `slug`. Неверный слаг, UUID блога
  вместо никнейма или чужой блог — 301 на канонический адрес; неизвестный пост — 404 «Пост не найден». Поиск —
  `frontend/api/posts.ts` (`lookUpPost`, `postPath`), в браузере — через `server/routes/bff/post.get.ts`. Страница
  показывает блог, заголовок и блоки; шапка и панель действий — E07-04 (#52), страница темы — E11-01 (#76).
- Отрисовка блоков (E07-03, #51, [ADR-0045](adr/0045-post-block-rendering.md), FR-POST): `components/post/PostBlocks.vue`
  (и `PostList.vue`) рисует документ версии 1 на сервере семантической разметкой: `p`, `h2`/`h3` с якорем
  `h-<id>`, `ul`/`ol` с вложенным списком, `figure` с `blockquote` и `figcaption`, `hr`; неизвестный блок или
  блок с нечитаемыми данными — заглушка `role="note"`. Чтение документа — `utils/post-blocks.ts`, встроенный HTML
  ещё раз проходит белый список ADR-0023 (`utils/inline-html.ts`). Лента пока показывает простой текст
  (`postText`), карточка с блоками — E08-02 (#55).
- Публикация (E05-09, #49, [ADR-0046](adr/0046-publishing-checks.md), FR-EDITOR): `POST /api/v1/posts/{id}/publish`
  (только автор с подтверждённой почтой) прогоняет `PostChecks` — пустой пост (`IBlockDocumentValidator.HasContent`,
  поля блока говорят `HasContent`), заголовок до 120 символов, блок до 64 КБ, пост до 512 КБ — и отвечает `400` с
  ошибками по полям; черновик сохраняется без проверок, правка опубликованного проходит те же. `GET /api/v1/topics`
  (модуль Blogs) — темы для выбора, демо-темы в `TopicsDemoData`. В редакторе (`components/editor/PostEditor.vue`,
  страница `/write`) — необязательный заголовок, выбор темы или «Без темы», «Опубликовать» (выключена, пока нет
  текста; `Ctrl+Enter`); сервер сайта сохраняет черновик и публикует (`server/routes/bff/publish.post.ts`,
  `api/publish.ts`), ошибки полей — на месте, остальное — тост, опубликованный пост открывается на своей странице.
  Не сделано здесь: обложка и «кто может комментировать» (параметры публикации), черновики с автосохранением —
  гость после входа текст не сохраняет.
- Лента «Свежее» (E08-01, #54, [ADR-0043](adr/0043-fresh-feed.md), FR-FEED): `GET /api/v1/feeds/fresh?cursor&limit`
  (`FreshFeedEndpoints` в модуле Posts, тег Feeds) — опубликованные посты от новых к старым по `(published_at, id)`,
  курсор `FreshFeedCursor`, сравнение строк `(published_at, id) < (…)` по частичному индексу `ix_posts_fresh_feed`;
  элемент — `PostView`. Страница `/new` (`frontend/pages/new.vue`, `components/feed/FeedView.vue`,
  `api/feeds.ts`): первая страница рендерится на сервере, следующие — кнопкой «Показать ещё» из браузера; ссылка на
  пост идёт через `/u/<id блога>/…` и редиректит на канонический адрес. Карточка поста — E08-02 (#55), бесконечная
  подгрузка и «Показать новые» — E08-03 (#56), фильтр рейтинга — с реакциями.
- Загрузка медиа (E06-01, [ADR-0030](adr/0030-media-upload-and-object-storage.md), FR-MEDIA): модуль
  `Platform.Modules.Media`, схема `media`, таблица `media_files` (владелец по `owner_account_id`, вид, MIME-тип по
  содержимому, размер, ключ, имя файла). `POST /api/v1/media` (`multipart/form-data`, поле `file`, только вошедшему):
  тип — по сигнатуре первых 64 байт (`FileFormats`: JPEG, PNG, GIF, WebP, MP4, MOV, WebM, MP3, M4A, OGG, WAV, FLAC,
  PDF), неизвестное содержимое — 415 `media_type_unsupported`, расширение другого формата — 415
  `media_extension_mismatch`, размер больше предела вида (`Media:MaxBytes:*`, по умолчанию 20/500/100/50 МБ) — 413
  `media_too_large`, пустой файл — 400. Оригинал — в хранилище за `IObjectStorage` под ключом `originals/` и 128
  случайными битами; `S3ObjectStorage` (AWSSDK.S3, локально MinIO, бакет создаётся сам при `CreateBucket=true`) и фейк
  `InMemoryObjectStorage` (`Media:Storage:Provider=InMemory`, во всех тестах API). На стенде нужны бакет и ключ
  (`MEDIA_STORAGE_*`, DEPLOY.md). Нет здесь: видео-воркера, загрузки по ссылке, удаления
  и уборки объектов-сирот. Контракт `Platform.Modules.Media.Contracts` (`IMediaLibrary`: файл по id — владелец, вид,
  MIME-тип) — для аватара E04-03; на клиенте загружает только форма аватара.
- Раздача изображений (E06-02, #46, [ADR-0037](adr/0037-image-delivery-imgproxy.md), FR-MEDIA): imgproxy читает
  оригиналы из бакета (`IMGPROXY_BASE_URL=s3://<бакет>/`), ссылки строит и подписывает API (`ImgproxyUrls`,
  HMAC-SHA256, `Media:Images:BaseUrl`/`Key`/`Salt`); без подписи — 403. Вариант `ImageVariant`: вырезка по центру
  (`rs:fill`) или по ширине (`rs:fit`), без увеличения, сторона до 4096, формат WebP или AVIF расширением. Контракт
  `IMediaLibrary.ImageUrlsAsync(ids, variant)`; первый потребитель — `Profile.avatarUrl` (128 px WebP). Кэш:
  `Cache-Control: public, max-age=31536000`, `ETag`. На стенде imgproxy — `/img/*` сайта через Caddy, ключ и соль —
  `IMGPROXY_KEY`, `IMGPROXY_SALT` (DEPLOY.md); CDN ставится перед сайтом сменой `Media:Images:BaseUrl`. Нет здесь:
  размытого превью и доминирующего цвета, очистки кэша CDN при удалении.
- Компонент изображения (E06-04, #47, [ADR-0040](adr/0040-image-component.md), FR-MEDIA): `UiImage` — единственный
  способ показать картинку. Место занято по `width`/`height` (атрибуты и `aspect-ratio`) до загрузки, `srcset` с
  `w`-дескрипторами из источников и `sizes` — браузер берёт ширину под плотность экрана, `loading="lazy"` и
  `decoding="async"` (для первого экрана — `eager` с `fetchpriority="high"`). Состояния в `data-state`: `empty`,
  `loading`, `loaded`, `error`; пустая и сломанная картинка — заглушка того же размера. `UiAvatar` показывает
  картинку через `UiImage`. API отдаёт источники картинки контрактом `IMediaLibrary.ImageSourcesAsync(ids, variants)`
  (тип `ImageSource { url, width }`); первый потребитель — `Profile.avatarSources` (квадраты 64, 128 и 192 px WebP),
  шапка блога. Витрина `/ui` показывает все состояния на картинке, которую рисует маршрут сайта
  `/showcase/landscape-{320,640,1280}.svg` из палитры бренда. Нет здесь: `<picture>` с AVIF, размытого превью и
  доминирующего цвета (ни один критерий #47 их не требует).
- Страница блога (E04-02, FR-PROFILE, [ui/blog.md](ui/blog.md)): `/u/<никнейм>`, у блога без никнейма — `/u/<UUID>`;
  `@ник`, другой регистр и UUID блога с никнеймом ведут 301 на один адрес; `id123` никнеймом не считается (404).
  Вкладки — дочерние страницы `/u/<блог>` («Посты») и `/u/<блог>/comments` («Комментарии»), переключаются без
  перезагрузки. Первая отрисовка — на сервере с токеном читателя (гость получает готовую страницу), дальше браузер
  спрашивает `/bff/blog-profile` (сервер сайта идёт в API с токеном). Владелец узнаётся по `registeredAt` в ответе
  (его видит только владелец, ADR-0025) и видит «На сайте с …» и кнопки «Ред.» и «Настройки» (ведут в
  `/settings/blog`, E04-03) и выключенную «Статистика» с пометкой «появится позже». Пункт меню «Профиль» (`/profile`) ведёт через
  `/bff/own-blog` в свой блог. Состояния: скелетон, «Блог не найден» (404), «Не удалось загрузить блог» с
  «Повторить»; вкладки пустые («Здесь пока нет постов», у владельца — «Написать первый пост»; «Пока нет
  комментариев»), пока нет постов (E05) и комментариев (E07). Нет здесь: обложки и аватара (нет в API), счётчиков,
  бейджей, «онлайн», действий посетителя (FR-SUB, FR-MSG, FR-MOD), сортировки и закреплённого поста, личных разделов.
  Код — `frontend/pages/u/`, `frontend/components/blog/`, `frontend/api/profiles.ts`; фейк API отдаёт блоги
  `anna` (владелец — аккаунт фейкового провайдера), `boris`, блог без никнейма и сбой на `broken`.
- Вход по почте (E03-02, [ADR-0022](adr/0022-sign-in-by-password-and-code.md), FR-AUTH): анонимные
  `/api/v1/sign-in` — `POST /password` (204 и cookie провайдера `identity.session`; на неизвестный адрес, неверный
  пароль и неподтверждённую регистрацию одинаково 400 `invalid_credentials`, Argon2 проверяется и по подставному
  хешу), `POST /code` (код на любой адрес, 202 `{ challengeId, resendAfterSeconds }`, таймер 60 секунд, 429
  `code_too_soon`), `POST /code/{challengeId}/confirmation` (204; 400 `confirmation_invalid`). Код — таблица
  `identity.sign_in_codes` (миграция `AddSignInCodes`): одна строка на адрес, хеш кода с `challengeId`, 10 минут,
  5 попыток, один раз; письмо `login-code`. Код создаёт аккаунт без пароля, если подтверждённого с этим адресом нет;
  ожидающая регистрация с этим адресом удаляется, другой аккаунт теряет неподтверждённую почту. То же при
  подтверждении почты (`AccountEmails.ReleaseUnconfirmedAsync`), а `PUT /api/v1/account/email` отвечает
  `email_taken` только на подтверждённый чужой адрес — неподтверждённый ждёт в `email_confirmations`. Лимиты:
  `identity.sign-in` (30 за 15 минут на адрес сети), `identity.sign-in.address` (10 паролей за 15 минут на адрес
  почты), `identity.sign-in-codes` (10 в час на адрес сети), `identity.sign-in-codes.address` (5 писем в час на адрес
  почты); счётчик на адрес почты — `RateLimitCounter` в `Platform.Http` (ключ — хеш адреса). Сайт: страница `/login`
  (`components/auth/SignInForm.vue`, вызовы — `api/sign-in.ts`): пароль или код с таймером, ошибки, загрузка; после
  входа — в `ReturnUrl` провайдера (только его `/connect/authorize`) или на `/auth/sign-in`. Тесты —
  `SignIn/SignInTests` (Mailpit), `Email/AccountEmailTests`, `frontend/tests/auth/SignInForm.spec.ts`, сквозной
  `frontend/e2e/login.spec.ts` на фейковом API. Капча после неудачных входов — E03-12, ниже; восстановление пароля — E03-05, окно входа — E03-04, ниже. В `nuxt dev` провайдер на `:5103` шлёт на
  свой `/login`, которого нет: локально страницу открывают на сайте напрямую.
- Капча и лимиты неудачных входов (E03-12, #32, [ADR-0038](adr/0038-sign-in-captcha.md), FR-AUTH): политики
  identity.sign-in.failures (10 неудачных входов по паролю и вводов кода за 15 минут с адреса сети) и
  identity.sign-in.failures.address (5 неудачных входов по паролю за 15 минут на адрес почты) считают только неудачи
  (RateLimitCounter.IsSpentAsync, IRateCounter.GetAsync). Сверх порога POST /api/v1/sign-in/password и
  POST /api/v1/sign-in/code/{id}/confirmation требуют поле captcha, иначе 400 captcha_required (одинаково для
  любого адреса); задание — GET /api/v1/sign-in/captcha. Поставщик — ICaptcha (Identity), настройка
  Identity:Captcha:Provider: ProofOfWork (своя проверка вычислением в формате ALTCHA, подпись — ключ из
  Identity:EncryptionKey, решение — один раз) или фейк Fake (только Development и Test; его берут тесты Identity).
  Сайт: components/auth/CaptchaCheck.vue в SignInForm, перебор — pi/captcha.ts. Тесты — SignIn/SignInTests,
  SignIn/ProofOfWorkCaptchaTests, rontend/tests/auth/SignInForm.spec.ts, 	ests/auth/captcha.spec.ts, сквозной
  rontend/e2e/login.spec.ts. Не сделано: капча при регистрации и при вводе кода восстановления пароля; выбор
  поставщика — вопрос владельцу (ADR-0038).
- Окно входа и регистрации (E03-04, #30, [ADR-0032](adr/0032-auth-window-and-guest-actions.md), FR-AUTH):
  `components/auth/AuthWindow.vue` — `UiModal` с формами входа и регистрации (`embedded`: без своих заголовков) и
  ссылкой между ними; на десктопе модальное окно, уже 640 px — шторка на весь экран (`UiModal fullscreen`). Одно окно
  на страницу — `AuthWindowHost` в `app.vue`, состояние `useAuthWindow()`. Кнопка с действием гостя вызывает
  `useRequireAccount()(action, run)`: вошедшему — сразу, гостю — окно; после входа действие и адрес страницы лежат в
  `sessionStorage` (`utils/pending-action.ts`, 10 минут), сайт входит через `/auth/sign-in?returnTo=<страница>`, и
  `onSignedInAgain(action, run)` повторяет действие один раз. Ошибка пароля — у поля «Пароль». Реакций ещё нет, поэтому
  механизм показывает кнопка «Нравится» на `/ui`; реакции (E10, #72) и подписки подключают его сами. Доступность
  форм проверяет axe (`@axe-core/playwright`, WCAG 2.1 AA) в `frontend/e2e/auth-window.spec.ts`; тесты —
  `tests/auth/AuthWindow.spec.ts`, `tests/auth/pending-action.spec.ts`. «Войти» в шапке по-прежнему ведёт на
  `/auth/sign-in`, а страницы `/login` и `/register` остаются для провайдера (`Identity:SignInPath`).
- Восстановление и смена пароля (E03-05, #31, [ADR-0036](adr/0036-password-reset-and-change.md), FR-AUTH): анонимные
  `POST /api/v1/password-reset` (всегда 202 `{ resetId, resendAfterSeconds }`, таймер 60 секунд, 429 `code_too_soon`;
  письмо `password-reset` — только на подтверждённую почту аккаунта) и `POST /api/v1/password-reset/{resetId}/confirmation`
  `{ code, password }` (204 и cookie провайдера; 400 `confirmation_invalid`; 400 `validation_failed` по полю `password`
  попытку не тратит). Код — таблица `identity.password_resets` (миграция `AddPasswordResets`): строка на адрес, хеш
  кода с `resetId`, 15 минут, 5 попыток, один раз. Сброс отзывает все токены и авторизации OpenIddict аккаунта и
  меняет `accounts.session_stamp`; cookie провайдера несёт метку (`identity_stamp`), и `/connect/authorize` не
  принимает cookie со старой меткой. `PUT /api/v1/account/password` `{ currentPassword, newPassword }` — вошедшему
  (400 `current_password_invalid`, 400 `validation_failed` по `newPassword`), сессии остаются. Оба пути шлют письмо
  `password-changed`. Лимиты — политики входа (`identity.sign-in-codes`, `identity.sign-in-codes.address`,
  `identity.sign-in`). Сайт: «Забыли пароль?» в форме входа (`/login` и окно входа) открывает
  `components/auth/PasswordResetForm.vue` (вызовы — `api/password.ts`), после сброса — `/auth/sign-in`; смена —
  `/settings/account` (`components/settings/PasswordChangeForm.vue`, через `server/routes/bff/password.put.ts`).
  Тесты — `Passwords/PasswordTests`, `Passwords/PasswordResetTests` (Identity), снимки писем в `Platform.Mail.Tests`,
  `frontend/tests/auth/PasswordResetForm.spec.ts`, `tests/settings/PasswordChangeForm.spec.ts`, сквозной
  `frontend/e2e/password.spec.ts`. Не сделано: страница `/settings` с разделами (пункт меню «Настройки» ведёт на неё),
  остальные части «Аккаунт и вход» — почта, провайдеры, устройства и «Выйти со всех устройств»; мгновенный отзыв уже
  выданного токена доступа (живёт свои 15 минут, ADR-0024).
- Вход по OpenID Connect (E18-02, [ADR-0018](adr/0018-openid-connect-provider.md), FR-ID-2): API подключает модуль
  Identity (`AddIdentityModule`, `MapIdentityEndpoints`; теперь API нужен `ConnectionStrings:Platform`). Identity —
  провайдер на OpenIddict 7: `/.well-known/openid-configuration`, `/.well-known/jwks`, `/connect/authorize`,
  `/connect/token`; только код авторизации с PKCE S256 (код 2 минуты, один раз; токены 15 минут), `sub` = `account_id`,
  токен доступа — JWT `at+jwt` с аудиторией `platform-api`. Таблицы OpenIddict — в схеме `identity`
  (`oidc_applications`, `oidc_authorizations`, `oidc_scopes`, `oidc_tokens`, миграция `AddOpenIddict`). Клиенты `web` и
  `admin` — публичные, из `Identity:Clients:*:RedirectUris`, точный адрес возврата; API пишет их в базу при старте
  (`OidcClientSync`). `/connect/authorize` берёт человека из cookie провайдера `identity.session`, без неё отправляет в
  окно входа (`Identity:SignInPath`, `/login`), с `prompt=none` отвечает `login_required`. Войти можно паролем или
  кодом (E03-02, выше) — на странице `/login` или в окне входа (E03-04).
  API проверяет `Authorization: Bearer` (OpenIddict validation): без `Auth:Authority` — своим сервером, с внешним
  адресом — по его discovery; `HttpContext.User` несёт `sub`, лимиты частоты считаются по аккаунту. Ключи подписи и
  шифрования — `Identity:SigningKey`, `Identity:EncryptionKey` (секреты стенда; в Development и Test создаются при
  старте), [CONFIG.md](CONFIG.md#вход-модуль-identity-в-api). Сайт — клиент через свой сервер (`openid-client` 6.8):
  `/auth/sign-in` и `/auth/callback` (`frontend/server/routes/auth/`), токены — на сервере Nuxt (с E03-03 — в Redis), браузеру —
  cookie `auth.session` (HttpOnly, SameSite=Lax), странице — только `useAccount().value.signedIn`; «Войти» ведёт на
  `/auth/sign-in`, вошедший видит меню аккаунта с временным именем «Аккаунт» (профиль — #33); клиент API на сервере
  добавляет токен. Стенд: Caddy отдаёт адреса провайдера API, выкладка пишет `SITE_URL`, нужны секреты
  `IDENTITY_SIGNING_KEY`, `IDENTITY_ENCRYPTION_KEY`, `SESSION_PASSWORD` ([DEPLOY.md](DEPLOY.md#что-нужно-от-владельца)).
  Тесты — `backend/tests/Platform.Modules.Identity.IntegrationTests/SignIn` (поток целиком, негативные пути, issuer из
  настройки, внешний провайдер-фейк, лимит по аккаунту, ключи и клиенты из настроек), `frontend/tests/auth-oidc.spec.ts`,
  сквозной `frontend/e2e/sign-in.spec.ts` на фейковом провайдере `frontend/e2e/fake-oidc-provider.mjs`. Не сделано
  здесь: вход в админку (клиент `admin` только зарегистрирован). Сессии и выход — ниже
  (E03-03).
- Сессии сайта и выход (E03-03, [ADR-0024](adr/0024-session-refresh-and-sign-out.md), FR-AUTH): провайдер выдаёт
  refresh-токен по `offline_access` (30 дней, каждая ротация продлевает; окно повтора 10 секунд,
  `IdentityModule.RefreshTokenLifetime`, `RefreshTokenReuseLeeway`); повтор погашенного токена после окна отзывает
  всю цепочку (поведение OpenIddict 7). Новые адреса — `/connect/revoke` и `/connect/endsession` (удаляет
  `identity.session`, возвращает на корень сайта клиента — `PostLogoutRedirectUris` выводятся из адресов возврата,
  новой настройки нет). Сайт: `server/utils/session-store.ts` (`SessionStore`: обновление за 30 секунд до истечения,
  одно на сессию в процессе, перечитывание перед обновлением, `invalid_grant` завершает сессию), хранилище —
  `server/plugins/auth-storage.ts` (Redis при `NUXT_SESSION_REDIS_URL`/`NUXT_SESSION_REDIS_PASSWORD`, ключи
  `site:auth-sessions:*` с TTL; иначе память), `POST /auth/sign-out` (отзыв, удаление сессии и cookie, переход на
  end-session провайдера), «Выйти» в меню аккаунта шапки и на экране «Меню» (`signOut()` в `composables/useAccount.ts`),
  билет хаба обновляет токен и повторяет запрос при 401 API. Стенд: `frontend` получает Redis стенда
  (`deploy/compose.stage.yaml`). Тесты — `SignIn/SessionTokenTests` (12: обновление истёкшего токена, ротация и
  продление, срок жизни, отзыв цепочки при повторе и окно, чужая цепочка не страдает, отзыв, чужой клиент, удалённый
  аккаунт, выход у провайдера и чужой адрес возврата), `frontend/tests/auth-session.spec.ts` (15), сквозной
  `frontend/e2e/session.spec.ts` (фейковый провайдер выдаёт токены на 3 секунды, фейковый API отвергает истёкшие).
  Не сделано: принудительное закрытие сервером открытых соединений хаба при выходе (другие вкладки перестают
  переподключаться, вкладка выхода рвёт соединение сама) — вместе с модерацией; мгновенный отзыв уже выданного токена
  доступа (JWT живёт свои 15 минут, ADR-0024).
- Канал реального времени (E01-11, [ADR-0019](adr/0019-realtime-channel.md)): библиотека `backend/src/Realtime`
  (`Platform.Realtime`, `AddPlatformRealtime`, `MapRealtime`) — хаб SignalR `/hubs/realtime` только для вошедших.
  Темы — группы `account:<id>` (соединение входит само) и `post:<id>` (`Subscribe`/`Unsubscribe`); право подписки —
  `IRealtimeTopicPolicy` вида темы (на чужой аккаунт и неизвестный вид — `topic_refused`; на пост пока любой вошедший,
  проверку видимости добавит модуль Posts). Модули шлют события через `IRealtimePublisher`, клиент получает `receive`
  с `{ topic, type, data }`. Браузер подключается по билету: `POST /auth/realtime-ticket` на сервере сайта →
  `POST /api/v1/realtime/tickets` с токеном человека (первый путь в снимке OpenAPI); билет — 60 секунд, в Redis под
  SHA-256, открывает только хаб (в адресе `access_token` или заголовком `Bearer`); без билета хаб принимает токен
  провайдера. Redis backplane включается вместе с кэшем (`Cache:Provider=Redis`, каналы `platform:realtime`). Клиент —
  `frontend/realtime/channel.ts`, `plugins/realtime.client.ts` (`@microsoft/signalr` 10, загружается только у
  вошедшего), `useRealtimeTopic`; переподключение без конца (0, 2, 5, 10, затем каждые 30 секунд) с новым билетом и
  повторной подпиской, прекращается при выходе. Стенд: Caddy отдаёт `/hubs/*` API; `nuxt dev` проксирует `/hubs`.
  Тесты — `RealtimeIntegrationTests` (`Platform.Api.IntegrationTests`, Redis: своё событие и не чужое, подписка на
  пост, отказ в чужом аккаунте и неизвестной теме, без билета, поддельный и просроченный билет, билет заголовком,
  билет не покупает билет, backplane между двумя экземплярами), `RealtimeSignInTests` (настоящие токены провайдера:
  билет за токен, просроченный токен), `frontend/tests/realtime.spec.ts`, сквозной `frontend/e2e/realtime.spec.ts` на
  фейковом API `frontend/e2e/fake-api.mjs` (long polling: подключение по билету, переподключение после обрыва с новым
  билетом). Не сделано: события продукта (их добавят комментарии, уведомления и счётчики), отправка из воркеров,
  принудительное закрытие соединений сервером при выходе и блокировке (модерация; при выходе вкладка рвёт соединение
  сама, ADR-0024).
- Тесты границ модулей (E01-18): `backend/tests/Platform.Architecture.Tests` по именам сборок
  (`Platform.Modules.<Модуль>` / `.Contracts`) запрещает ссылки на внутренности чужого модуля, ссылки
  контрактов и общих сборок на модули, Telegram вне `Platform.Telegram.Gateway`, библиотеки учётных данных вне
  модуля `Identity`, чужую схему у `DbContext`. Проверка — по метаданным сборок, без сторонних библиотек.
  Нарушения ловятся на модулях-образцах `backend/tests/ArchitectureFixtures`. Правила —
  [ARCHITECTURE.md](ARCHITECTURE.md#границы-модулей).
- Демо-данные и фабрики объектов (E01-17): библиотека `backend/src/Seeding` (`Platform.Seeding`) — `SeedData`
  (зерно и `TimeProvider`; идентификаторы `Id(kind, n)` и случайные последовательности выводятся из зерна),
  `ObjectFactory<T>`, `IDemoSeeder`, `AddMissingAsync` (идемпотентная запись по идентификаторам); команда
  `dotnet run --project backend/tools/Seed` — миграции, затем демо-сидеры модулей из `SeedCatalog`; в `Staging` и
  `Production` отказывается без `Seed:AllowProtectedEnvironment=true`. Тестам — `TestData.Create()`,
  `IntegrationApiFactory.Data`, `SaveAsync<TContext>`, `SeedDemoAsync()`. Фабрики живут в коде модуля (не в
  `Platform.Testing`), так их использует и сидер. Образец — `NoteFactory` и `NotesDemoSeeder` модуля заметок.
  Каталог сидера пока пуст: модулей продукта нет; пользователи, блоги, темы, посты и комментарии появятся в
  E01-19 (#287) по мере модулей. Правила — [ADR-0009](adr/0009-demo-data-and-factories.md).
- Образы и выкладка (E01-03): `scripts/images.ps1` собирает пять образов — `api`, `workers`, `migrator`
  (`backend/Dockerfile`, три цели), `frontend` (сервер Nuxt) и `admin` (Vite за nginx без root); базовые образы
  закреплены по дайджесту. Workflow `images` на каждом PR собирает их и прогоняет смоук выкладки, после слияния в
  `main` публикует в GHCR (`ghcr.io/<владелец>/<репозиторий>/<образ>`, теги `sha-<коммит>` и `latest`; релиз
  release-please добавляет `<версия>` и `<мажор>.<минор>`). Стенд stage — `deploy/compose.stage.yaml` (образы,
  PostgreSQL, Redis, RabbitMQ, Caddy с TLS; хосты в окружении `Staging`), выкладка — `scripts/deploy.ps1`: мигратор
  новой версии, затем замена контейнеров с ожиданием healthcheck; откат — выкладка прежнего тега. Workflow
  `deploy` выкладывает тег вручную на `stage` или `production` (по SSH к Docker сервера) и вызывается после
  слияния, когда переменная `DEPLOY_STAGE` = `true`; без неё выкладка пропускается, и секреты хостинга CI не нужны.
  `scripts/deploy-test.ps1` статически проверяет Dockerfile, compose-файл и workflow (шаг `deploy config`), а смоуком
  выкладывает, обновляет, роняет миграцию и откатывает стенд на Docker раннера (задание `images / build`; локально
  — при `VERIFY_IMAGES=1`). Инструкция и список того, что нужно от владельца, — [DEPLOY.md](DEPLOY.md). Сервер,
  домен и значения окружений GitHub пока не заданы: stage не подключён.
- Временный бренд (E02-01): `brand/brand.json` — единственное место названия (kitabber), знака, палитры
  светлой и тёмной темы и набора иконок (Lucide, `lucide-vue-next`, ISC). Оба клиента показывают в шапке
  `BrandLogo`, заголовок и значок вкладки берутся оттуда же; витрина бренда — страница `/brand` сайта. Образы
  получают `brand/` именованным контекстом сборки. Vitest с Vue Test Utils и happy-dom подключён в обоих
  клиентах (`pnpm test`, шаг verify «unit tests»).
- Дизайн-токены (E02-02, [ADR-0012](adr/0012-design-tokens.md)): `brand/tokens.json` плюс палитра `brand.json` →
  генератор `brand/tokens.ts` → закоммиченный `brand/tokens.css` (CSS-переменные `--color-*`, `--font-*`,
  `--space-*`, `--radius-*`, `--size-*`), подключён в обоих клиентах. Светлая и тёмная палитры: атрибут
  `data-theme` или системная тема; переключателя темы пока нет (FR-UI). Типографика для лонгридов: 18 px, 1,7,
  строка до 40rem. stylelint (`pnpm run lint`) запрещает в стилях компонентов сырые цвета и размеры; витрина
  токенов — страница `/tokens` сайта. `BrandLogo` красится токенами и следует теме страницы.

- Спецификации ключевых экранов (E02-03, [docs/ui](ui/README.md)): лента, пост, редактор, блог, тема, комментарии,
  уведомления и настройки — схема раскладки на 1440 и 390 px, размеры именами токенов, состояния «Пусто»,
  «Загрузка», «Ошибка» и ссылки на FR. Решения, которых нет в ТЗ (адреса страниц, пороги глубины веток, раскладка
  настроек, высоты шапки и обложки), помечены «предложение, требует подтверждения». Шаг verify «ui specs»
  (`scripts/ui-specs-test.ps1`, случай в самопроверке) проверяет файлы, разделы, FR, токены и якоря ссылок в `docs/`.
- Базовые компоненты (E02-04, [ADR-0013](adr/0013-ui-components.md)): `frontend/components/ui/` — `UiButton`,
  `UiField`, `UiMenu`, `UiTabs`, `UiModal` (уже 640 px — шторка снизу), `UiSheet`, `UiToast` с `UiToaster` и
  `useToast()` (тосты подключены в `app.vue`), `UiAvatar`. Клавиатура и ARIA по шаблонам WAI-ARIA: ловушка фокуса
  и Esc в диалогах, стрелки в меню и вкладках, живые области тостов. Витрина со всеми состояниями и переключателем
  темы — страница `/ui` сайта. Новые токены `--color-overlay` и `--color-shadow`. Админка компоненты пока не
  использует.
- Каркас десктопа (E02-05): `frontend/components/shell/` — `ShellLayout` (ссылка «Перейти к содержимому», шапка,
  сетка из колонок `--size-menu`, `--size-feed`, `--size-aside` с промежутком `--space-6`; от 1240 px три колонки,
  925–1239 px без правой, 640–924 px одна колонка без левого меню, до 639 px во всю ширину; левое меню и правая
  колонка прилипают под шапкой), `ShellHeader` (знак, поиск — пока только событие `search`, «Написать» → `/write`,
  колокольчик → `/notifications` и меню аватара для вошедшего, «Войти» → `/login` для гостя; на телефоне — только
  знак и поиск), `ShellNav` (ленты, «Сообщения», «Рейтинг», темы с «Показать все», «О проекте», «Правила»,
  переключатель темы; текущий экран — `aria-current="page"`), `ShellAside` с двумя `ShellWidget` (состояния «пусто»,
  «загрузка», «ошибка» с «Повторить», «готово»), `ShellThemeSwitch` и `composables/useTheme.ts` (светлая, тёмная,
  системная; выбор хранится в `localStorage` этого браузера). Пункты и адреса меню — один список
  `frontend/utils/navigation.ts` (адреса — предложение из docs/ui). Раскладка `layouts/default.vue` оборачивает все
  страницы, витрины `/brand`, `/tokens`, `/ui` — раскладка `bare`. Каркас телефона (E02-06) — в слоте `mobile` у `ShellLayout`.
  Страниц лент, сообщений, тем и служебных пока нет: ссылки меню ведут на 404 (страницы 404 — #64).
- Каркас телефона (E02-06): `ShellTabBar` — нижняя панель «Лента», «Рейтинг», «Уведомления», «Сообщения», «Меню»
  ниже 925 px (на 640–924 px тоже: левого меню там нет), со счётчиками непрочитанного (проп `counts`, данных пока
  нет), текущей вкладкой `aria-current` и прокруткой наверх по повторному нажатию; `ShellWriteButton` — плавающая
  «Написать» ниже 640 px (на 640–924 px «Написать» остаётся в шапке); `ShellMenuScreen` на странице `/menu` —
  профиль или «Войти», черновики, закладки, настройки, темы, тема оформления, служебные ссылки, «Выйти». Безопасные
  зоны: `viewport-fit=cover`, отступы `env(safe-area-inset-*)` у шапки, панели и кнопки; вкладки высотой
  `--size-tab-bar` (56 px), нажимаемые строки не ниже `--size-touch` (44 px) — новые токены. Тосты ниже 925 px
  поднимаются над панелью. Пункты панели и меню аккаунта — в том же `frontend/utils/navigation.ts`.
- Тестовый каркас клиентов (E01-16): Vitest пишет JUnit в `artifacts/test-results/<клиент>/`; Playwright
  (`@playwright/test`, только Chromium) — `frontend/e2e` и `admin/e2e`, `pnpm e2e`, проекты `desktop` (1440×900) и
  `mobile` (390×844, сенсорный ввод), каждый тест в обоих. Тесты идут против продакшен-сборки, которую verify
  собирает шагом «build» (`node .output/server/index.mjs` и `vite preview` в `webServer`). JUnit — в
  `artifacts/test-results/<клиент>-e2e/`, при падении трасса и снимок экрана — в `…-e2e/failures` (артефакт CI
  `test-results`). Фикстура `frontend/e2e/fixtures.ts` роняет тест на ошибке скрипта или консоли; `makeScrollable` меняет DOM только после гидратации (`waitForHydration`, #300: иначе несовпадение гидратации сбрасывало прокрутку, тест «прокрутка наверх» падал в профиле desktop). Сквозные проверки
  каркаса из #15 и #16 (ширины 220/640/320, скрытие колонок на 1100 и 800 px, прилипание, нижняя панель, плавающая
  «Написать», экран «Меню», прокрутка наверх, безопасные зоны через эмуляцию Chromium) и витрины `/brand`, `/tokens`,
  `/ui` в обеих темах (у каждой ровно один `h1`, у `/brand` разделы тем — `h2`); в админке — дымовой тест. Снимков экрана для сравнения нет. Браузеры в CI кэшируются
  (`~/.cache/ms-playwright`).

Функций продукта пока нет: сайт — пустой каркас десктопа и телефона без данных, админка — пустая страница с логотипом, у API есть служебные `/health/live`, `/health/ready`, `/metrics`, адреса провайдера входа (E18-02) и регистрация по почте (E03-01, страница `/register`), но окна входа ещё нет.

## Базовая линия тестов

| Набор | Число тестов |
| --- | --- |
| Сервер, модульные | 191 (из них 12 — пустой пост E05-09 в `Platform.Modules.Editor.Tests` (`HasContent`); из них 2 — снимки писем `password-reset` и `password-changed` E03-05 в `Platform.Mail.Tests`; `Platform.Modules.Editor.Tests`: 98 — из них 8 — вложенность списка E05-04: уровни пунктов по порядку, список без `levels` — плоский, 6 видов плохих уровней с местом ошибки; схемы блоков E05-08: все блоки версии 1 и канонический вид, 25 видов плохого блока и документа с местом ошибки, повтор id, неподдерживаемая версия, предел числа ошибок, проверка документа схемами его версии, 35 векторов XSS, разрешённое форматирование, текст и почтовые ссылки, API: очищенный документ, 401 без входа, 400 с путями ошибок; `Platform.Mail.Tests`: 15 — шаблоны писем E01-12 и письмо подтверждения почты E18-03: снимок каждого шаблона, экранирование в HTML, незаполненное поле, неизвестный шаблон, выбор культуры с откатом на русский, название из `brand/brand.json` и его отсутствие в шаблонах; `Platform.Architecture.Tests`: 46 — границы модулей на сборках платформы, модули-образцы с нарушениями, разрешённые и запрещённые ссылки, схемы; из них 15 — FR-ID-1 E18-01: почта, пароли и внешние идентификаторы только в `identity`, нет внешних ключей между схемами, таблицы только в своей схеме, нарочно нарушающий контекст, распознавание имён столбцов; `Platform.Api.Tests`: 13 — курсорная пагинация и часы; `Platform.Configuration.Tests`: 12, проверка настроек и файлы секретов; `Platform.Seeding.Tests`: 5, зерно, идентификаторы и фабрики) |
| Сервер, интеграционные | 454 (из них 13 — публикация E05-09 в `PublishApiTests` и `TopicApiTests`: проверки, права, темы; из них 9 — лента «Свежее» E08-01 в `FreshFeedApiTests`: страницы по курсору без пропусков и дублей при новых постах, одинаковое время публикации, черновики и удалённые вне ленты, неверный курсор и лимит; из них 16 — слаг адреса поста E07-02 в `PostSlugTests`: транслитерация, пост без слага, обрезка длинного слага, слаг следует за заголовком; из них 5 — капча E03-12: порог неудач на адрес почты и на адрес сети, одинаковый ответ `captcha_required`, вход с решением фейка, ввод кода, а также `ProofOfWorkCaptchaTests` — решение проходит один раз, чужая подпись, неверное число, срок; `Platform.Modules.Identity.IntegrationTests`: 10 — пароль E03-05: сброс закрывает прежнюю cookie провайдера и refresh-токен, новый пароль входит, старый нет, письма с кодом и о смене; код одноразовый и истекает через 15 минут; 5 неверных кодов тратят код, слабый пароль попытку не тратит; ответ одинаков для любого адреса, письмо только аккаунту; неподтверждённой регистрации письма нет; смена с текущим паролем, 401, неверный текущий, слабый новый, сессии остаются, письмо; аккаунт без пароля задаёт его сбросом; правила кода и метки сессий без базы; `Platform.Modules.Blogs.IntegrationTests`: 8 — профиль E04-03: правка имени, описания и аватара видна всем сразу (с подписанной ссылкой на аватар E06-02), профиль без аватара — без ссылки, пустые поля очищают, пределы в графемах (25 эмодзи — можно, 26 — нет) без частичного сохранения, аватар — только своё изображение (чужое, PDF и несуществующее — 400), без входа — 401, аккаунт без блога получает его, правило `SetAvatar`; `Platform.Modules.Media.IntegrationTests`: 65 — раздача E06-02: 13 без контейнера на ссылки imgproxy (подпись по примеру документации imgproxy, путь, другой ключ или соль, 5 неверных сторон, настройки и 4 неверных), 5 на imgproxy и MinIO в Testcontainers (квадрат WebP, ширина AVIF, без увеличения, 4 ссылки без верной подписи — 403, кэш на год и ETag → 304); загрузка E06-01: 34 без базы (14 форматов по сигнатуре, 3 чужих содержимых, 4 имени с верным расширением и без него, 5 подменённых расширений, пустой файл, пределы по видам, пределы из настроек и 3 неверных значения, случайные ключи, владелец и имя файла), 11 через API на PostgreSQL с фейком хранилища (оригинал под случайным ключом с владельцем и типом по содержимому, два ключа одному файлу, 4 подмены — 415 и ничего не сохранено, предел изображения из настройки — 413, PDF со своим пределом, без входа 401, пустой файл и запрос без файла — 400), 2 на MinIO в Testcontainers (запись, чтение, удаление, бакет при первой записи, общий бакет); `Platform.Modules.Posts.IntegrationTests`: 28 — пост E07-01: 12 без базы на правила (черновик native без источника, пустой заголовок — 3, источник Telegram, источник без ссылки и без сообщения, автор, блог и тело обязательны, публикация один раз, мягкое удаление и неизменность удалённого, кто видит, кто меняет, кто пишет в личный блог и тему), 16 через API на PostgreSQL (черновик в личном блоге с очищенным телом, пост в теме, чужой личный блог, неизвестный блог, ошибки схемы и нет тела, создание без входа, черновик только автору, опубликованный всем, происхождение и источник, уникальный источник в базе, правка автором, правка без блога, права на правку, мягкое удаление, права на удаление, демо-посты один раз и принимаются валидатором); `Platform.Modules.Blogs.IntegrationTests`: 30 — блоги E04-01: 18 без базы на правила (личный блог, 25 и 160 символов в графемах, пробелы, 12 форм никнейма), 11 через API на PostgreSQL (профиль по id и никнейму, дата регистрации только владельцу, 404, 3 ошибки валидации никнейма, `/me` без входа и с созданием блога один раз, уникальные индексы), 1 на настоящем хосте воркеров (событие создаёт один блог, повтор и дубль ничего не меняют); `Platform.Modules.Identity.IntegrationTests`: 150, из них 12 — сессии E03-03: обновление истёкшего токена доступа, ротация и продление, срок жизни refresh-токена, повтор погашенного токена отзывает цепочку, чужая цепочка цела, окно одновременных обновлений, отзыв, чужой клиент, удалённый аккаунт, выход у провайдера с просроченным `id_token_hint`, чужой адрес возврата; 2 — `AccountActivated` E04-01 (подтверждённая регистрация объявляется один раз, неподтверждённая — нет; аккаунт по коду объявляется); 13 — вход E03-02 через API с письмами в Mailpit: пароль до токена доступа, одинаковый ответ на неверный пароль, неизвестный адрес, неподтверждённую регистрацию и неверный ввод, код входит в аккаунт и один раз, одинаковый ответ на запрос кода и таймер для любого адреса и новый аккаунт по коду, код отнимает адрес у неподтверждённой регистрации и у аккаунта, вписавшего его, неверный, чужой, израсходованный и просроченный код, 3 ошибки валидации, лимит паролей на адрес почты, лимит писем на адрес почты, лимит запросов кода на адрес сети; 27 — регистрация E03-01: 14 без базы на пароль (длина, Argon2id и своя соль, проверка только своего пароля, отказ на подделанной строке) и 13 через API с письмами в Mailpit (регистрация, код из Mailpit, вход до токена доступа; неверный и просроченный код; таймер повторной отправки; 5 ошибок валидации; занятый адрес; замена чужой неподтверждённой регистрации; подтверждённый аккаунт не входит по идентификатору и коду; лимит на адрес сети); 52 — почта E18-03: 36 без базы на нормализацию (регистр, точки Gmail, `+метки`, IDN, NFC, неверные адреса) и 17 через API — код из письма подтверждает почту и сохраняет дату, неподтверждённый не публикует (403 `email_not_confirmed`), дубль после нормализации (409 только на подтверждённый, неподтверждённый ждёт), первый введённый код отнимает неподтверждённый адрес, уникальный индекс в базе, неподтверждённая почта не ключ слияния, просроченный, чужой и израсходованный код, подтверждённая почта остаётся до подтверждения новой, неверный адрес, аноним, лимит писем; 3 — хаб реального времени E01-11 с токенами провайдера: билет за токен открывает хаб, просроченный токен не даёт ни билета, ни хаба; 34 — вход E18-02: discovery и ключи, поток с PKCE и токены с `sub` = `account_id`, API видит аккаунт, окно входа и `prompt=none`, удалённый аккаунт, чужой клиент и адрес возврата, нет PKCE и `plain`, неверный верификатор, повтор, чужой и просроченный код, просроченный и поддельный токен, лимит по аккаунту, issuer из настройки, внешний провайдер по `Auth:Authority`, 10 без базы — ключи и клиенты из настроек; 6 — модуль Identity E18-01: UUID v7 по часам, порядок идентификаторов, проверка версии, миграция только в схему `identity`, запись и чтение аккаунта, демо-аккаунты один раз с идентификаторами `Id("account", n)`; `Platform.Mail.Tests`: 6 — доставка E01-12: письмо через очередь воркеров, повтор после двух сбоев, неверный адрес у вызывающего, 3 на Mailpit в Testcontainers — SMTP из настроек с входом и без, имя отправителя; `Platform.Api.Tests`: 30 в процессе, без Docker, из них 8 — наблюдаемость E01-09: пробы, метрики Prometheus, трасса из `traceparent`, ошибка в логе и спане под trace id, пробы не трассируются, JSON-логи; `Platform.Configuration.Tests`: 15, настоящие хосты и сверка с репозиторием, из них 5 — телеметрия воркеров, 3 — настройки почты воркеров; `Platform.Persistence.IntegrationTests`: 9 на PostgreSQL (миграции, сброс Respawn; E18-01 — в базе реальных модулей нет столбцов учётных данных вне `identity` и внешних ключей между схемами, и запросы это ловят), 7 без базы; `Platform.Messaging.IntegrationTests`: 6 на контейнерах, 11 без Docker; `Platform.Seeding.Tests`: 5, команда сидера без базы (окружения, настройки); `Platform.Api.IntegrationTests`: 31 (прежняя запись 17 отставала на 1), из них 13 — канал реального времени E01-11 на Redis: событие своей темы и не чужой, подписка и отписка на пост, отказ в чужом аккаунте и неизвестных темах (5), без билета и с поддельным, просроченный билет, билет заголовком `Bearer`, билет только за токен, backplane между двумя экземплярами, новое соединение снова в теме аккаунта; API на PostgreSQL и Redis и сидер на PostgreSQL, из них 2 — трасса до спана базы и готовность) |
| Клиент, компонентные | 550 (`frontend`: 538; из них 15 — публикация E05-09 в `tests/editor/PostEditor.spec.ts` и `tests/post/publish.spec.ts`; из них 16 — отрисовка блоков E07-03 в `tests/post/PostBlocks.spec.ts`: снимки каждого типа блока, заглушка, разметка, белый список; из них 9 — свой блок «Текст» #331 в `tests/editor/text.spec.ts`: плоский список тегов инструментов и `br`, его не меняет Editor.js, превращение по полю `text`, вставка `P`, отрисовка, плейсхолдер, сохранение и слияние с разметкой, `preserveBlank`, вставка, очистка пустого абзаца; из них 12 — лента «Свежее» E08-01 в `tests/feed/`: состояния ленты, следующая страница, курсор и адрес поста из ленты; из них 8 — вставка списков E05-02: списки Word без маркеров и с уровнем, список Google Docs, вложенные списки веб-страницы, абзацы в пункте, вид по маркеру Word, `onPaste` и `pasteConfig` блока «Список»; из них 11 — адрес поста E07-02 в `tests/post/posts.spec.ts`: id из сегмента, одна схема для блога и темы, поиск поста и его блога, состояния страницы поста; из них 12 — компонент изображения E06-04: `srcset` по ширинам и `sizes`, ссылка вместо источников, место по пропорциям, ленивая загрузка и `eager`, состояния «загрузка», «загружено», «пусто», «ошибка», картинка, загруженная или сломанная до гидратации, крошечная картинка плотного экрана, ленивая картинка после гидратации, источники аватара под его размер, `srcset` аватара в шапке блога, раздел витрины; из них 16 — меню блоков и перестановка E05-07: место падения блока по серединам блоков, клавиши Ctrl+Shift+↑/↓ и Cmd, перенос текущего блока клавишами с пределами у краёв, чужие клавиши не трогает, перетаскивание за «⋮⋮» с метками «до» и «после», каретка в перенесённом блоке, клик после перетаскивания не открывает меню, короткое нажатие и отмена без переноса, только с ручки; поиск шторки на телефоне — поиск по части названия без учёта регистра, скрытие и «Ничего не найдено», поле в открытом меню без фокуса, `Enter` добавляет первый найденный, сброс при закрытии, на десктопе не вмешивается; из них 5 — капча E03-12: форма просит капчу, ошибка и загрузка проверки, решение тратится одной попыткой, капча при вводе кода, перебор `api/captcha.ts`; из них 1 — аватар в шапке блога по ссылке E06-02; из них 16 — пароль E03-05: форма сброса — почта из формы входа, код и новый пароль, загрузка, короткий пароль без вызова API, неверный код и правило пароля у полей, новый код по таймеру, ошибки адреса, лимита и связи, возврат ко входу, «Забыли пароль?» только с вызовами сброса и вход после сброса; форма смены — пусто, загрузка и успех, короткий пароль без вызова, неверный текущий у поля, правило API у нового, 401, 429 и сбой; из них 16 — профиль E04-03: счёт символов по графемам и пределы 25 и 160, PUT `/profiles/me` и его исходы, загрузка аватара `multipart/form-data` и отказы 415, 413, 401; из них 11 — окно входа E03-04: модальное окно на десктопе и шторка на весь экран на телефоне, формы без своих заголовков, переход между входом и регистрацией, `signed-in` после пароля и после кода регистрации, закрытие; отложенное действие выполняется один раз на своей странице, не трогает чужие, забывается на другой странице и через 10 минут, переживает битую запись и недоступное хранилище; 25 — цитата и разделитель E05-05: пункты меню, два поля цитаты с плейсхолдерами, текст и подпись хранятся раздельно с разметкой, поле с одним `<br>` пусто, `Enter` в тексте ведёт в подпись, `Shift+Enter` — перенос, `Enter` в подписи даёт текст после цитаты, только чтение, превращение с подписью через тире, документ схемы; разделитель пуст, не редактируется, `role="separator"`, щелчок выделяет, `Backspace` и `Delete` удаляют выделенный и ставят каретку рядом (4 случая), без выделения и с модификатором клавиши не трогает, обработчик снимается, каретка после добавленного разделителя (4 случая); 26 — списки E05-04: пункты меню, `ul`/`ol` с уровнями, пустой список с плейсхолдером, сохранение и выправление уровней, `Tab` и `Shift+Tab` с пределом и кареткой, `Enter` делит пункт, поднимает пустой вложенный, завершает список в конце и в середине, превращает пустой список в текст, `Backspace` поднимает и соединяет, смена типа в меню блока и через превращение без потери уровней, текст ↔ список построчно, слияние, только чтение, 6 случаев `normalizeLevels`; 11 — заголовки E05-03: пункты меню H2 и H3, уровень и плейсхолдер, уровень 2 по умолчанию, якорь из id блока и его неизменность при правке текста, сохранение разметки и уровня, превращение по полю `text` без своего `sanitize`, только чтение, круговое преобразование документа с заголовками, текст в заголовок с разметкой; 35 — блок «Текст» E05-02: ссылки — допустимые адреса и их вид, отказ `javascript:`, `data:`, `ftp:`, относительных, без точки в хосте, с логином; очистка поля — разрешённое без изменений, `rel` и только `href`, опасные ссылки, скрипты и неизвестные элементы, `strong`/`em` в `b`/`i`, текст и пробелы как есть; вставка из Word, Google Docs и веб-страницы, переносы строк и слияние, пустая вставка; документ чистит все текстовые поля; из них 15 — сессия E03-03: refresh-токен и срок сессии, когда обновлять, ротация, отказ и чужой `sub` завершают сессию, недоступный провайдер, отзыв и адрес выхода, выход без end-session; хранилище — свежая сессия, одно обновление на одновременные запросы, токены другого экземпляра, принудительное обновление после 401, отказ, провайдер недоступен, срок жизни, выход во время обновления; 35 — ядро редактора E05-01: круговое преобразование документа всех блоков версии 1 через данные Editor.js и JSON, служебные поля и порядок полей, плоский список из вложенного, неизвестный блок, пустой документ; история — отмена и повтор между блоками, слияние ввода, новый шаг после паузы, в другом блоке и после отмены, сброс повтора, эхо отрисовки, предел шагов; каретка по смещению через разметку; `BlockEditor` на фейковом движке — загрузка, пусто, ошибка и «Повторить», документ в формате схемы, клавиши отмены и повтора по коду клавиши, Ctrl+Z без фокуса и не из чужого поля, запись несообщённой правки перед отменой; окно — диалог, закрытие, развёртывание, блокировка прокрутки; 16 — страница блога E04-02: шапка гостя и владельца с датой и кнопками, блог без названия и никнейма, адреса вкладок и текущая, скелетон, «не найден», ошибка с «Повторить», пустые вкладки, разбор адреса (UUID, никнейм, не `id123`), ответы API в состояния, владелец по `registeredAt`, свой блог; 35 — ядро редактора E05-01: круговое преобразование документа всех блоков версии 1 через данные Editor.js и JSON, служебные поля и порядок полей, плоский список из вложенного, неизвестный блок, пустой документ; история — отмена и повтор между блоками, слияние ввода, новый шаг после паузы, в другом блоке и после отмены, сброс повтора, эхо отрисовки, предел шагов; каретка по смещению через разметку; `BlockEditor` на фейковом движке — загрузка, пусто, ошибка и «Повторить», документ в формате схемы, клавиши отмены и повтора по коду клавиши, Ctrl+Z без фокуса и не из чужого поля, запись несообщённой правки перед отменой; окно — диалог, закрытие, развёртывание, блокировка прокрутки; 7 — вход E03-02: пустая форма, загрузка и вход, отказ без подсказки о почте, слишком много попыток и нет связи, код с таймером, неверным и новым кодом, ошибка адреса и таймер из `Retry-After`, возврат только в `/connect/authorize` провайдера; 13 — регистрация E03-01: пустая форма, загрузка, короткий пароль без вызова API, 4 ошибки API, обратный отсчёт и новый код, таймер из `Retry-After`, неверный и верный код, устаревшая регистрация, формат отсчёта, чтение `Retry-After`; 9 — канал реального времени E01-11: новый билет на каждое подключение, бесконечные задержки переподключения, повторная подписка, повтор первого подключения и после отказа SignalR, остановка при выходе, события по темам, запрос билета у API с токеном и 401; 12 — вход E18-02: запрос кода с PKCE S256, обмен кода с верификатором, отказ при чужих state, nonce и issuer, возврат только на страницу сайта, настройки клиента; 5 — трассировка E01-09: разбор `traceparent`, трасса страницы, новый спан на вызов, один trace id на вызовы страницы, новая трасса в браузере; 4 — клиент API E01-08: команда генерации из снимка, файл совпадает с генератором, нет ручных обращений к API, базовый адрес и problem details; 5 — настройка Playwright E01-16: два профиля, каждый тест в обоих, трасса и снимок при падении, отчёты JUnit, продакшен-сборка; 32 — каркас телефона E02-06: пять вкладок по ТЗ и их адреса, текущая вкладка, счётчики и их озвучивание, прокрутка наверх повторным нажатием, экран «Меню» для гостя и вошедшего без «коллекции», «подписки» и монетизации, открытие из панели, контракт стилей — панель ниже 925 px, плавающая кнопка ниже 640 px, безопасные зоны и «чёлка», 44 px, вкладки не расширяют страницу, место под панелью и тосты над ней; 41 — каркас десктопа E02-05: ориентиры, ссылка к содержимому, порядок фокуса, пункты меню без монетизации и «коллекции», текущий пункт на разных адресах, шапка гостя и вошедшего, переключатель темы, состояния виджетов, контракт стилей — ширины колонок из токенов, прилипание, скрытие колонок по точкам перелома; 62 — базовые компоненты E02-04: кнопка, поле, меню, вкладки, окно и шторка, тосты, аватар, полнота витрины `/ui` и переключение темы; прежние 34 — бренд в одном месте, копии `BrandLogo` совпадают, нет картинок, иконки и лицензия, контраст WCAG AA, отличие от цветов DTF, `BrandLogo`; токены: файл совпадает с генератором, роли палитр, контраст всех текстовых цветов, типографика чтения, линтер стилей ловит 10 видов сырых значений и пропускает токены, линтер в обоих клиентах, только известные переменные; витрина токенов; `admin`: 12 — те же 4 проверки клиента API, `BrandLogo`, лицензия иконок, те же 5 проверок настройки Playwright) |
| Сквозные (в каждом из двух профилей) | 87 в профиле, 174 прогона (`frontend`: 86; из них 4 — публикация E05-09 в `e2e/publish.spec.ts`; из них 1 — блоки поста E07-03 в `e2e/post.spec.ts`; из них 1 — #331: курсив, маркер и ссылка переживают текст → заголовок → текст, а тест текст ↔ заголовок ждёт жирный после обратного превращения; из них 3 — лента «Свежее» E08-01 в `e2e/feed.spec.ts`: первая страница с сервера, порядок и следующая страница, переход к посту; из них 2 — вставка списков E05-02: списки Word и Google Docs становятся блоками «Список» с уровнями, список веб-страницы тоже; из них 6 — адрес поста E07-02 в `e2e/post.spec.ts`: пост по адресу со слагом, 301 с неверного слага и блога, пост без заголовка по id, та же схема в теме, 404, `/u/<блог>/comments` не пост; из них 4 — изображение E06-04 на витрине `/ui`: картинка вне экрана не загружается до прокрутки, место занято до загрузки и макет не сдвигается, ширина под плотность экрана, заглушки пустой и сломанной картинки того же размера; плюс в тесте настроек блога аватар берёт 64 или 192 px под плотность; из них 5 — меню блоков и перестановка E05-07: поиск в меню, стрелки и `Enter` (на телефоне — поле шторки и `Enter`), «/» и `Esc`, меню под «+» на десктопе и шторкой на телефоне, перетаскивание за «⋮⋮» с отменой и повтором, Ctrl+Shift+↑/↓ с кареткой; из них 1 — капча E03-12 после неудачных входов на `/login`; из них 4 — пароль E03-05: восстановление на `/login` с неверным кодом, коротким паролем и входом с новым паролем, восстановление в окне входа, смена в `/settings/account` с неверным и верным текущим паролем, гость в настройках аккаунта входит и возвращается; из них 3 — настройки блога E04-03: гость входит и возвращается в настройки; из них 2 — окно входа E03-04: гость нажимает «Нравится» на `/ui`, видит модальное окно или шторку на весь экран, axe без нарушений пустым и с ошибкой у поля, входит и возвращается на ту же страницу с поставленной отметкой один раз; регистрация в окне с ошибкой у поля, axe, переход ко входу и закрытие без действия; 6 — цитата и разделитель E05-05 на настоящем Editor.js: цитата из меню «+» с текстом и подписью раздельно, `Enter` текст → подпись → текст после цитаты, отмена и повтор; текст ↔ цитата через «Превратить в»; разделитель не редактируется и переживает отмену и повтор; `Backspace` в начале следующего блока удаляет его; `Delete` в конце предыдущего тоже; щелчок или стрелка выделяют, `Backspace` или `Delete` удаляют без пустого блока; 4 — списки E05-04 на настоящем Editor.js: `Enter`, `Tab` и `Shift+Tab` с пределом вложенности и делением пункта, пустой пункт поднимает вложенный и завершает список, смена типа через «Превратить в» без потери разметки и уровней, отмена и повтор с вложенностью; 2 — заголовки E05-03 на настоящем Editor.js: текст в «Заголовок», в «Подзаголовок» и обратно с тем же текстом, якорь `h-<id>` переживает правку, отмену и повтор; 4 — блок «Текст» E05-02 на настоящем Editor.js: панель над выделением и в поле зрения, жирный, курсив и маркер; ссылка — отказ `javascript:` с ошибкой, домен с `https://` и `rel`, ссылка переживает отмену и повтор из документа; вставка из Word; вставка веб-страницы без скриптов, фреймов и обработчиков; прежние 34 — прежняя запись 21 опережала на 1, из них 2 — сессия E03-03 на фейковом провайдере: сессия переживает истёкший токен доступа и билет хаба выдаётся, «Выйти» отзывает refresh-токен, завершает сессию провайдера и оставляет гостя; 5 — страница блога E04-02 на фейковом API: серверная отрисовка для гостя, вкладки без перезагрузки и по адресам, владелец с датой и кнопками и чужой блог, «Профиль» ведёт в свой блог, один адрес блога и блог без никнейма по UUID, 404 и ошибка с «Повторить»; 7 — редактор E05-01 на настоящем Editor.js: окно 800×700 по центру и развёрнутое, весь экран на телефоне поверх нижней панели и «Написать», пусто с плейсхолдером, отмена и повтор между блоками с кареткой, форматированный текст восстанавливается из документа без потерь, открытие из «Написать» и возврат по «✕», `Esc`; 2 — вход E03-02 на фейковом API: пустая форма, неверный и верный пароль, вход с cookie провайдера; код из письма, неверный адрес и код, таймер и новый код, вход; 2 — регистрация E03-01 на фейковом API: регистрация, неверный код, таймер и новый код, вход с cookie провайдера; ошибки короткого пароля, неверного адреса и занятой почты; 2 — реальное время E01-11 на фейковом API: гость не подключается, вошедший подключается по билету и переподключается после обрыва с новым; 2 — вход E18-02 на фейковом провайдере: код с PKCE, токенов нет в хранилищах и cookie страницы, ошибка обратного вызова; раскладка профиля, прилипание колонок, 1100 px, 800 px, экран «Меню», прокрутка наверх, безопасные зоны, витрины `/brand`, `/tokens`, `/ui` в светлой и тёмной теме; `admin`: 1 — дымовой) |

Число не должно уменьшаться; уменьшение объясняется в PR.

## Открытые вопросы к владельцу

Ни один не блокирует M0–M3.

| Вопрос | Когда нужен ответ | Связанные задачи |
| --- | --- | --- |
| Капча входа ([ADR-0038](adr/0038-sign-in-captcha.md)): оставить свою проверку вычислением `ProofOfWork` (рекомендация: без третьих лиц и ключей) или подключить Cloudflare Turnstile / hCaptcha (нужны ключ сайта, секретный ключ, реализация `ICaptcha` и виджет) | до запуска M1 | #32 |
| Финальное название и бренд (сейчас временные kitabber и палитра ADR-0011; меняются правкой `brand/brand.json`) | До открытой беты (M5) | E02-01 |
| Подтвердить ADR-0002: свой провайдер OpenID Connect с первого дня | До начала M1 | E18-01, E18-02 |
| Для выкладки входа на stage (ADR-0018) — завести секреты `IDENTITY_SIGNING_KEY`, `IDENTITY_ENCRYPTION_KEY`, `SESSION_PASSWORD` (команды — DEPLOY.md) | До первой выкладки на stage | E18-02 |
| Для выкладки медиа на stage (ADR-0030) — S3-совместимый бакет и ключ доступа: переменные `MEDIA_STORAGE_URL`, `MEDIA_STORAGE_REGION`, `MEDIA_STORAGE_BUCKET`, `MEDIA_STORAGE_ACCESS_KEY` и секрет `MEDIA_STORAGE_SECRET_KEY` ([DEPLOY.md](DEPLOY.md#что-нужно-от-владельца)); без них выкладка останавливается. Подтвердить ADR-0030: файл с расширением другого формата отклоняется, пределы 20/500/100/50 МБ | До следующей выкладки на stage | E06-01 |
| Для раздачи изображений на stage (ADR-0037) — секреты `IMGPROXY_KEY` и `IMGPROXY_SALT` (hex, `openssl rand -hex 32`, DEPLOY.md); по желанию — CDN перед сайтом и ключ хранилища только для чтения для imgproxy. Подтвердить ADR-0037: ключ объекта виден (в Base64) в подписанной ссылке, что уточняет п. 6 ADR-0030 | До выкладки медиа на stage | E06-02 |
| Какие проекты будут объединять аккаунты и на каком они стеке | До M7 | E18-06 |
| Хранилище телеметрии: на stage — свой Grafana-стек (решено 21 ноября 2026, задача #318); для production — общее решение под Grafana и телеметрию для всех проектов владельца, скорее всего одно хранилище на все проекты (#321) | Production — до открытой беты | E01-09, #318, #321 |
| Хостинг (сервер с Docker), домен, почтовый сервис, хранилище; что задать для stage — в [DEPLOY.md](DEPLOY.md#что-нужно-от-владельца) | До первой выкладки на stage | E01-03, E01-12 |
| Почтовый сервис: решено — транзакционный сервис на бесплатном тарифе для stage и production; выбор конкретного сервиса, домен отправителя (SPF, DKIM, DMARC) и переменные `SMTP_*` — после первого релиза (#320) | После первого релиза | E01-12, #320 |
| Первый релиз — 0.1.0, не 1.0.0: Release PR release-please копит изменения и не сливается до первой беты (решение владельца 21 ноября 2026) | До первой беты | release-please |
| Где держать секреты стендов: выкладка сделана на секретах окружений GitHub (рекомендация ADR-0007) — подтвердить или выбрать другое | До первой выкладки на stage | E01-03 |
| Ключи внешних провайдеров входа (Яндекс, VK, Google, Telegram) | До M2 | E03-07 |
| Подтвердить ADR-0013: витрина компонентов — страница `/ui` сайта вместо Histoire или Storybook, как предлагала задача #14 | До E02-05 (#15) | E02-04 |
| Итоговый текст правил сообщества | До M4 | E15-17 |
| Бот Telegram, служебный аккаунт и `api_id`, сервер вне России, тестовый канал | До M6 | E19-01 |
| Переносить ли мост с Telegram раньше (до открытой беты) | До конца M3 | эпик E19 |
| Предложения в спецификациях экранов ([docs/ui](ui/README.md), пометка «предложение, требует подтверждения»); пункты меню «коллекция» и «подписка» из раздела 5 ТЗ не показываются. Каркас десктопа #15 сделан по ним: промежуток колонок `--space-6`, высота шапки `--space-16`, адреса меню (`frontend/utils/navigation.ts`), тема хранится в браузере, а не в аккаунте. Каркас телефона #16: нижняя панель и экран «Меню» (`/menu`, отдельная страница) показываются ниже 925 px, плавающая «Написать» — ниже 640 px; адреса `/menu`, `/profile`, `/drafts`, `/bookmarks`, `/settings`; на экране «Меню» есть темы и «Выйти»; на этих предложениях построены и сквозные тесты #17 | До экранов лент (M1): пока ответа нет, тесты каркаса фиксируют предложения | E02-03, E01-16 |
| Юридические документы | До M7 | E17-08 |
| Нужен ли короткий числовой номер блога для адреса `id123` из ТЗ или хватит UUID (страница блога E04-02 пока живёт на `/u/<никнейм>` и `/u/<UUID>`; адрес `/u/` — предложение ui/blog.md) | До E04-05 (#95) | E04-01 |
| MassTransit 8 (Apache-2.0) пока поддерживается авторами ограниченно; при прекращении поддержки — лицензия 9, Wolverine или свой outbox (ADR-0005) | Когда ветка 8 перестанет получать исправления | E01-06 |

## Настройки репозитория

Включено (19 ноября 2026):

- репозиторий публичный, основная ветка `main`;
- слияние только сквошем, заголовок сквош-коммита — заголовок PR, ветка удаляется после слияния;
- workflow `ci`, `PR title` и `Release` работают;
- GitHub Actions разрешено создавать pull request (нужно release-please);
- правило `main`: только через PR, только сквош, обязательные проверки `pr-title / check` и `verify`, запрет удаления и принудительной записи;
- автослияние (auto-merge) разрешено.

Не включено — нужно действие владельца:

| Что | Зачем | Где |
| --- | --- | --- |
| Приложение Renovate | Обновление зависимостей; конфигурация уже в `renovate.json` | github.com/apps/renovate |
| Обязательная проверка `images / build` | Сборка образов и смоук выкладки на каждом PR (E01-03); сейчас запускается, но не обязательна | Settings → Branches → правило `main` |
| Окружения `stage` и `production`, переменная `DEPLOY_STAGE` | Подключение стендов; полный список значений — [DEPLOY.md](DEPLOY.md#что-нужно-от-владельца) | Settings → Environments, Settings → Variables |
