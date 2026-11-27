# Состояние проекта

Обновляется в каждом PR. Последнее обновление: 27 ноября 2026 (репосты E10-08: репост в свой блог с текстом, список репостов поста, оригинал скрывает репосты при удалении, уведомление автору).

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
  [ADR-0064](adr/0064-realtime-event-order.md) (реальное время: порядок событий гарантирован только внутри одной темы,
  между темами его нет — у каждой темы свой канал Redis; предложено, задача #316).
  [ADR-0069](adr/0069-stage-telemetry.md) (телеметрия stage: коллектор OpenTelemetry, Prometheus, Loki, Tempo и
  Grafana отдельными контейнерами, конфигурация внутри compose-файла, хранение 14 дней; предложено, задача #318).
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
  `AccountActivated` при первом подтверждении почты (с #409 — `account.created.v1`), личный блог создают воркеры, запасной путь — `/profiles/me`,
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
  [ADR-0067](adr/0067-image-preview-and-dominant-color.md) (размытое превью и доминирующий цвет: считаются при
  загрузке через SkiaSharp, WebP 16 px и средний цвет в `media_files`, в `UiImage` — цвет и размытая SVG-фильтром
  копия до загрузки из HTML сервера; предложено, задача E06-03).
  [ADR-0070](adr/0070-settings-sections-and-account-preferences.md) (раздел настроек: список `/settings` и разделы по
  своим адресам, на десктопе — в правой колонке; модуль Preferences со схемой `preferences`, тема аккаунта на всех
  устройствах через `/api/v1/preferences/me`; предложено, задача E04-08).
  [ADR-0079](adr/0079-blog-cover-position.md) (обложка блога: картинка целой, положение — вертикальный процент
  `object-position`, шапка 10 : 3 на всех экранах, `PUT`/`DELETE /api/v1/profiles/me/cover`; предложено, задача E04-04).  [ADR-0082](adr/0082-topic-owner-and-topic-page.md) (владелец темы — `owner_account_id`, `Profile.owner`, лента одного
  блога `GET /api/v1/feeds/blogs/{blogId}`, страница темы `/<тема>` с 301 на один адрес; предложено, задача E11-01).
  [ADR-0080](adr/0080-feed-cache.md) (кэш лент: целая страница в Redis под ключом с поколением, сброс — новым
  поколением после публикации, правки и удаления опубликованного поста, срок жизни 30 секунд по `TimeProvider`, без
  Redis — из базы; так же кэшируется лента блога; предложено, задача E08-09).
  [ADR-0084](adr/0084-topic-creation-by-people.md) (создание темы пользователем: `POST /api/v1/topics`, подтверждённая
  почта, лимит тем на аккаунт и стоп-список из настроек, дубли названия и адреса, зарезервированные адреса страниц сайта,
  форма `/topics/new`; предложено, задача E11-11).
  [ADR-0086](adr/0086-hotness-and-popular-feed.md) («горячесть»: `sign · log10(очки) + возраст / DecayHours`, коэффициенты
  в настройках `Posts:Hotness:*` — предложение, требует подтверждения; инкрементальный проход в API под advisory-блокировкой,
  таблица `posts.post_hotness` в модуле Posts вместо отдельного `Feeds`; лента `/api/v1/feeds/popular`; предложено, задача E08-05).
  [ADR-0104](adr/0104-top-periods.md) (топ за период: рейтинг из `post_hotness`, скользящие периоды, `/api/v1/feeds/top/{period}`,
  страницы `/top/<период>`; предложено, задача E08-06).
  [ADR-0106](adr/0106-fresh-rating-filter.md) (фильтр «Свежего»: пороги −10, +5, +10 по рейтингу из `post_hotness`,
  `?minRating=`, выбор в Preferences `feeds.freshMinRating`, у гостя — `localStorage`; предложено, задача E08-07).
  [ADR-0108](adr/0108-show-new-posts.md) («Показать новые»: перечитывание первой страницы «Свежего» с тем же порогом раз в
  минуту, без события SignalR, новые посты сверху только по нажатию; предложено, задача E08-08).
  [ADR-0111](adr/0111-pull-to-refresh-and-hiding-header.md) (жесты ленты на телефоне: свои обработчики касаний
  `PullToRefresh`, на «Свежем» обновление — «Показать новые» сразу; шапка прячется по `useHideOnScroll`, прилипающее под
  ней считает `top` от `--shell-header-offset`; предложено, задача E08-14).
  [ADR-0114](adr/0114-blog-comments-tab.md) (вкладка «Комментарии»: у блога — комментарии владельца, у темы — под её
  постами; `GET /api/v1/comments/blogs/{blogId}` с курсором, `IPostDirectory.PublishedPostIdsAsync`, чтение пачками с
  отсевом неопубликованных и заблокированных, клиент — через BFF с токеном; вкладки темы — дочерние страницы;
  предложено, задача E09-15).
  [ADR-0116](adr/0116-bookmarks-section.md) (раздел «Закладки»: контракт `IBookmarkList` в Reactions, вкладки строят
  Posts (`/api/v1/feeds/bookmarks`) и Comments (`/api/v1/comments/bookmarks`), удалённое — на своём месте без
  содержимого, только свои; клиент — `/bookmarks` и `/bookmarks/comments` через BFF; предложено, задача E04-12).
  [ADR-0117](adr/0117-reposts.md) (репост — пост в личном блоге с `repost_of_post_id` и текстом, в ленте блога и
  «Моей ленте» с оригиналом внутри, не в общих лентах и поиске, удаление оригинала скрывает репосты, событие
  `PostReposted` — уведомление `repost` автору; предложено, задача E10-08).
  [ADR-0089](adr/0089-my-feed.md) («Моя лента»: посты блогов подписки и людей, на чьи блоги подписан читатель, по дате
  с курсором «Свежего»; исключения читателя — контракт `IFeedExclusionSource`, источники добавят #81 и #112; без общего
  кэша, `Cache-Control: private, no-store`; предложено, задача E08-04).
  [ADR-0092](adr/0092-notification-events.md) (события `CommentPosted`, `ReactionPut`, `BlogSubscribed` через outbox
  модулей-владельцев, модуль Notifications в воркерах, правила «никому о своём действии» и «одно действие — одно
  уведомление», идемпотентность inbox и ключом `(получатель, действие)`, подавление через `INotificationSuppression`,
  его реализует чёрный список Preferences; подписка на тему не уведомляет — предложение, требует подтверждения;
  предложено, задача E12-01).
  [ADR-0093](adr/0093-notification-feed-and-counter.md) (лента уведомлений и счётчик: модуль Notifications и в API,
  лента по курсору `(created_at, id)`, число непрочитанных, отметка показанных `POST …/read` с ответом новым числом;
  отметка по показу каждой загруженной страницы, точки остаются до следующего открытия — предложение, требует
  подтверждения; строка без заголовка поста и цитаты; предложено, задача E12-02).
  [ADR-0094](adr/0094-search-indexing.md) (модуль Search в воркерах, OpenSearch за `ISearchIndex` по REST без клиентской
  библиотеки, индексы `posts` и `blogs` за алиасами с анализатором `russian`; тонкие события `PostChanged` и
  `BlogChanged` — потребитель перечитывает состояние у владельца; переиндексация командой `search-reindex` с
  переключением алиаса; адрес OpenSearch по умолчанию и «снятый = удалённый» — предложения, требуют подтверждения;
  предложено, задача E14-01).
  [ADR-0097](adr/0097-search-suggestions-and-history.md) (подсказки поиска в шапке: модуль Search в API только читает
  индексы, `bool_prefix` по последнему слову, документы из `_source`, блоги карточками `IBlogDirectory`; чёрный список
  читателя скрывает посты и личные блоги, но не темы и не скрытое из лент; лимит `search.suggestions` 120 в минуту;
  история — в `localStorage` по ключу HMAC от `account_id` на пароле сессий сайта, гостю своя, на сервере не
  хранится; размеры списков, пауза, лимит и Enter без страницы результатов — предложения, требуют подтверждения;
  предложено, задача E14-02).
  [ADR-0091](adr/0091-black-list.md) (чёрный список в модуле Preferences, контракт `IBlockList`; исключения читателя в общих лентах — после чтения из кэша, курсор всей страницы; комментарии заблокированных свёрнуты, запрет отвечать и комментировать под постом; предел 1000 и запрет под постом — предложение, требует подтверждения; предложено, задача E11-06).

  [ADR-0041](adr/0041-post-address-and-slug.md) (адрес поста `<адрес блога>/<id>-<слаг>`, пост находится по UUID,
  неверный слаг или блог — 301 на канонический адрес, слаг не хранится и строится из заголовка; предложено, задача
  E07-02).
  [ADR-0042](adr/0042-pasted-lists.md) (вставленные списки Word, Google Docs и веб-страниц — блок «Список» с
  уровнями; предложено, задача E05-02).
  [ADR-0048](adr/0048-comment-tree-and-api.md) (комментарии: модуль Comments, материализованный путь в `COLLATE "C"`,
  ветка одним запросом, глубина из `Comments:MaxDepth`, вид `Native`/`Guest`, контракт `Posts.Contracts`; предложено,
  задача E09-01).
  [ADR-0054](adr/0054-comment-tree-levels.md) (дерево комментариев: чтение на `levels` уровней с числом ответов
  `replies`, глубокие ответы — веткой по кнопке, 6 уровней на широком экране и 3 на телефоне, сворачивание линией
  ветки, авторы — профилями по 6 запросов; предложено, задача E09-02).
  [ADR-0056](adr/0056-comment-form.md) (форма комментария: отправка через `/bff/comments`, комментарий в дереве сразу
  и откат при сбое с возвратом текста, черновик в localStorage, окно входа для гостя; предложено, задача E09-03).
  [ADR-0058](adr/0058-comment-sorting.md) (порядок комментариев: `sort=popular|date` у чтения дерева, сортируются
  ветки под постом, ответы — в порядке дерева; «Популярные» — по числу ответов до рейтинга E10-05, «По дате» — новые
  сверху; выбор в localStorage на все посты; предложено, задача E09-04).
  [ADR-0059](adr/0059-comment-edit-and-delete.md) (правка и удаление комментария: окно `Comments:EditMinutes`, 15
  минут, удалять можно всегда; удалённый с ответами — заглушка, без ответов — удаляется с опустевшими заглушками выше;
  свои комментарии страница узнаёт через сервер сайта; меню «…» своего комментария; предложено, задача E09-05).
  [ADR-0060](adr/0060-comment-branch-screen.md) (экран ветки: на телефоне ответы глубже одного чтения открываются
  страницей `<адрес поста>/comments/<id>`, а не на месте; ветка там начинается от левого края, текст не уже колонки
  без трёх отступов; на десктопе — по-прежнему «Показать ещё»; предложено, задача E09-06).
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
  перенос — один шаг истории.
- Блок «Текст» (E05-02, #37, [ADR-0028](adr/0028-text-block-formatting.md)): всплывающая панель над выделением
  (под ним, если сверху нет места) — жирный, курсив, маркер, ссылка; ссылка принимает только `http`, `https`,
  `mailto` и получает `rel="nofollow ugc noopener noreferrer"`, иначе ошибка под полем; вставка из Word, Google Docs
  и веб-страниц чистится до белого списка ADR-0023 (`frontend/editor/inline.ts`, `tools.ts`), документ чистит
  каждое текстовое поле тем же списком. Списки из буфера вставляются блоком «Список» с уровнями, маркер Word
  выбрасывается ([ADR-0042](adr/0042-pasted-lists.md)).
- Редактор на телефоне (E05-10, #42, [ADR-0047](adr/0047-phone-keyboard-panel.md), предложено; **частично**): на
  ширине до 640 px `EditorWindow` берёт рамку видимой области (`frontend/composables/useVisualViewport.ts`), так
  что клавиатура iOS и Android не закрывает его низ; панель `frontend/components/editor/EditorKeyboardBar.vue` в
  слоте `toolbar` — «+» (меню блоков шторкой), «Ж», «К»; пока клавиатура открыта, подвал с «Опубликовать» скрыт.
  Кнопки не забирают фокус, а `BlockEditor.keepSelection` возвращает выделение, если браузер его всё же увёл.
  Кнопки «Фото из галереи» и «Снять на камеру» добавляют блок «Фото» (E05-06, #45, ADR-0049).
  **Осталось по #42:** только ручная проверка на живых iOS и Android (порядок — в PR #357), включая камеру.
- Блок «Фото» (E05-06, #45, [ADR-0049](adr/0049-image-block.md), предложено): одно изображение с подписью
  (`frontend/editor/image.ts`). Тип `image` версии 1 схем — `media` (id загруженного файла), `width`, `height`
  (размер из браузера) и `caption` не длиннее 300 знаков; `Posts` пускает только изображения, загруженные автором,
  и отдаёт подписанные источники в `PostView.images` (320…1920 px). В редакторе — выбор файла сразу из меню,
  состояния «пусто», «загрузка» (превью под индикатором), «ошибка» («Повторить», «Выбрать другой файл»), «готово»;
  блок без файла не попадает в документ. На странице поста — `figure` с `UiImage` и подписью. Не сделано: видео, GIF,
  галерея из нескольких файлов, полноэкранный просмотр (M3, E06); размер на сервере и уборка незагруженных сирот.
  `POST /api/v1/editor/documents/validation` (только вошедшим) возвращает очищенный документ или 400 с ошибками.
  Не сделано здесь: остальные блоки (добавляют свои задачи; «Фото» — ADR-0049), лимиты размера поста и блока (сделаны в #49, ADR-0046).
- Бэклог в GitHub Issues: 262 задачи в 19 эпиках и 8 вехах; порядок — в [ROADMAP.md](ROADMAP.md).
- Процесс: `CLAUDE.md`, `scripts/verify.ps1`, CI (`ci / verify`), проверка заголовка PR, release-please,
  конфигурация Renovate (в том числе regex-менеджер для версии gitleaks в `ci.yml`, #285).

- Каркас монорепозитория (E01-01): решение .NET `backend/Platform.slnx` (хосты `Api` и `Workers`, net10.0),
  веб-клиент на Nuxt 3 в `frontend/`, админка на Vue 3 + Vite в `admin/`; общие `.editorconfig`,
  `.prettierrc.json`, `global.json`. Структура и линтеры описаны в [ARCHITECTURE.md](ARCHITECTURE.md).
- `scripts/verify.ps1` проверяет структуру репозитория, сборку и `dotnet format` бэкенда, ESLint, Prettier,
  проверку типов и `pnpm build` обоих клиентов. CI ставит .NET по `global.json`, pnpm и Node 22.
- CI (E01-02): `ci / verify` запускает `scripts/verify.ps1` с кэшами NuGet и pnpm и тайм-аутом 20 минут (было 10, поднят в #375: полная проверка выросла до 9–10 минут);
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
  транспорт и кэш в памяти работают без Docker. Правила — [ADR-0005](adr/0005-messaging-and-cache.md). Несколько
  публикующих модулей в одном хосте (#281): каждый публикует через `IModuleOutbox<TContext>` в outbox своей схемы,
  обычный `IPublishEndpoint` области — outbox первого модуля хоста (для служб вне модулей); тест `MultiModuleOutboxTests`.
  Почта модуля (#393) — `IMailer<TContext>` поверх его outbox, письмо уходит после коммита своего модуля, каким бы
  по счёту он ни был (`ModuleMailTests`); код модулей не берёт обычные `IPublishEndpoint`, `ISendEndpointProvider` и
  `IMailer` (архитектурный тест `ModuleOutboxTests`).

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
  строит клиент так же, как эта команда (`openapiTS` + `astToString` в процессе теста, без отдельного процесса CLI —
  тот под нагрузкой полного прогона упирался в таймаут 5 с, #307), и сравнивает с закоммиченным файлом,
  запрещает ручные `fetch`/`$fetch`/`useFetch` и строки `/api/` вне клиента и проверяет, что ошибка приходит как problem details. Пока `paths` пуст. Клиент сайта
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
  `frontend/tests/api-trace.spec.ts`. На stage телеметрию принимает свой Grafana-стек (#318, ниже); канала
  оповещений пока нет — см. открытые вопросы.
- Телеметрия стенда stage (#318, [ADR-0069](adr/0069-stage-telemetry.md), предложено): в `deploy/compose.stage.yaml`
  `otel-collector` (contrib), Prometheus, Loki, Tempo и Grafana, образы закреплены по дайджесту (Renovate). API и
  воркеры шлют OTLP на `http://otel-collector:4317`; трассы — в Tempo, логи — в Loki (`trace_id` в метаданных),
  метрики — в Prometheus через его приём OTLP, `/metrics` API снимается заданием `platform-api-scrape`. Портов наружу
  нет, Caddy отдаёт только Grafana на `GRAFANA_ADDRESS` (пароль `GRAFANA_ADMIN_PASSWORD`, без анонимов и
  регистрации). Хранение — 14 дней (Prometheus ещё не больше 10 ГБ), вывод контейнеров — 5 × 20 МБ. API на стенде
  пишет журнал запросов (`Hosting.Diagnostics`), строки проб коллектор отбрасывает. Дашборды «API», «.NET runtime»,
  «PostgreSQL (Npgsql)», переход лог → трасса и трасса → логи, правило Prometheus `ApiServerErrors` (5xx больше 5%
  пять минут). Конфигурация — `configs:` внутри compose-файла: выкладка по SSH файлов на сервер не копирует.
  Проверки — статическая часть `scripts/deploy-test.ps1` (адрес OTLP, порты, только Grafana за Caddy, пароль, срок
  7–15 дней, дашборды — JSON с настроенными источниками, алерт) и смоук `images / build` (401 без пароля; трасса,
  строка лога и метрика запроса к API через источники Grafana; снятие `/metrics`, правило, дашборды). Nuxt
  телеметрию пока не шлёт (ADR-0015).
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
  В его папку `bin` копируются `appsettings.json` сразу Api, Workers и Migrator (побеждает собранный последним),
  поэтому тесты хоста воркеров читают настройки из `backend/src/Workers` (`HostSettings.Workers(...)` передаёт
  `--contentRoot`), а не из `bin`; новый тест хоста с файлом настроек — так же (#411).
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
  Identity публикует `account.created.v1` (до #409 — `AccountActivated`) через свой outbox, когда почта аккаунта
  подтверждена впервые (регистрация или первый вход по коду); `AccountCreatedBlogsConsumer` в воркерах создаёт личный блог
  с inbox в схеме `blogs` (воркерам теперь нужна `ConnectionStrings:Platform`; модули подключаются в
  `WorkerHost.CreateBuilder`, а `WorkerHost.Configure` остаётся без модулей для тестов). API: `GET /api/v1/profiles/{id}`
  и `GET /api/v1/profiles/by-nickname/{nickname}` открыты всем (404 `profile_not_found`, 400 `validation_failed`), как
  и `GET /api/v1/profiles/by-account/{accountId}` — личный блог аккаунта, автор поста (E07-04, ADR-0050; блог не создаёт),
  `GET /api/v1/profiles/me` — вошедшему, создаёт личный блог, если его ещё нет; `registeredAt` видит только владелец.
  Демо-данные: личные блоги 20 демо-аккаунтов с никнеймами `demo0`…`demo19`. Смена никнейма — E04-05 (ниже).
- Онбординг (E03-10, #93, частично, [ADR-0102](adr/0102-onboarding.md), FR-AUTH): у личного блога `onboarded_at`
  (миграция `AddOnboarding`, существующим блогам и демо-данным проставлено — онбординг видят только новые аккаунты),
  в профиле владельца `onboardingPending`. `POST /api/v1/profiles/me/onboarding` (`OnboardingInput`: `name`, `avatarId`,
  `topicIds`) сохраняет имя (2–25 графем), аватар и подписки на темы разом и отмечает онбординг пройденным; пустое поле —
  пропущенный шаг; повтор — 409 `blogs.onboarding_done`. Сайт: колбэк входа ведёт аккаунт с `onboardingPending` на
  `/onboarding?returnTo=…` (API не ответил — сразу на страницу), там три шага «Имя», «Аватар», «Темы» (первая страница
  каталога) с «Пропустить» у каждого, отправка через `/bff/onboarding`. **Осталось:** шаг уведомлений (нет настроек,
  E12-05 #202) и подтверждение длины имени владельцем.
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
  здесь: остальных
  настроек раздела, вопроса при уходе с несохранёнными изменениями, уборки старых файлов аватара.
- Обложка блога (E04-04, #94, [ADR-0079](adr/0079-blog-cover-position.md), FR-PROFILE): в `Subsite` —
  `cover_media_id` и `cover_position` (0…100, по умолчанию 50, миграция `AddSubsiteCover`). `PUT
  /api/v1/profiles/me/cover` (`CoverInput`: `mediaId`, `position`) ставит или двигает обложку, `DELETE` убирает;
  файл проверяется как аватар (своё изображение), положение вне 0…100 — 400 с полем `position`. `Profile.cover`
  (`ProfileCover`): `id`, `position`, `sources` (вся картинка по ширинам 400…1920 WebP) и `preview`; `null` без
  обложки. Клиент: `components/blog/BlogCover.vue` в шапке блога — коробка 10 : 3 на всех экранах с `object-fit:
  cover` и `object-position: 50% <position>%`, поэтому полоса одна и та же на десктопе и телефоне; владельцу —
  «Загрузить обложку», «Изменить положение», «Заменить», «Удалить обложку»; в режиме правки ползунок, который
  двигают мышью и пальцем (pointer events, `touch-action: none`) и стрелками; расчёт — `utils/cover-position.ts`;
  картинка уходит через `/bff/media` только по «Сохранить», обложка — через `/bff/own-cover` (PUT и DELETE).
  Нет здесь: уборки старых файлов обложек, горизонтального положения, смены обложки темы (E11-12, #114).
- Смена никнейма (E04-05, #95, [ADR-0090](adr/0090-nickname-change-and-old-address.md), FR-PROFILE):
  `PUT /api/v1/profiles/me/nickname` (`NicknameInput`) проверяет форму, зарезервированные адреса и стоп-список тем
  (400 по полю `nickname`), 30 дней с последней смены (409 `blogs.nickname_change_too_soon`, `TimeProvider`) и
  занятость: никнейм другого блога или темы и никнейм, оставленный другим блогом меньше 90 дней назад (400). Общие
  правила адреса — `BlogAddresses`, создание темы тоже на них. Первый никнейм блога без никнейма — не смена. В
  `Subsite` — `nickname_changed_at`, оставленные никнеймы — `blogs.former_nicknames` (миграция `AddNicknameChanges`);
  `GET /profiles/by-nickname/{старый}` отвечает блогом с новым никнеймом, и сайт ведёт 301 на `/u/<новый>`, пока
  старый никнейм не займёт другой. Владелец видит `Profile.nicknameChangeableFrom`. Клиент: карточка «Никнейм» на
  `/settings/blog` (`NicknameForm`, `PUT /bff/own-nickname`): проверка формы при вводе, ошибки API под полем, поле
  закрыто до даты следующей смены. Нет здесь: живой проверки занятости по мере ввода («✓ свободен» из
  docs/ui/settings.md), смены адреса темы.
- Тема и её страница (E11-01, #76, [ADR-0082](adr/0082-topic-owner-and-topic-page.md), FR-TOPIC): у темы есть
  владелец — `owner_account_id` (`Subsite.CreateTopic` требует его; миграции нет, индекс личного блога отфильтрован
  по типу), демо-темы принадлежат демо-аккаунтам с тем же номером. `Profile.owner` (`ProfileOwner`: `id`, `name`,
  `nickname`) — личный блог владельца темы, иначе `null`. Модуль Posts: `GET /api/v1/feeds/blogs/{blogId}` открыт
  всем — опубликованные посты блога или темы, новые сверху, курсор ленты «Свежее», 404 `posts.blog_not_found`.
  Клиент: `pages/[topic].vue` на `/<тема>` — `components/topic/TopicView.vue` (обложка только показом, аватар,
  `h1`, описание, «Владелец темы: <блог>»; скелетон и ошибка с «Повторить») и `TopicFeed.vue` (общий `FeedView` с
  `useFeed`, первая страница на сервере; пусто — «В этой теме пока нет постов» и «Написать в тему» →
  `/write?topic=<id>` с выбранной темой; сбой ленты — «Повторить»). Нет темы — страница 404 (`error.vue`); другой
  регистр, UUID и `/u/<тема>` — 301 на `/<тема>`, `/<ник личного блога>` — 301 на `/u/<ник>`. `FeedView` без
  `title` берёт `label` и свой текст и действие пустого состояния. Тесты: `SubsiteRulesTests`, `TopicApiTests`,
  `BlogFeedApiTests`, `PublishApiTests.PostPublishedInATopicIsInItsFeed`, `tests/topic/TopicView.spec.ts`,
  `e2e/topic.spec.ts`. Нет здесь: создания тем (E11-11, #77; инструментов администрации нет), управления темой
  (#114), правил (#186), модераторов и закреплённых (#174), вкладки
  «Комментарии» и сортировки; вкладка «Посты» личного блога по-прежнему пустая, хотя
  лента одного блога для неё уже есть.
- Подписки на блоги и темы (E11-03, FR-SUB, [ADR-0085](adr/0085-blog-subscriptions.md)): таблица
  `blogs.subscriptions` (ключ — аккаунт и блог, индекс `ix_subscriptions_subsite`, внешний ключ на `subsites`
  с каскадом). `PUT` и `DELETE /api/v1/blogs/{id}/subscription` — только вошедшему, оба идемпотентны и отвечают
  `SubscriptionView` (число подписчиков и своя отметка); на свой личный блог — 403 `blogs.own_blog` (на свою тему
  можно), нет блога — 404 `blogs.blog_not_found`. `Profile` несёт `subscriberCount`, `subscriptionCount` (сколько
  блогов читает владелец личного блога; у темы `null`) и `subscribed`; числа считаются по строкам при каждом
  чтении. Контракт `IBlogSubscriptions` (`SubscribedBlogsAsync`, `IsSubscribedAsync`) — для «Моей ленты» (#80) и
  списков подписок (#96). Клиент: `BlogSubscription.vue` в шапке `BlogView` и `TopicView`, состояние —
  `useSubscription` на странице: кнопка и счётчик меняются сразу, при сбое откат с тостом, гостю окно входа и
  подписка сама после входа; изменение идёт через `/bff/subscription`. Тесты: `SubscriptionApiTests`,
  `SubscriptionRulesTests`, `tests/blog/subscriptions.spec.ts`, `BlogView.spec.ts`, `TopicView.spec.ts`,
  `e2e/subscriptions.spec.ts`. Нет здесь: событий о подписке (уведомление «новый подписчик», FR-NOTIFY), кнопки
  на карточках ленты и в шапке поста, раздела «Подписки» с массовой отпиской, колокольчика уведомлений о постах.
- Списки подписчиков и подписок (E04-06, #96, FR-PROFILE, [ADR-0099](adr/0099-subscriber-lists.md)), **частично: нет
  правила скрытого профиля — самой настройки ещё нет, критерий проверяется вместе с #181**. `GET
  /api/v1/blogs/{id}/subscribers` (у блога и темы) и `GET /api/v1/blogs/{id}/subscriptions` (только у личного блога,
  у темы — 404 `blogs.topic_has_no_subscriptions`), открыты всем, `CursorPage<SubscriptionCard>`, новые подписки
  сначала, курсор `(subscribedAt, key)`; подписчик показан своим личным блогом, строка несёт `subscribed` и `own`
  смотрящего; строк ровно столько, сколько чисел в профиле. Заблокированные не скрываются (предложение в ADR-0099).
  Клиент: числа в `BlogSubscription.vue` — кнопки, открывают `SubscriptionListDialog.vue` (модальное окно, на телефоне
  лист) со `SubscriptionList.vue`: заглушки, пусто, ошибка с «Повторить», «Показать ещё», «Подписаться» в строке с
  откатом и тостом; чтение через `/bff/subscription-list`. Тесты: `SubscriptionListApiTests`,
  `tests/blog/SubscriptionList.spec.ts`, `e2e/subscription-lists.spec.ts`.
- Создание темы пользователем (E11-11, #77, [ADR-0084](adr/0084-topic-creation-by-people.md), FR-TOPIC): модуль Blogs,
  `POST /api/v1/topics` (`TopicInput`: `name`, `nickname`, `description`) — вход и подтверждённая почта (иначе 401 и
  403 `email_not_confirmed`), 201 с `TopicSummary`, создавший — владелец. Лимит тем на аккаунт — настройка
  `Blogs:Topics:MaxPerAccount` (5), сверх него — 409 `blogs.topic_limit_reached`. Название — 2–25 символов, не дубль
  названия другой темы без учёта регистра; адрес — правила никнейма, не занят блогом или темой, не из
  `TopicCreationRules.ReservedAddresses` (страницы сайта, служебные префиксы, будущие разделы; тест сверяет список с
  `frontend/pages`); слово названия или адреса не начинается с элемента стоп-списка `Blogs:Topics:StopWords`
  (`TopicCreationRules.StopWordIn`). Клиент: `/topics/new` (`pages/topics/new.vue`,
  `components/topic/TopicCreateForm.vue`, `server/routes/bff/topics.post.ts`) — длины и вид адреса проверяются при
  вводе, ошибки API под полями, лимит, неподтверждённая почта со ссылкой и сбой — над кнопкой; созданная тема
  открывается по адресу; «Создать тему» в блоке «Темы» левого меню и, для вошедшего, на экране «Меню». Тесты:
  `TopicCreationRulesTests`, `TopicCreateApiTests`, `tests/topic/TopicCreateForm.spec.ts`,
  `e2e/topic-create.spec.ts`. Нет здесь: управления темой (#114), создания тем администрацией, уникального индекса
  по названию.
- Темы в меню и каталог тем (E11-02, #78, [ADR-0088](adr/0088-topic-menu-and-catalog.md), FR-TOPIC, FR-SUB): модуль
  Blogs, `Topics/TopicCatalogEndpoints.cs`. `GET /api/v1/topics/menu` (открыт всем) — `TopicMenu` с `source`:
  `subscriptions` — темы, на которые подписан вошедший, по названию; `popular` — самые популярные темы для гостя и
  для того, кто ещё ни на одну тему не подписан; не больше `TopicCatalogRules.MenuSize` (10).
  `GET /api/v1/topics/catalog?q=&cursor=&limit=` — `CursorPage<TopicCard>` (`id`, `name`, `nickname`, `description`,
  `subscriberCount`): больше подписчиков — выше, при равенстве новее — выше, курсор `(subscriberCount, id)`
  (ADR-0006); `q` ищет подстроку в названии или адресе без учёта регистра (`ILIKE`, `%` и `_` экранируются), длиннее
  50 символов — 400. Числа считаются по строкам `blogs.subscriptions` (как в профиле, ADR-0085). Клиент: меню берёт
  темы через `server/routes/bff/topic-menu.get.ts` (`composables/useTopicMenu.ts`, после показа страницы; запрашивает
  снова при входе и выходе и после смены подписки), пока они грузятся или при сбое в меню только «Показать все»;
  каталог `/topics` (`pages/topics/index.vue`, `components/topic/TopicCatalog.vue`) — первая страница на сервере,
  поиск с паузой 300 мс сохраняется в адресе `?q=`, «Показать ещё», состояния загрузки, пустоты (нет тем / ничего не
  нашлось) и ошибки с «Повторить». На телефоне под баннером cookie теперь есть место той же высоты, чтобы конец
  страницы (служебные ссылки экрана «Меню») можно было из-под него прокрутить. Тесты: `TopicCatalogApiTests`,
  `TopicCatalogRulesTests`, `tests/topic/TopicCatalog.spec.ts`, `tests/topic/topic-menu.spec.ts`,
  `tests/shell/ShellCookieBanner.spec.ts`, `e2e/topics.spec.ts`. Нет здесь: аватаров тем в каталоге и меню (у тем их
  пока не задают), счётчика подписчиков в меню, ручного порядка тем в меню, раздела «Подписки» с массовой отпиской
  (FR-SUB).
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
  показывает шапку, заголовок, блоки и панель действий (E07-04), страница темы — `/<тема>` (E11-01, ниже).
- Шапка поста и плавающая панель действий (E07-04, #52, [ADR-0050](adr/0050-post-header-and-action-panel.md),
  FR-POST): `lookUpPost` отдаёт ещё `author` — для личного блога это сам блог, для темы — `GET
  /api/v1/profiles/by-account/{accountId}` (не прочитался — `null`, шапка без автора). Шапка в `PostView.vue`: аватар
  и имя автора со ссылкой на его блог, тема — только для поста в теме, `time` с датой (UTC) или «черновик».
  `components/post/PostActions.vue` — `toolbar` «Действия с постом» («Нравится», «Комментарии», «Поделиться», стрелки
  между кнопками), `position: sticky` у низа окна в колонке поста, ниже 925 px — над нижней панелью, ниже 640 px — во
  всю ширину; всё рисуется на сервере. Пока: «Нравится» выключена до E10-01 (#63), «Комментарии» ведут к якорю
  `#comments` в конце поста (дерево — E09-02, #58), «Поделиться» копировала ссылку с тостом (системное меню и соцсети —
  сделаны в E07-07, #97, ниже); счётчиков нет. Плавающая «Написать» на странице поста скрыта (`definePageMeta({ writeButton: false })`,
  `types/page-meta.d.ts`). Меню «…» — E07-05 (ниже).
- Меню «…» поста (E07-05, #53, [ADR-0053](adr/0053-post-actions-menu.md), FR-POST): `components/post/PostMenu.vue` в
  конце шапки — кнопка «Меню поста», на широком экране UiMenu, ниже 640 px — нижняя шторка. Состав —
  `utils/post-menu.ts` (`postMenuItems`): автору «Редактировать» и «Удалить», читателю и гостю «Пожаловаться»,
  «Скрыть», «Скопировать ссылку»; продвижения, донатов и подарков нет. Чей пост, решает сервер сайта: `lookUpPost`
  получает `account_id` сессии и отдаёт `own` (сам id на страницу не уходит). «Редактировать», «Пожаловаться» и
  «Скрыть» скрыты флагами `runtimeConfig.public.features` (`postEdit`, `postReport`, `postHide`, по умолчанию выкл.,
  `NUXT_PUBLIC_FEATURES_*` при старте) до своих задач (редактор опубликованного поста, E15-02 #159, E11-07 #112;
  потом — флаги E01-14 #118); их выбор отдаётся событием `action`. «Удалить» спрашивает «Удалить пост?», затем
  `DELETE /bff/post?id=…` (`server/routes/bff/post.delete.ts`, `deletePost` в `api/posts.ts`) → API; удалённый пост
  уводит в его блог с тостом «Пост удалён», сбой оставляет окно с тостом. Фейк API удаляет только написанные в тесте
  посты, посевные не удаляет (403).
- «Поделиться» поста (E07-07, #97, [ADR-0103](adr/0103-post-share.md), FR-POST): `components/post/PostShare.vue` на
  панели действий. Ниже 640 px при `navigator.share` — системное меню с канонической ссылкой и заголовком (закрытие
  молчит, другой отказ копирует ссылку); иначе (десктоп, телефон без API) — `UiMenu` над кнопкой (проп
  `side="above"`): «Скопировать ссылку», Telegram, ВКонтакте, X — простые адреса «поделиться» (`utils/share.ts`,
  `shareNetworks`) в новой вкладке с `noopener,noreferrer`, сторонний код не грузится. Ссылка — `canonicalUrl`
  (origin и путь `postPath`, без query и hash), ею же копирует пункт меню «…». Кнопки «Поделиться» на карточке ленты
  пока нет.
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
- Быстрый пост (E05-29, #334, [ADR-0055](adr/0055-quick-post.md), предложено, FR-EDITOR): форма
  `components/editor/QuickPostForm.vue` (текст без заголовка, до 10 фото с загрузкой и ошибкой, тема, «Развернуть»,
  «Опубликовать», `Ctrl+Enter`; гостю — «Войдите, чтобы написать пост.»), обвязка `components/feed/QuickPost.vue`
  над лентой на `/` и `/new` от 640 px, на телефоне — лист `components/feed/QuickPostSheet.vue` по плавающей
  «Написать» (`ShellWriteButton` с `quick`, ссылка `/write` остаётся). Текст и фото превращаются в документ
  ADR-0023 (`editor/quick-post.ts`: абзацы по пустой строке, перенос — `<br>`, фото блоками `image` после текста) и
  публикуются тем же путём, что из полного редактора (`composables/usePostPublishing.ts`, общий с `/write`).
  «Развернуть» кладёт документ и тему в `useQuickPost().handoff`, `/write` забирает их один раз (`PostEditor`
  `initial`). Видео не прикрепляется — его нет в схеме блоков M1 (ADR-0049, M3); передача теряется при перезагрузке
  до черновиков с автосохранением.
- Комментарии, сервер (E09-01, #57, [ADR-0048](adr/0048-comment-tree-and-api.md), FR-COMMENT, FR-TG-6): модуль
  `Platform.Modules.Comments`, схема `comments`. Комментарий хранит материализованный путь (`path`, UUID v7 в hex
  через точку, `COLLATE "C"`) и глубину; `CommentTree.OfPost` и `CommentTree.Branch` читают дерево и ветку одним
  запросом каждое по индексу `ix_comments_tree`. Глубина — настройка `Comments:MaxDepth` (по умолчанию 8). Вид
  `native` или `guest`: гостевой (будущий мост E19, `Comment.CreateGuest`) без аккаунта, с именем, ссылкой и
  источником, уникальным по `(source_channel, source_message)`; на него не отвечают. API: `GET
  /api/v1/posts/{postId}/comments`, `GET /api/v1/comments/{id}/branch` (открыты всем), `POST
  /api/v1/posts/{postId}/comments` (`confirmed-email`). Посты — через новый контракт `Platform.Modules.Posts.Contracts`
  (`IPostDirectory`). Демо-данные: по 6 комментариев под первыми 10 демо-постами. Не сделано здесь: форма (E09-03),
  форматирование текста; сортировка — E09-04, правка и удаление — E09-05, ниже.
- Дерево комментариев (E09-02, #58, [ADR-0054](adr/0054-comment-tree-levels.md), FR-COMMENT,
  [docs/ui/comments.md](ui/comments.md)): чтение дерева и ветки принимает `?levels=` (1…32, без него — всё), у каждого
  комментария `replies` — все ответы под ним, и скрытые (`CommentTree.Shown`). Клиент: `api/comments.ts`
  (`nestComments`, `withBranch`, `loadAuthors`), `composables/useComments.ts`, `components/comments/` (`CommentTree`,
  `CommentItem`, `PostComments`) под постом в `#comments`, только у опубликованного поста; читается в браузере после
  показа поста. Вложенные списки с `aria-level`, линия ветки — кнопка «Свернуть ветку» (`aria-expanded`), свёрнутая —
  «Развернуть · N ответов»; «Показать ещё N ответов» читает ветку (скелетон строки, «Повторить» при сбое). Состояния:
  скелетон трёх комментариев, ошибка с «Повторить», «Комментариев пока нет — будьте первым». Пост с 1000 комментариев
  проверен в браузере (самая длинная задача главного потока и сворачивание). Попутно: подписи панели действий поста на
  телефоне скрыты визуально (были шире 390 px и сдвигали страницу вбок, #361). Не сделано здесь: страница треда
  (E09-06), ссылка на комментарий, подсветка новых и действия; имена авторов читаются запросом на автора —
  один запрос на всех вынесен в #367.
- Форма комментария и ответа (E09-03, #59, [ADR-0056](adr/0056-comment-form.md), FR-COMMENT,
  [docs/ui/comments.md](ui/comments.md)): `components/comments/CommentForm.vue` — над деревом свёрнутое поле
  «Комментарий…» (раскрыто сразу, если комментариев нет или есть черновик), под комментарием — форма по «Ответить»
  (нет у гостевого, на последнем уровне `maxDepth` и у отправляемого). Отправка через `POST /bff/comments`
  (`server/routes/bff/comments.post.ts`, токен на сервере сайта): `useComments.send` ставит комментарий в дерево сразу
  приглушённым («Отправляется…», автор «Вы»), ответ сервера встаёт на его место; при сбое или отказе комментарий
  убирается, текст и причина возвращаются в форму (`withComment`, `withoutComment`, `replaceComment` в
  `api/comments.ts`). Черновик — `utils/comment-draft.ts` (localStorage, ключ на пост и родителя), удаляется после
  отправки. Гость получает окно входа, после входа открывается та же форма (`pendingActionWith` в
  `utils/pending-action.ts`). Фейк API отвечает на `POST /api/v1/posts/{id}/comments`, не сохраняя комментарий; текст
  «сбой» — ошибка 500. Тесты: `tests/comments/CommentForm.spec.ts`, `tests/comments/comment-send.spec.ts`,
  `e2e/comment-form.spec.ts` (оба профиля). Не сделано здесь: форматирование, упоминания (E09-11), вложения (E09-10),
  права комментирования (E09-14), прокрутка к новому комментарию (E09-07), форма у клавиатуры на телефоне (#369).
- Порядок комментариев (E09-04, #60, [ADR-0058](adr/0058-comment-sorting.md), FR-COMMENT,
  [docs/ui/comments.md](ui/comments.md)): `GET /api/v1/posts/{postId}/comments` принимает `?sort=popular|date`
  (без него — `popular`, другое слово — 400 с полем `sort`); `CommentTree.Sorted` переставляет ветки под постом после
  `Shown`, ответы внутри ветки остаются в порядке дерева, чтение ветки не сортируется. «Популярные» — больше ответов
  выше, при равенстве старше выше; «По дате» — новые сверху. Рейтинга у комментария пока нет — с E10-05 (#73) он
  встанет на место числа ответов. Клиент: переключатель «Популярные | По дате» (`role="group"`, `aria-pressed`) рядом
  с заголовком в `CommentTree.vue`, есть во время загрузки и ошибки, нет у поста без комментариев; выбор —
  `utils/comment-sort.ts` (localStorage `comments.sort`, один на все посты), `useComments.order` перечитывает дерево,
  старый ответ не рисуется поверх нового. Отправленный комментарий встаёт в конец «Популярных» и в начало «По дате»
  (`withComment(…, sort)`). Фейк API сортирует так же. Не сделано здесь: выбор на сервере в настройках аккаунта (нет
  настроек — E08-13 #102 про ленты), сортировка ответов внутри ветки.
- Правка и удаление комментария (E09-05, #61, [ADR-0059](adr/0059-comment-edit-and-delete.md), FR-COMMENT,
  [docs/ui/comments.md](ui/comments.md)): `PATCH /api/v1/comments/{id}` (автор, `confirmed-email`, пока не вышло окно
  `Comments:EditMinutes`, по умолчанию 15 минут; после — 403 `comments.edit_closed`, чужой — 403
  `comments.not_author`), `DELETE /api/v1/comments/{id}` (автор, в любое время), `GET
  /api/v1/posts/{postId}/comments/mine` (свои неудалённые). У комментария `edited_at`, `deleted_at` (миграция
  `EditAndDeleteComments`); в ответе `editedAt`, `editableUntil`, `deleted`. Удалённый с ответами — заглушка без автора
  и текста, на неё не отвечают; без ответов — удаляется из базы вместе с опустевшими заглушками выше
  (`CommentTree.Delete`). Клиент: свои комментарии — через `GET /bff/own-comments` (сервер сайта, id без
  `account_id`), правка и удаление — `PATCH`/`DELETE /bff/comments`; меню «…» своего комментария
  (`CommentMenu.vue`, `utils/comment-menu.ts`): «Редактировать» в окне, «Удалить» с подтверждением; форма правки
  `CommentEdit.vue` с отказом сервера под полем; «· изменено» у времени; заглушка «Комментарий удалён»
  (`withEdited`, `withDeleted`). Фейк API: комментарии anna — свои для вошедшего, их окно давно вышло, «Вторая ветка»
  изменена; правка и удаление отвечаются без сохранения. Тесты: `CommentChangeRulesTests`, `CommentApiTests`,
  `tests/comments/comment-edit.spec.ts`, `tests/comments/CommentEdit.spec.ts`, `e2e/comment-edit.spec.ts` (оба
  профиля). Не сделано здесь: остальные пункты меню «…» (ссылка, жалоба, блокировка, закрепление и удаление ветки
  автором поста), история правок, удаление модератором (E15).
- Дерево на телефоне и экран ветки (E09-06, #62, [ADR-0060](adr/0060-comment-branch-screen.md), FR-COMMENT,
  [docs/ui/comments.md](ui/comments.md)): на телефоне (уже 640 px) под последним прочитанным уровнем вместо «Показать
  ещё» — ссылка «Открыть ветку (N) →» на страницу `frontend/pages/branch.vue` (`/u/<блог>/<id>-<слаг>/comments/<id>` и
  `/<тема>/<id>-<слаг>/comments/<id>`, `branchPath`). Страница читает ветку `GET /api/v1/comments/{id}/branch` в
  браузере (`useComments` с `branchId`), рисует её тем же `CommentTree` (`branch`: без заголовка, порядка и формы под
  постом; ответить, изменить и удалить можно), уровни `aria-level` сохраняются, корень — у левого края; глубже — снова
  «Открыть ветку». «← К посту» ведёт на `<пост>#comments`. Ветка другого поста, удалённая или неверный адрес — «Ветка
  не найдена…» (`loadBranch` отличает 404); сбой — «Повторить». На десктопе ничего не изменилось. Тесты:
  `tests/comments/CommentTree.spec.ts`, `tests/comments/comments.spec.ts`, `e2e/comment-branch.spec.ts` (переход по
  ветке и ширина текста — в профиле mobile, экран ветки и его состояния — в обоих). Не сделано: «Выше по ветке» на
  экране ветки, код 404 у страницы с несуществующей веткой (ветка читается в браузере, страница отвечает 200),
  подсветка комментария по ссылке.
- Ссылка на комментарий (E09-07, #104, [ADR-0107](adr/0107-comment-link.md), FR-COMMENT,
  [docs/ui/comments.md](ui/comments.md)): постоянная ссылка — канонический адрес поста и `#comment-<id>`
  (`commentPath`). Время комментария — ссылка на него, «Скопировать ссылку» — первый пункт меню «…» у любого читателя
  (`utils/comment-menu.ts`, `commentUrl` без query). Новое чтение `GET /api/v1/comments/{id}/place` (уровень и предки
  из материализованного пути; 404, если комментария нет или пост не опубликован). `PostComments` по фрагменту — и при
  загрузке, и при смене фрагмента на той же странице, и на экране ветки — прокручивает к комментарию и подсвечивает
  его (`comment--highlighted`, `data-highlighted`); комментарий глубже прочитанного на десктопе подгружается на месте
  от ближайшего показанного предка (`nearestShown`, `more`), на телефоне открывается экран ветки предка на три уровня
  выше (`branchRootOf`). Ссылки ленты, уведомлений и виджета «Популярные комментарии» работают так же. Тесты:
  `CommentApiTests` (место комментария, 404), `tests/comments/comments.spec.ts`, `tests/comments/comment-edit.spec.ts`,
  `tests/comments/CommentLink.spec.ts`, `e2e/comment-link.spec.ts` и `e2e/feed-best-comment.spec.ts` в обоих
  профилях. Не сделано: «Выше по ветке» на экране ветки.
- Новые с прошлого визита (E09-08, #105, [ADR-0109](adr/0109-new-comments-since-last-visit.md), FR-COMMENT,
  [docs/ui/comments.md](ui/comments.md)): таблица `comments.comment_visits` (аккаунт, пост, время открытия; миграция
  `AddCommentVisits`) и `POST /api/v1/posts/{postId}/comments/visit` (вход обязателен; 404 для неопубликованного
  поста): отвечает `previousVisitAt` и `newIds` — чужие, не удалённые, не от заблокированных читателем комментарии
  после прошлого открытия в порядке дерева, не больше 500 — и переносит открытие на «сейчас» (`ON CONFLICT …
  GREATEST`). Первое открытие новых не даёт. Клиент: `PostComments` после показа дерева под постом спрашивает
  `POST /bff/comment-visit` (`server/routes/bff/comment-visit.post.ts`; гость не спрашивает, экран ветки тоже), ответ
  помнится на странице 10 минут (`useState`). Новые — `comment--new`, `data-new`: полоса `--color-accent` без фона и
  «новый» после времени (подсветка ссылки E09-07 — с фоном). «К следующему новому · N» (`comments-next-new`) прилипает
  под шапкой и идёт по новым в порядке показа (`newCommentsOrder`), на десктопе непрочитанные подгружаются на месте
  (`bring`, как ссылка), на телефоне — только показанные. Фейк API помнит открытия по адресу клиента. Тесты:
  `CommentVisitApiTests`, `tests/comments/NewComments.spec.ts`, `e2e/comment-new.spec.ts` в обоих профилях. Не
  сделано: счётчик новых на карточке ленты, отметки для гостя, чистка старых открытий.
- Вкладка «Комментарии» блога и темы (E09-15, #107, [ADR-0114](adr/0114-blog-comments-tab.md), FR-COMMENT,
  [docs/ui/blog.md](ui/blog.md), [docs/ui/topic.md](ui/topic.md)): `GET /api/v1/comments/blogs/{blogId}?cursor=&limit=`
  в модуле Comments (`BlogCommentEndpoints`, правила `BlogComments`) — у личного блога комментарии его владельца, у
  темы все комментарии под её опубликованными постами (гостевые тоже), новые сверху, курсор `(created_at, id)`.
  Удалённые не читаются, под неопубликованным постом и от заблокированных читателем отсеиваются (пачками, до 5 за
  чтение), блог заблокированного владельца пуст; неизвестный блог — 404 `comments.blog_not_found`. Новые контракты:
  `IPostDirectory.PublishedPostIdsAsync`, ссылка Comments → `Blogs.Contracts` (`IBlogDirectory`). Клиент:
  `useBlogComments` читает в браузере через `GET /bff/blog-comments` (`server/routes/bff/blog-comments.get.ts`) с
  токеном человека и после перезагрузки (не как #443), `BlogCommentList` — пост ссылкой на комментарий
  (`blogCommentPath`), автор, время, текст до 6 строк, «Показать ещё»; скелетон, ошибка с «Повторить», «Пока нет
  комментариев». Тема получила вкладки «Посты» и «Комментарии» дочерними страницами (`pages/[topic]/index.vue`,
  `pages/[topic]/comments.vue`, `nav` «Разделы темы»). Фейк API: `GET /api/v1/comments/blogs/{id}`. Тесты:
  `BlogCommentApiTests`, `BlogCommentsTests`, `tests/comments/BlogComments.spec.ts`, `e2e/blog-comments.spec.ts` в
  обоих профилях. Не сделано: реакции и число ответов у комментариев вкладки, вкладка «О теме», посты во вкладке
  «Посты» личного блога (по-прежнему пустое состояние).
- Комментарии в реальном времени (E09-09, #106, [ADR-0112](adr/0112-realtime-comments.md), FR-COMMENT,
  [docs/ui/comments.md](ui/comments.md)): модуль Comments после сохранения нового комментария публикует в тему
  `post:<id>` событие `comments.posted` (`CommentPostedEvent`: `commentId`, `postId`, `parentId`, без автора; сбой
  канала комментарий не отменяет). Клиент: `useComments` под постом слушает тему (экран ветки — нет), на событие
  спрашивает `GET /bff/comment-arrival` (`server/routes/bff/comment-arrival.get.ts`, `loadCommentArrival`: ветка
  комментария и свои комментарии с токеном человека), так что свой (и из другой вкладки), удалённый и от автора из
  чёрного списка читателя не приходят; событие своего комментария до ответа отправки ждёт его. Пришедшие ждут за
  «N новых» (`comments-arrived`, слева в прилипающей полосе нулевой высоты под шапкой, `aria-live`), заголовок
  «Комментарии N» считает их сразу; по нажатию они встают в дерево (`withArrived`: под показанным родителем, иначе
  добавляют ответ показанным предкам по `/place`) с отметкой «новый» и входят в «К следующему новому». Канал
  считает подписки на тему (`RealtimeChannel.subscribe`/`unsubscribe`): лайки и комментарии поста делят её.
  `RecordingPublisher` перенесён в `Platform.Testing`. Фейк API: `POST /__comment-arrival`. Тесты:
  `CommentRealtimeApiTests`, `tests/comments/LiveComments.spec.ts`, `tests/realtime.spec.ts`,
  `e2e/comment-live.spec.ts` в обоих профилях. Не сделано: подгрузка пропущенных за время обрыва связи, счётчик на
  карточке ленты, правка и удаление в реальном времени, экран ветки.
- Учёт просмотров (E07-06, #70, [ADR-0071](adr/0071-post-view-counting.md), FR-POST): в модуле Posts счётчики
  `view_count` и `impression_count` у поста, таблицы `posts.post_views` (пост, вид, зритель, сутки UTC) и
  `posts.view_salts` (секрет суток для псевдонима гостя, прошлые удаляются). `POST /api/v1/posts/{id}/open`,
  `/api/v1/posts/impressions` (1–50 карточек), `/api/v1/posts/{id}/reading` (1–7200 с) всегда отвечают 204; зритель —
  аккаунт или HMAC сети и строки браузера; боты (`ViewRules.IsBot`), предзагрузка и автор не считаются; черновик — 404.
  `PostView.viewCount` — в посте и карточке ленты («1 234 просмотра» с иконкой глаза). Сайт шлёт отчёты на
  `/bff/views` (`fetch` с `keepalive`; `composables/useViews.ts`): открытие чужого опубликованного поста, карточку,
  половина которой секунду была на экране (пачкой раз в 3 с и при уходе), и время видимости страницы — только с
  согласием «Аналитические». Учёт за внутренним `IViewCounter`; события в аналитическую
  базу (E16-18, #217) подключаются там; уборка старых строк `post_views` отложена до них.
- Счётчики в Redis со сбросом в базу (E10-09, #75, [ADR-0083](adr/0083-redis-counters-with-batched-flush.md)):
  общий буфер записи `IWriteBuffer` в `Platform.Caching` (Redis, фейк в памяти): `AppendOnceAsync` — метка и запись
  одним скриптом, `ClaimAsync` — пакет с арендой 1 мин (брошенный пакет берётся снова), `CompleteAsync`. Просмотры:
  `BufferedViewCounter` вместо `DatabaseViewCounter`, `ViewFlusher` пакетно пишет `post_views` (`ON CONFLICT DO
  NOTHING`, счётчик растёт на число вставленных строк, время чтения — `GREATEST`), так что повтор пакета ничего не
  меняет; `ViewFlushService` — каждые `Posts:Views:FlushSeconds` (5 с) и при остановке. `viewCount` отстаёт на один
  сброс. Лайки и рейтинг остаются в транзакции базы (ADR-0081): ответ лайка и событие точны. Для стенда нужен Redis с
  сохранением на диск.
- Рейтинг поста и комментария (E10-05, #73, [ADR-0081](adr/0081-post-and-comment-rating.md), FR-REACT): вес вида
  реакции — справочник `ReactionWeights` в `backend/src/Modules/Reactions/Rating.cs` («Нравится» — 1, веса не
  отрицательные); рейтинг цели — сумма её реакций по весам, хранится в `reactions.ratings` (строки с нулём нет, индекс
  по `post_id`; миграция `CreateRatings` перенесла прежние лайки). `PUT`/`DELETE` лайка идут в транзакции под
  `pg_advisory_xact_lock` на цель и пересчитывают её рейтинг до коммита. Контракт `IReactionRatings`
  (`CommentRatingsAsync`, `PostRatingsAsync`). «Популярные» в дереве комментариев: рейтинг, потом число ответов, потом
  старший; лучший комментарий ленты — по рейтингу (блок по-прежнему показывает число лайков); фейк API сортирует так
  же. API и клиенты не менялись. Не сделано: карма (E10-06, #173) и сортировка лент по рейтингу постов — читают тот же
  контракт.
- Закладки на посты и комментарии (E10-07, #108, [ADR-0113](adr/0113-bookmarks.md), FR-REACT): таблица
  `reactions.bookmarks` в модуле Reactions (ключ — цель и аккаунт, в строке `post_id` и автор цели `target_author_id`,
  миграция `AddBookmarks`), `BookmarkEndpoints`: `PUT`/`DELETE /api/v1/posts/{postId}/bookmark` и
  `/api/v1/comments/{id}/bookmark`, `GET /api/v1/posts/{postId}/bookmarks` (пост и его комментарии),
  `GET /api/v1/bookmarks/posts?ids=…` (посты карточек, не больше 100, иначе 400 по полю `ids`); всё — только с
  аккаунтом, почта не обязательна; ответ `{bookmarked, count}`, `count` — только автору цели. Клиент: `api/bookmarks.ts`,
  BFF `server/routes/bff/bookmarks.get.ts` и `bookmark.post.ts`, одно хранилище `useBookmarks` на приложение
  (`provide` в `app.vue`, `bookmarksKey`), `components/bookmarks/BookmarkButton.vue` в `PostActions` и в ряду счётчиков
  `FeedCard` (через `FeedView`), пункт «В закладки» / «Убрать из закладок» в меню «…» комментария (`comment-menu.ts`,
  автор видит «· N»); гость — окно входа, после входа закладка ставится сама (`bookmark:<цель>:<id>`). Тесты:
  `BookmarkApiTests`, `BookmarkTests`, `tests/bookmarks/bookmarks.spec.ts`, `e2e/bookmarks.spec.ts`. Раздел «Закладки»
  — следующий пункт.
- Раздел «Закладки» (E04-12, #109, [ADR-0116](adr/0116-bookmarks-section.md), FR-PROFILE, [docs/ui/blog.md](ui/blog.md)):
  контракт `IBookmarkList` в `Reactions.Contracts` (`BookmarkList`: закладки одного вида одного человека, новые сверху,
  курсор `BookmarkPosition (created_at, target_id)`); вкладка «Посты» — `GET /api/v1/feeds/bookmarks` в Posts
  (`BookmarkFeedEndpoints`, `BookmarkedPostView {postId, bookmarkedAt, card}`, `card` — `FeedPostView` или `null` у
  неопубликованного поста), вкладка «Комментарии» — `GET /api/v1/comments/bookmarks` в Comments
  (`BookmarkCommentEndpoints`, `BookmarkedCommentView {commentId, postId, bookmarkedAt, comment}`, `comment` —
  `BlogCommentView` или `null` у удалённого). Только с аккаунтом и только свои, `private, no-store`. Клиент:
  `pages/bookmarks.vue` (`/bookmarks`, `nav` «Разделы закладок», гостю — «Войдите…»), вкладки
  `pages/bookmarks/index.vue` и `pages/bookmarks/comments.vue`, `useBookmarkSection` через
  `GET /bff/bookmark-section?tab=` (`server/routes/bff/bookmark-section.get.ts`), `BookmarkSectionList` — пост карточкой
  `FeedCard` с кнопкой закладки, комментарий — пост ссылкой на комментарий, автор, время, текст; удалённое — заглушкой;
  скелетон, ошибка с «Повторить», пусто, «Показать ещё». Фейк API: `GET /api/v1/feeds/bookmarks` и
  `/api/v1/comments/bookmarks`, у каждого клиента в закладках с начала удалённые пост и комментарий. Тесты:
  `BookmarkSectionApiTests`, `tests/bookmarks/BookmarkSection.spec.ts`, `e2e/bookmark-section.spec.ts`. Не сделано:
  заглушку нельзя убрать из раздела (`DELETE …/bookmark` отвечает 404 для удалённой цели).
- Репосты (E10-08, #110, [ADR-0117](adr/0117-reposts.md), FR-REACT): репост — строка `posts.posts` с
  `repost_of_post_id` и `repost_text` (до 1000 символов; миграция `AddReposts`, уникальный индекс `ux_posts_repost`
  по неудалённым репостам). `POST /api/v1/posts/{id}/reposts` `{text?}` (вход и подтверждённая почта; свой пост — 403
  `posts.repost_own_post`, повтор — 409 `posts.already_reposted`, черновик и удалённый — 404, длинный текст — 400;
  репост репоста репостит оригинал) публикует репост сразу в личном блоге и событие `PostReposted`
  (Posts.Contracts) через outbox; `GET /api/v1/posts/{id}/reposts` — репосты поста новые сверху, без людей из
  чёрного списка читателя. Репост в ленте блога и в «Моей ленте» с `original` в `FeedPostView`; в «Свежем»,
  «Популярном», топах и поиске репостов нет; удаление оригинала скрывает репосты в лентах и на `GET /posts/{id}`;
  `PUT` репоста — 409 `posts.repost_not_editable`. Notifications: тип `repost` (`PostRepostedNotificationsConsumer`,
  ключ `repost:<пост>:<человек>`). Клиент: блок «Репосты» на странице опубликованного поста (`PostRepostsSection`,
  `PostReposts`, `useReposts`, `server/routes/bff/repost.post.ts`, `reposts.get.ts`: список, «Сделать репост» с
  текстом для вошедшего читателя чужого поста, скелетон, ошибка с «Повторить», пусто, «Показать ещё»), карточка
  репоста в `FeedCard` (репостнувший, «сделал репост», текст, оригинал в рамке, счётчики и закладка оригинала),
  страница репоста — текст и «Открыть оригинал», строка уведомления «сделал репост вашего поста». Фейк API хранит
  репосты по адресу клиента, у «Привет, мир» — репост Бориса. Тесты: `RepostRulesTests`, `RepostApiTests`,
  `NotificationRulesTests`, `NotificationGenerationTests`, `tests/reposts/reposts.spec.ts`, `e2e/reposts.spec.ts`.
  Не сделано: вкладка «Посты» личного блога на клиенте ещё пуста (E04-02), так что в блоге репост виден только через
  API; счётчика репостов на карточке нет.
- Выбор реакции из набора (E10-02, #87, [ADR-0101](adr/0101-reaction-choice.md), FR-REACT): `ReactionKind` — семь
  видов по картинкам набора (`ReactionKinds.CodeOf`/`Parse`), все весят 1 (`ReactionWeights`). API лайков заменён
  API реакций (`ReactionEndpoints`): `GET /api/v1/posts/{postId}/reactions`, `PUT` с `{kind}` и `DELETE
  /api/v1/posts/{postId}/reaction` и `/api/v1/comments/{id}/reaction`, ответ `ReactionsView {reactions: [{kind,
  count}], mine}`. Ставится только включённый в справочник вид с картинкой (иначе 400, поле `kind`); своя реакция
  другого вида меняется в той же транзакции под блокировкой цели, рейтинг пересчитывается там же; `ReactionPut` — только
  для новой реакции, `reactions.changed` — после любой перемены, со всеми видами. `ILikeCounts` считает реакции всех
  видов. Клиент: `api/reactions.ts` (состояние `{counts, mine}`, `chosen`, `rowOf`, `changeReaction`; `api/likes.ts`
  удалён), BFF `server/routes/bff/reactions.get.ts` и `reaction.post.ts`, `useLikes` (имя прежнее: `choose`,
  `toggle`, `loadSet`, набор читается при первом открытии), `ReactionRow` — виды картинками `ReactionIcon`, кнопка
  «Выбрать реакцию» с набором над рядом: наведение на неё (300 мс), долгое нажатие пальцем на вид или на неё (500 мс),
  нажатие; Escape и нажатие вне закрывают; загрузка, ошибка с «Повторить», пусто. Гость выбирает — окно входа, после
  входа выбранный вид ставится сам (`like:<пост>:<цель>:<вид>`). Тесты: `ReactionApiTests` (смена, снятие любого вида,
  400 вне набора и выключенный вид, гонка выборов), `tests/reactions/reactions.spec.ts`, `tests/likes/likes.spec.ts`,
  `e2e/reaction-choice.spec.ts`. Не сделано: список оценивших (E10-04), шторка набора на телефоне, горячая клавиша.
- Набор реакций (E02-12, #86, [ADR-0095](adr/0095-reaction-set.md), FR-REACT, FR-UI), **частично**: справочник  `reactions.reaction_set` (`code`, `title`, `position`, `enabled`; `ReactionSetEntry` в
  `backend/src/Modules/Reactions/ReactionSet.cs`), набор по умолчанию — справочные данные миграции `AddReactionSet`:
  «Нравится» (`like`), «Тепло», «Улыбка», «Браво», «Полезно», «Удивительно», «Грустно». Коды с картинками — закрытый
  список `ReactionGraphics.Codes`; `GET /api/v1/reactions/set` без входа отдаёт включённые по порядку и без кодов,
  которых нет в сборке. Графика — Fluent Emoji Flat (Microsoft, MIT), SVG в `brand/reactions` с `LICENSE`
  рядом; `utils/reaction-graphics.ts`, `components/reactions/ReactionIcon.vue` (картинка фиксированного размера, а не
  системный эмодзи) и `ReactionSet.vue` (загрузка, ошибка с «Повторить», пусто); витрина `/reactions` (читает набор
  в браузере, строка об авторстве), адрес `reactions` зарезервирован для тем. Интеграционные тесты Reactions
  сохраняют таблицу при очистке (`KeepTables`). Не сделано: правка справочника в админке — E15-12 (#168, после входа и
  ролей E15-10/E15-11). Выбор, смена и снятие реакции из набора сделаны в E10-02 (#87, ADR-0101).
- Ряд реакций со счётчиками (E10-03, #72, [ADR-0072](adr/0072-reaction-row-and-live-counts.md), FR-REACT): модуль
  Reactions после настоящей перемены лайка публикует в тему `post:<id>` событие `reactions.changed`
  (`{target, targetId, reactions: [{kind, count}]}`, никого не называет; повтор и отказ — без события; сбой канала
  не отменяет изменение). Клиент: `frontend/api/reactions.ts` (виды `reactionKinds`, сортировка, разбор события),
  компонент `components/reactions/ReactionRow.vue` (варианты `panel` и `compact`, по убыванию количества, своя —
  акцентом и `aria-pressed`, числа по языку), `useLikes` подписан на тему открытого поста и меняет счётчики, оставляя
  свою отметку. С E10-02 (#87) видов семь, событие несёт все. Не сделано: перечитывание счётчиков после переподключения канала, живой
  счётчик в карточке ленты.
- Лайк на посты и комментарии (E10-01, #63, [ADR-0061](adr/0061-likes.md), FR-REACT): новый модуль
  `backend/src/Modules/Reactions` со схемой `reactions` (таблица `reactions`, ключ — цель и аккаунт: одна реакция
  человека на цель, вид пока только `Like`) и контракт `Comments.Contracts` (`ICommentDirectory`, реализует модуль
  Comments). API: `PUT`/`DELETE /api/v1/posts/{postId}/like` и `/api/v1/comments/{id}/like` (ставит и снимает,
  повтор ничего не меняет, ответ — `{count, liked}`; ставить — с подтверждённой почтой), `GET
  /api/v1/posts/{postId}/likes` — лайки поста и его комментариев одним запросом, без входа тоже. Свой пост и
  комментарий — 403 `reactions.own_content`, гостевой комментарий — 403 `reactions.guest_comment`, черновик, удалённый
  и неизвестный — 404. Клиент: `composables/useLikes.ts` (страница поста читает лайки один раз для панели и дерева,
  экран ветки — свои), BFF `server/routes/bff/likes.get.ts` и `like.post.ts`; «Нравится» в плавающей панели
  (E07-04) теперь работает и показывает счётчик, в дереве — сердечко со счётчиком перед «Ответить»; свой пост и
  комментарий — только счётчик, у гостевого и удалённого лайка нет; нажатие гостя открывает окно входа, после входа
  лайк ставится сам. Не сделано (свои задачи E10): набор реакций, список оценивших, рейтинг и карма (и пороги кармы
  на реакции), счётчики в карточке ленты, уведомление о реакции, сортировка «Популярные» по рейтингу.
- Лучший комментарий под карточкой ленты (E08-15, #335, [ADR-0062](adr/0062-feed-best-comment.md), FR-FEED): элемент
  ленты `FeedPostView` получил `bestComment` (`FeedCommentView`: id, личный блог автора или имя гостя, начало текста
  до 300 знаков с «…»), `null` у поста без показанных комментариев. Выбирает модуль Comments
  (`ICommentDirectory.BestShownAsync`, правило — `BestComments.Pick`): больше лайков — лучше, при равенстве раньше
  написанный, удалённый не выбирается. Лайки он спрашивает у модуля Reactions через новый контракт
  `Reactions.Contracts` (`ILikeCounts.CommentLikesAsync`, по индексу `post_id`), блоги авторов комментариев читаются
  тем же запросом, что блоги авторов постов: страница ленты стоит одинаковое число запросов при любом размере.
  Клиент: `components/feed/FeedBestComment.vue` — аватар, имя и до трёх строк текста с многоточием, весь блок —
  ссылка `<пост>#comment-<id>` (`feedCommentPath`); страница поста после чтения дерева прокручивает к комментарию, а
  комментарий глубже прочитанного (на телефоне — глубже трёх уровней) открывает на экране его ветки
  (`PostComments.vue`, `commentOfHash`). Тесты: `FeedBestCommentApiTests`, `BestCommentsTests`,
  `tests/feed/FeedCard.spec.ts`, `e2e/feed-best-comment.spec.ts`. Не сделано: «скрытые модерацией» — модерации
  комментариев ещё нет, фильтр добавится в `BestShownAsync`; рейтинг (E10-05) заменит число лайков в одном месте;
  подсветки комментария по ссылке нет; блок пока последний в карточке — строка счётчиков (E08-06) встанет над ним.
- Виджет «Популярные комментарии» (E08-12, #101, [ADR-0105](adr/0105-popular-comments-widget.md), FR-FEED):
  `GET /api/v1/comments/popular?limit=1..10` (`PopularCommentEndpoints` в модуле Comments, по умолчанию 5) —
  показанные нативные комментарии, написанные за последние сутки под опубликованными постами, с рейтингом выше нуля:
  рейтинг по убыванию, при равенстве более ранний. Гостевые комментарии из Telegram не входят, даже с рейтингом. О постах
  модуль спрашивает новый `IPostDirectory.PublishedHeadlinesAsync` (блог, заголовок, слаг одним запросом), о рейтингах —
  `IReactionRatings`; окно суток читается по частичному индексу `ix_comments_day` (миграция `AddCommentsDayIndex`).
  Выбор общий: `PopularCommentsCache` держит запас из 30 в Redis минуту, после кэша для вошедшего убираются авторы из
  его чёрного списка (`IBlockList`). Клиент: раскладка `default` кладёт в `ShellAside` `PopularCommentList` (аватар и
  имя блога автора через `loadAuthors`, три строки текста, заголовок поста; пункт — ссылка
  `/u/<blogId>/<id-slug>#comment-<id>`), данные — `composables/usePopularComments.ts` через `/bff/popular-comments`,
  только пока правая колонка на экране (`asideQuery`, от 1240 px), заново при входе и выходе; состояния `ShellWidget`.
  Фейк API отдаёт два комментария и убирает Бориса для вошедшего. «Интересные блоги» по-прежнему пусты.
- Лента «Свежее» (E08-01, #54, [ADR-0043](adr/0043-fresh-feed.md), FR-FEED): `GET /api/v1/feeds/fresh?cursor&limit`
  (`FreshFeedEndpoints` в модуле Posts, тег Feeds) — опубликованные посты от новых к старым по `(published_at, id)`,
  курсор `FreshFeedCursor`, сравнение строк `(published_at, id) < (…)` по частичному индексу `ix_posts_fresh_feed`;
  элемент — `FeedPostView` (E08-02). Страница `/new` (`frontend/pages/new.vue`, `components/feed/FeedView.vue`,
  `api/feeds.ts`, `composables/useFeed.ts`): первая страница рендерится на сервере, следующие подгружаются из браузера
  сами при прокрутке. «Показать новые» — E08-08 (ниже), фильтр рейтинга — E08-07 (ниже).
- Кэш лент (E08-09, #71, [ADR-0080](adr/0080-feed-cache.md), FR-FEED): `FeedPageCache` в модуле Posts держит готовые
  страницы «Свежего» и ленты блога (`/api/v1/feeds/blogs/{blogId}`) в `IDistributedCache` под ключом
  `feeds:page:<поколение>:<лента>:<limit>:<курсор>`; повторный запрос не идёт в базу, несуществующий блог (404) не
  кэшируется. Публикация, правка и удаление опубликованного поста через API пишут новое общее поколение
  (`feeds:generation`) — страницы всех лент сбрасываются сразу. Срок жизни страницы — 30 секунд по `TimeProvider`:
  столько могут отставать имя блога, лучший комментарий и счётчик просмотров. Без Redis страница читается из базы.
  «Популярное» кэшируется так же, своим именем ленты; «Моя лента» у каждого своя и не кэшируется (ADR-0089).
- Чёрный список (E11-06, #81, [ADR-0091](adr/0091-black-list.md), FR-SUB). Таблица `preferences.blocked_accounts`, API `/api/v1/preferences/me/blocks` (`GET`, идемпотентные `PUT`/`DELETE /{accountId}`, себя нельзя, не больше 1000), контракт `Preferences.Contracts.IBlockList`, тот же сервис — источник `IFeedExclusionSource`. «Свежее», «Популярное» и лента блога применяют исключения читателя после общего кэша (`Posts.ReaderExclusions`), лучший комментарий заблокированного убирается с карточки. Дерево и ветка комментариев помечают комментарии заблокированных `authorBlocked`; заблокированный получает `403 comments.blocked` на ответ заблокировавшему и на комментарий под его постом. Стирание по `account.deleted` — в обе стороны. Сайт: раздел «Приватность и блокировки» (`/settings/privacy`, `BlockList.vue`, `/bff/blocks*`) — список с именем блога и датой, «Разблокировать», состояния «загрузка», «пусто», «ошибка» с «Повторить»; комментарий заблокированного в дереве свёрнут: «Комментарий скрыт: вы заблокировали автора.» и «Показать». Кнопки: профиль блога отдаёт `accountId` владельца личного блога (у темы `null`) и `blocked` для читателя (Blogs читает `IBlockList`); вошедший на чужом личном блоге видит «Заблокировать» (`BlogBlock.vue`) или, если уже заблокировал, строку «Вы заблокировали этого человека…» и «Разблокировать»; в меню «…» чужого поста и чужого комментария — «Заблокировать автора»; блокировка всегда через окно подтверждения `components/blocks/BlockDialog.vue` с тостом, после блокировки из комментария все комментарии автора в дереве сворачиваются сразу. Форма комментария на `403 comments.blocked` пишет «Автор заблокировал вас: отвечать ему и комментировать его посты нельзя.», оставляет текст и не отправляет снова. Запрет писать в личных сообщениях — с мессенджером (E13-08, #193).
- Лента уведомлений (E12-02, #83, [ADR-0093](adr/0093-notification-feed-and-counter.md), FR-NOTIFY): модуль
  Notifications зарегистрирован и в хосте API (`AddNotificationsModule`, `NotificationEndpoints`): `GET /api/v1/notifications/me`
  — свои уведомления по убыванию `(created_at, id)` с курсором, действующее лицо — личный блог из
  `IBlogDirectory.CardsAsync`; `GET …/unread` — `{ unread }`; `POST …/read` с `{ ids }` (1–100) помечает свои
  непрочитанные и отвечает новым числом. Сайт: `useUnreadNotifications` (через `/bff/notifications/unread`),
  колокольчик в `ShellHeader` с числом («99+») и панелью (`Esc`, клик вне, «Все уведомления →»), страница
  `/notifications`, счётчик на вкладке нижней панели; `NotificationList` — скелетон, пусто, ошибка с «Повторить»,
  «Показать ещё» по курсору, отметка показанных. Не сделано: группировка и «Прочитать все» (E12-03), живой счётчик
  (E12-04), заголовок поста и цитата в строке.
- «Моя лента» (E08-04, #80, [ADR-0089](adr/0089-my-feed.md), FR-FEED): `GET /api/v1/feeds/my` только вошедшему —
  опубликованные посты блогов и тем из `IBlogSubscriptions` и все посты владельцев личных блогов подписки, по дате,
  курсор «Свежего». Исключения читателя — `Posts.Contracts.IFeedExclusionSource` (скрытые посты, блоги и темы,
  заблокированные авторы): лента объединяет все зарегистрированные источники; пока их нет, ничего не исключается —
  источники добавят E11-06 (#81) и E11-07 (#112). Общий кэш лент не используется, ответ `private, no-store`. Сайт:
  `/my` (первая страница на сервере сайта, дальше `/bff/my-feed`), гостю — «Войдите» с окном входа, пустая лента —
  до шести тем и «Найти авторов в «Свежем»». Не сделано: сортировка «по популярности» (#412) и вкладки лент на телефоне (#413) —
  до `/my` на телефоне из интерфейса пока не дойти.
- «Горячесть» и лента «Популярное» (E08-05, #74, [ADR-0086](adr/0086-hotness-and-popular-feed.md), FR-FEED, только
  сервер): `HotnessFormula` — `sign(очки) · log10(max(|очки|, 1)) + (published_at − 2026-01-01) / DecayHours`, очки —
  рейтинг (`IReactionRatings`), видимые комментарии (новый `ICommentDirectory.ShownCountsAsync`) и открытия по весам.
  Коэффициенты — настройки `Posts:Hotness:*` (CONFIG.md; значения по умолчанию — предложение), читаются перед каждым
  проходом. `HotnessService` раз в `IntervalSeconds` (60 с) запускает `HotnessCalculator` под
  `pg_try_advisory_xact_lock`: при смене коэффициентов (строка `posts.hotness_formula`) пересчитывает все счета из
  сохранённых входных данных, затем перечитывает посты окна (`WindowDays`, 7 дней) и посты без счёта и пишет в
  `posts.post_hotness` только изменившиеся, убирает строки неопубликованных. `GET /api/v1/feeds/popular` — по убыванию
  `(score, id)`, курсор `PopularFeedCursor`, элемент `FeedPostView`, кэш `FeedPageCache` (лента `popular`, общее
  поколение; у `GetOrReadAsync` — перегрузка с позицией текстом). Тестовые хосты выключают проходы
  (`Posts:Hotness:IntervalSeconds = 0`). Вкладки «Популярное» на сайте ещё нет.
- Топ за день, неделю, месяц, год и всё время (E08-06, #98, [ADR-0104](adr/0104-top-periods.md), FR-FEED; мера и
  периоды — предложение): `GET /api/v1/feeds/top/{period}` (`TopFeedEndpoints`, без входа), `period` — `day`, `week`,
  `month` (30 дней), `year` (365 дней), `all`, скользящие от «сейчас» по `TimeProvider`; другое значение — 400
  `validation_failed`. Посты, опубликованные в периоде, по рейтингу из `posts.post_hotness.rating` (копия прохода
  «горячести»), при равенстве новее выше, курсор `TopFeedCursor(Rating, Id)`. Кэш — `FeedPageCache`, лента
  `top:<период>`. Рейтинг постов старше окна прохода (7 дней) замирает — топы за месяц и дольше видят у них рейтинг
  первой недели. Сайт: `pages/top/[period].vue` (`/top/day` … `/top/all`, первая страница на сервере, незнакомый период —
  404), заголовок «Популярное», переключатель `TopPeriodSwitch` (ссылки, текущий — `aria-current`), пустой период —
  «За этот период постов пока нет» со ссылкой на «Свежее»; фейк API считает периоды от 23 ноября 2026. Пункт
  «Горячее» в переключателе и сама лента `/popular` — E08-05a (#406).
- Фильтр «Свежего» по рейтингу (E08-07, #99, [ADR-0106](adr/0106-fresh-rating-filter.md), FR-FEED; набор порогов и
  хранение выбора гостя — предложение): `GET /api/v1/feeds/fresh?minRating=` `-10`, `5` или `10` (без параметра — все;
  другое — 400 `validation_failed` с полем `minRating`; значения — `FreshFeedRating` в `Posts.Contracts`). Рейтинг — из
  `posts.post_hotness.rating`, пост без прохода считается с 0; порядок и курсор прежние; кэш — лента `fresh:<порог>`.
  Выбор вошедшего — `preferences.account_preferences.fresh_min_rating`: `GET /api/v1/preferences/me` отдаёт
  `feeds.freshMinRating`, `PUT /api/v1/preferences/me/feeds` меняет (сайт — `/bff/preferences/feeds`); сервер сайта
  читает его перед отрисовкой `/new`. Выбор гостя — `localStorage` `fresh-min-rating`, применяется после гидратации.
  Сайт: на `/new` заголовок «Свежее» и `FreshRatingFilter` (кнопки с `aria-pressed`), `useFreshRating`; пустая лента
  при пороге — «С таким рейтингом постов пока нет…» и «Показать все». Фейк API фильтрует по лайкам других (ни один пост
  не доходит до +5). Порог выбирается и в разделе `/settings/feeds` (E08-13, #102, ADR-0110).
- Плашка «Показать новые» (E08-08, #100, [ADR-0108](adr/0108-show-new-posts.md), FR-FEED; опрос вместо события
  SignalR и плашка только на «Свежем» — предложение): `composables/useNewPosts.ts` раз в 60 секунд, пока вкладка видна,
  и при возвращении во вкладку перечитывает первую страницу `/api/v1/feeds/fresh` с тем же порогом (из кэша лент) и
  считает посты выше самого нового показанного (`newerPosts`, `newerOverflow`, `showNewer` в `api/feeds.ts`); в ленту
  они сами не попадают. `components/feed/NewPostsBanner.vue` на `/new` — «Показать новые» с числом («N+», если новая вся
  первая страница), место нулевой высоты, плашка прилипает под шапкой поверх карточек и ленту не сдвигает,
  `aria-live`. Нажатие ставит новые посты сверху (при переполнении — лента заново с первой страницы) и прокручивает
  вверх. Смена порога сбрасывает насчитанное. `useFeed` теперь отдаёт `posts` и `cursor`. Фейк API:
  `POST /__fresh-arrival?client=` публикует пост для одного адреса клиента. Сервер и API не менялись; SignalR не
  используется (хаб только для вошедших, ADR-0019).
- Жесты ленты на телефоне (E08-14, #103, [ADR-0111](adr/0111-pull-to-refresh-and-hiding-header.md), FR-FEED, раздел 5
  ТЗ; предложено): `components/feed/PullToRefresh.vue` оборачивает `/new`, `/my`, `/top/<период>` и посты темы
  (`TopicFeed`); касание от самого начала страницы раскрывает отметку на половину пути пальца (не больше 96 px,
  `utils/pull.ts`), отпущенная дальше 64 px вызывает обновление страницы: на «Свежем» — `useNewPosts.check` и `show`
  (новые посты сверху, показанные остаются; несработавшая лента читается заново), на остальных — `retry` `useFeed`.
  Пока лента на экране, `overscroll-behavior-y: contain` у `<html>` выключает собственный жест браузера.
  `composables/useHideOnScroll.ts` в `ShellLayout`: сдвиг больше 8 px вниз прячет шапку, вверх — возвращает, ближе 64 px
  к началу она видна всегда; действует только в телефонной оболочке (уже 925 px), шапка с фокусом клавиатуры остаётся.
  `--shell-header-offset` у `.shell` — высота видимой шапки; от неё считают `top` плашка «Показать новые» и «К
  следующему новому». Сквозной тест `e2e/feed-gestures.spec.ts` касается через `Input.dispatchTouchEvent` Chromium;
  `e2e/shell.spec.ts` теперь ждёт, что телефонная шапка после прокрутки спрятана.
- Настройки лент (E08-13, #102, [ADR-0110](adr/0110-feed-settings.md), FR-FEED; главная лента переадресацией и
  сортировка, оставленная #412, — предложение): `preferences.account_preferences.home_feed` (`Popular`, `Fresh`, `My`;
  по умолчанию `Popular`) и `autoplay_video` (по умолчанию `true`), миграция `AddHomeFeedAndAutoplay`.
  `GET /api/v1/preferences/me` отдаёт `feeds.homeFeed` и `feeds.autoplayVideo`; `PUT /api/v1/preferences/me/feeds`
  берёт `freshMinRating` всегда, а `homeFeed` и `autoplayVideo` — если они есть в запросе (иначе остаются прежние;
  неизвестная лента — 400 с полем `homeFeed`). Сайт: `/settings/feeds` — `FeedSettingsForm` (главная лента, порог
  «Свежего», галочка автопроигрывания; сохранение сразу, тост, откат при сбое, скелет и «Повторить»); `/` у вошедшего
  читает настройки и отвечает 302 на `/new` или `/my` (`homeFeedPath` в `api/preferences.ts`), «Популярное» и гость
  остаются на `/`. Фейк API хранит весь раздел. Нет здесь: применение автопроигрывания (видео в лентах — E06-06, #127),
  «Скрывать просмотренные» (E08-10, #184), сортировка «Моей ленты» (#412).
- Карточка поста в ленте (E08-02, #55, [ADR-0052](adr/0052-feed-post-card.md), предложено): элемент ленты —
  `FeedPostView { post, author, topic }`, где `author` — личный блог автора, `topic` — тема поста (`FeedBlogView`:
  id, тип, имя, никнейм, `avatarSources`); их читает один пакетный `IBlogDirectory.CardsAsync` на страницу
  (`FeedPostViews` в модуле Posts, `BlogCard` в `Blogs.Contracts`, который теперь ссылается на `Media.Contracts`).
  `components/feed/FeedCard.vue`: аватар и имя автора, тема, дата — ссылка на пост, заголовок — ссылка на пост,
  блоки через `PostBlocks`. Обрезка — `feedPreview` в `api/feeds.ts`: не больше 3 блоков и ни одного блока после
  того, на котором текст достиг 600 знаков (первый блок — всегда); короткий пост целиком, у обрезанного — «Показать
  полностью», раскрывает остальное на месте. Ссылка на пост — канонический адрес (`feedItemPath`: тема, иначе блог
  автора), без известного блога — `/u/<id блога>/…`. Счётчиков, реакций, лучшего комментария и меню «…» на
  карточке ещё нет — свои задачи (FR-REACT, E08-06, E07-05).
- Бесконечная подгрузка и возврат на место (E08-03, #56, [ADR-0051](adr/0051-feed-infinite-scroll-and-return.md),
  предложено): метка в конце списка под `IntersectionObserver` (запас 800 px) просит следующую страницу, пока она
  грузится — «Загрузка следующих постов…»; сбой оставляет посты и даёт «Повторить», сам запрос не повторяется; без
  `IntersectionObserver` — кнопка «Показать ещё». `useFeed` хранит посты и курсор по записи истории (поле
  `feedEntry` в `history.state`, `useState('feed:snapshots')`, не больше 10 записей): «назад» из поста рисует ту же
  ленту сразу и без запроса к API, роутер ставит прежнюю прокрутку; новый заход читает ленту заново сверху. Снимок
  живёт до перезагрузки вкладки. Другие ленты берут тот же `useFeed`.
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
  очистки кэша CDN при удалении (превью и цвет — E06-03, ниже).
- Компонент изображения (E06-04, #47, [ADR-0040](adr/0040-image-component.md), FR-MEDIA): `UiImage` — единственный
  способ показать картинку. Место занято по `width`/`height` (атрибуты и `aspect-ratio`) до загрузки, `srcset` с
  `w`-дескрипторами из источников и `sizes` — браузер берёт ширину под плотность экрана, `loading="lazy"` и
  `decoding="async"` (для первого экрана — `eager` с `fetchpriority="high"`). Состояния в `data-state`: `empty`,
  `loading`, `loaded`, `error`; пустая и сломанная картинка — заглушка того же размера. `UiAvatar` показывает
  картинку через `UiImage`. API отдаёт источники картинки контрактом `IMediaLibrary.ImageSourcesAsync(ids, variants)`
  (тип `ImageSource { url, width }`); первый потребитель — `Profile.avatarSources` (квадраты 64, 128 и 192 px WebP),
  шапка блога. Витрина `/ui` показывает все состояния на картинке, которую рисует маршрут сайта
  `/showcase/landscape-{320,640,1280}.svg` из палитры бренда. Нет здесь: `<picture>` с AVIF.
- Раздел настроек (E04-08, #69, [ADR-0070](adr/0070-settings-sections-and-account-preferences.md)): `/settings` —
  список разделов (первый экран телефона, вход из «Меню»), разделы «Блог» `/settings/blog`, «Ленты» `/settings/feeds`,
  «Основные» `/settings/general`, «Уведомления» `/settings/notifications`, «Аккаунт и вход» `/settings/account`,
  «Настройки cookie» `/settings/cookies` (порядок — `settingsNav` в `frontend/utils/navigation.ts`). На десктопе
  разделы — в правой колонке (`definePageMeta({ settingsNav: true })`), уже 1240 px у раздела «←» к списку. Модуль
  `Preferences` (схема `preferences`, таблица `account_preferences`): `GET /api/v1/preferences/me`,
  `PUT /api/v1/preferences/me/general` (тема `system`, `light`, `dark`); браузер ходит через `/bff/preferences`.
  Плагин `preferences.client.ts` у вошедшего применяет тему аккаунта при старте, выбор в «Основных» и в переключателе
  левого меню сохраняется в аккаунт, сбой — тост и прежняя тема. Нет здесь: «Скрывать просмотренные» в «Лентах» (E08-10; остальное
  в «Лентах» — E08-13, ADR-0110) и «Уведомления» (E12-05) — пока только место раздела; тема с сервера без мигания при первой отрисовке (открытый вопрос
  ADR-0070); «Приватность и блокировки» появится в списке со своей задачей.
- Размытое превью и доминирующий цвет (E06-03, #68, [ADR-0067](adr/0067-image-preview-and-dominant-color.md),
  FR-MEDIA): при загрузке картинки `ImagePreviews` (SkiaSharp, MIT; JPEG и WebP декодируются сразу уменьшенными,
  поворот по EXIF, больше 25 Мпикс без масштаба — без превью) считает WebP до 16 px по длинной стороне и средний по
  непрозрачности цвет `#rrggbb`; они хранятся в `media.media_files` (`preview_color`, `preview_image`, миграция
  `AddImagePreviews`). Нечитаемая картинка и не картинка принимаются без превью. Контракт
  `IMediaLibrary.ImagePreviewsAsync(ids)` → `ImagePreview { color, image: data:image/webp;base64,… }`; в API —
  `PostView.imagePreviews` (по id файла, рядом с `images`) и `preview` в ответе `POST /api/v1/media`. `UiImage`
  принимает `preview`: пока картинка грузится, коробка залита цветом и размытой SVG-фильтром копией (встроенный стиль
  из HTML сервера, `data-preview`), после загрузки и при ошибке — снимается; посты в ленте и на странице передают
  превью через `PostBlocks`. Витрина `/ui` — пример «Размытое превью до загрузки». Нет здесь: пересчёта картинок,
  загруженных до миграции, превью аватаров и обложек (их DTO не несут `imagePreviews`), превью в редакторе.
- Страница блога (E04-02, FR-PROFILE, [ui/blog.md](ui/blog.md)): `/u/<никнейм>`, у блога без никнейма — `/u/<UUID>`;
  `@ник`, другой регистр и UUID блога с никнеймом ведут 301 на один адрес; `id123` никнеймом не считается (404).
  Вкладки — дочерние страницы `/u/<блог>` («Посты») и `/u/<блог>/comments` («Комментарии»), переключаются без
  перезагрузки. Первая отрисовка — на сервере с токеном читателя (гость получает готовую страницу), дальше браузер
  спрашивает `/bff/blog-profile` (сервер сайта идёт в API с токеном). Владелец узнаётся по `registeredAt` в ответе
  (его видит только владелец, ADR-0025) и видит «На сайте с …» и кнопки «Ред.» и «Настройки» (ведут в
  `/settings/blog`, E04-03) и выключенную «Статистика» с пометкой «появится позже». Пункт меню «Профиль» (`/profile`) ведёт через
  `/bff/own-blog` в свой блог. Состояния: скелетон, «Блог не найден» (404), «Не удалось загрузить блог» с
  «Повторить»; вкладки пустые («Здесь пока нет постов», у владельца — «Написать первый пост»; «Пока нет
  комментариев»), пока нет постов (E05) и комментариев (E07). Нет здесь: счётчиков (обложка — E04-04, аватар — E04-03),
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
  остальные части «Аккаунт и вход» — провайдеры (устройства и «Выйти со всех устройств» сделаны в E03-09, токен
  доступа завершённой сессии отвергается в пределах 30 секунд, ADR-0078).
- Смена почты с подтверждением (E03-06, #90, [ADR-0020](adr/0020-email-normalization-and-confirmation.md), FR-AUTH):
  API прежний (`GET/PUT /api/v1/account/email`, `POST /api/v1/account/email/confirmation`). Подтверждённая почта
  остаётся, пока не введён код с нового адреса; когда код её заменил, `AccountEmails.ConfirmAsync` кладёт в outbox
  письмо `email-changed` на прежний адрес (в той же транзакции, что и смена; нового адреса в письме нет). Первое
  подтверждение и неверный код писем на прежний адрес не шлют. Адрес, подтверждённый другим аккаунтом, — 409
  `email_taken` без id, имени и почты владельца. Сайт: раздел «Почта» в `/settings/account`
  (`components/settings/EmailChangeForm.vue`: загрузка, ошибка с «Повторить», «нет почты», ожидающий адрес и поле
  кода; вызовы — `api/email.ts` через `server/routes/bff/email.get.ts`, `email.put.ts`, `email-confirmation.post.ts`).
  Тесты — `Email/AccountEmailTests` (Identity), снимок `email-changed` в `Platform.Mail.Tests`,
  `frontend/tests/settings/EmailChangeForm.spec.ts`, сквозной `frontend/e2e/email-change.spec.ts` (фейк API хранит
  почту по адресу клиента, `taken@example.com` занят).
- Устройства и «Выйти со всех устройств» (E03-09, #92, [ADR-0078](adr/0078-device-sessions-and-sign-out-everywhere.md),
  FR-AUTH): таблица `identity.account_sessions` (миграция `AddAccountSessions`) — строка на каждый вход у провайдера;
  её id — `sid` в cookie `identity.session` и во всех токенах. Название устройства — браузер и система из
  `User-Agent` (`DeviceNames`), заголовок и IP не хранятся; активность двигают вход и `/connect/token`. API:
  `GET /api/v1/account/sessions` (`current` у сессии запроса), `DELETE …/{id}` (404 `session_not_found` и на чужую),
  `POST …/sign-out-others` (`{ ended }`). Завершённая сессия удаляется: `/connect/authorize` сбрасывает cookie,
  `/connect/token` — `invalid_grant`, токен доступа отвергает `EndedSessionValidation` (ответ помнится 30 секунд на
  экземпляр). Сброс пароля удаляет все сессии. Cookie без `sid` получают сессию при следующем authorize, refresh-токены
  без `sid` больше не обновляются. Сайт: раздел «Устройства» в `/settings/account`
  (`components/settings/SessionList.vue`: загрузка, ошибка с «Повторить», «других устройств нет», подтверждение в
  окне; вызовы — `api/sessions.ts` через `server/routes/bff/sessions.get.ts`, `sessions/[id].delete.ts`,
  `sessions/sign-out-others.post.ts`). Тесты — `SignIn/AccountSessionTests`, `SignIn/DeviceNamesTests` (Identity),
  `frontend/tests/settings/SessionList.spec.ts`, сквозной `frontend/e2e/sessions.spec.ts` (фейк API хранит три
  устройства по адресу клиента). Не сделано: фоновая очистка строк старше 30 дней (#398).
- Вход через внешних провайдеров (E03-07, #91, [ADR-0115](adr/0115-external-sign-in.md), FR-AUTH): Яндекс ID, VK ID,
  Google и Telegram Login. Браузер идёт по ссылке `GET /api/v1/sign-in/external/{provider}?returnUrl=…[&link=true]`;
  API кладёт попытку (`state`, верификатор PKCE, куда вернуться) в зашифрованную cookie `identity.external` на 10
  минут и отправляет к провайдеру; `…/{provider}/callback` сверяет её, провайдер доказывает человека, API ставит
  сессию провайдера и возвращает на `returnUrl` (сайт входит обычным потоком кода). Провайдеры — за
  `IExternalSignInProvider` (`External/OAuthProviders.cs`: Яндекс, VK, Google с PKCE; `TelegramLoginProvider` —
  проверка HMAC подписи бота и свежести 10 минут; Telegram возвращает на страницу сайта `/auth/telegram`, она передаёт
  фрагмент `#tgAuthResult` в callback). Аккаунт (`ExternalSignIns`): привязанная пара — её аккаунт; иначе почта, за
  которую ручается провайдер, — аккаунт с этой подтверждённой почтой (пара привязывается) или новый с ней; без почты
  (Telegram) — отказ `email_required`, аккаунтов без почты не бывает. `link=true` привязывает к аккаунту сессии
  провайдера. Ошибки — переход на `/login` (при привязке — на `returnUrl`) с `?externalError=`. Провайдер включён, если
  заданы его ключи `Identity:External:*` (docs/CONFIG.md); `GET /api/v1/sign-in/external` называет включённые;
  `Identity:External:Fake=true` (Development, Test) заменяет все четыре фейком со страницей входа внутри API. Лимит
  `identity.sign-in.external`. Сайт: `components/auth/ExternalSignIn.vue` (кнопки под формой входа на `/login` и в
  окне входа; без провайдеров блока нет; загрузка и ошибка), `api/external-sign-in.ts`, `pages/auth/telegram.vue`.
  Тесты — `External/ExternalSignInTests` (Testcontainers: новый аккаунт по почте, вход по паре, привязка по почте,
  отказ без почты, Telegram по привязке, привязка и её отказы, отказ у провайдера, чужой `state`, повтор, адреса
  возврата), `External/ExternalProviderTests` (без Docker: запросы и разбор ответов Яндекса, VK, Google на заглушке
  HTTP, подпись Telegram, включение настройками), `frontend/tests/auth/ExternalSignIn.spec.ts`, сквозной
  `frontend/e2e/external-sign-in.spec.ts` (фейк провайдера в `e2e/fake-api.mjs`). Раздел «Способы входа» в настройках и
  письма об изменении — #119 (ниже). Не сделано: боевые ключи — у владельца (открытые вопросы); регистрация только
  через Telegram — вопрос владельцу.
- Привязка и отвязка провайдеров в настройках (E03-08, #119, ADR-0115 п. 10): в «Аккаунт и вход»
  (`/settings/account`) раздел `components/settings/SignInMethods.vue` — включённые провайдеры и привязанные (с датой
  привязки), «Привязать» — переход на вход провайдера с `link=true` и возвратом в настройки (ошибка `externalError`
  показывается в разделе), «Отвязать» — `DELETE /api/v1/account/identities/{id}` через `/bff/identities*`
  (`api/identities.ts`); последний способ входа не отвязывается (409, раздел объясняет почему); загрузка, ошибка с
  «Повторить», пусто. Сообщение `email_required` («привяжите провайдера в настройках») теперь ведёт в работающий раздел.
  Письма: `ExternalIdentities` при каждой привязке (в том числе автопривязке по почте при входе) и отвязке шлёт на
  подтверждённую почту письмо `sign-in-method-linked` / `sign-in-method-unlinked` (провайдер и время) через outbox
  модуля в той же записи; без изменений (повтор, отказ) — письма нет; аккаунту без почты писать некуда; первая
  привязка нового аккаунта письма не шлёт. Тесты — `External/SignInMethodLetterTests` (Testcontainers), снимки писем
  в `Platform.Mail.Tests`, `frontend/tests/settings/SignInMethods.spec.ts`, сквозной `e2e/sign-in-methods.spec.ts`.
- Внешние идентичности (E18-04, #88, [ADR-0098](adr/0098-external-identities.md), FR-ID-4): таблица
  `identity.external_identities` (миграция `AddExternalIdentities`) — пара «провайдер, субъект», уникальная по паре
  и по «аккаунт, провайдер»; ключи провайдеров — `ExternalProviders` (`yandex`, `vk`, `google`, `telegram`, формат
  ключа для будущих проектов). `ExternalIdentities`: `LinkAsync` (повтор — без изменений, чужая пара — отказ, не
  перенос), `FindAccountAsync`, `ListAsync`, `UnlinkAsync` — отказ, если не останется способа входа (подтверждённая
  почта или другая идентичность), под блокировкой строки аккаунта. Журнал `identity.external_identity_log` пишется в
  той же записи. Удаление аккаунта уносит привязки и журнал каскадом. API: `GET /api/v1/account/identities`
  (`{ id, provider, linkedAt }`, без субъекта), `DELETE …/{id}` (404 `external_identity_not_found`, 409
  `last_sign_in_method`). Событий о привязках нет. Тесты — `External/ExternalIdentityTests` (Identity). Вход через провайдеров — #91 (ниже,
  ADR-0115: аккаунтов без почты он не создаёт, так что вопрос событий для них снят). Раздел «Способы входа» на сайте и
  письма — #119 (выше).
- События аккаунта как контракт (E18-05, #89, [ADR-0087](adr/0087-account-events-contract.md), FR-ID-5):
  `account.created.v1`, `account.email_verified.v1`, `account.updated.v1` (`changes: ["email"]`),
  `account.deleted.v1` — записи в `Identity.Contracts/AccountEvents.cs`; имя события — и тип сообщения
  (`urn:message:<имя>`), и exchange. В событиях только `accountId` и `occurredAt`: ни почты, ни паролей, ни токенов.
  Их собирает `AccountEventPublisher` из изменений аккаунтов перед `SaveChanges` и кладёт в outbox Identity
  (`AccountActivated` убран в #409: Blogs читает `account.created`, а до #424 дочищает очередь `account-activated`
  записью `LegacyAccountActivated`, ADR-0087 п. 9). Удаление — служба `AccountDeletions`
  (без API: запрос, срок ожидания и восстановление — E03-11, #170); неактивированная регистрация удаляется без
  события. Схемы — [docs/contracts](contracts/README.md), строятся из кода (`UPDATE_CONTRACTS=1`), тест сверяет их и
  правила совместимости. Первый модуль, стирающий своё: Preferences (`AccountDeletedConsumer`, inbox в схеме
  `preferences`, миграция `AddPreferencesInbox`, модуль подключён к хосту воркеров). Остальные модули — в E03-11.
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
  провайдера. Redis backplane включается вместе с кэшем (`Cache:Provider=Redis`, каналы `platform:realtime`); порядок событий
  гарантирован только внутри темы ([ADR-0064](adr/0064-realtime-event-order.md), #316). Клиент —
  `frontend/realtime/channel.ts`, `plugins/realtime.client.ts` (`@microsoft/signalr` 10, загружается только у
  вошедшего), `useRealtimeTopic`; переподключение без конца (0, 2, 5, 10, затем каждые 30 секунд) с новым билетом и
  повторной подпиской, прекращается при выходе. Стенд: Caddy отдаёт `/hubs/*` API; `nuxt dev` проксирует `/hubs`.
  Тесты — `RealtimeIntegrationTests` (`Platform.Api.IntegrationTests`, Redis: своё событие и не чужое, подписка на
  пост, отказ в чужом аккаунте и неизвестной теме, без билета, поддельный и просроченный билет, билет заголовком,
  билет не покупает билет, backplane между двумя экземплярами; «лишнего не пришло» доказывает маяк в той же теме,
  `SettleAsync`, #379), `RealtimeSignInTests` (настоящие токены провайдера:
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
  Каталог сидера (E01-19, #287) наполняет пустую базу модулями Identity, Blogs, Posts и Comments: 20 подтверждённых
  аккаунтов, их личные блоги, 6 тем, 40 опубликованных постов (по два на аккаунт: чётный в личном блоге, нечётный
  в теме) и по 6 комментариев деревом под первыми 10 постами. Модули ссылаются друг на друга только через
  `SeedData.Id(kind, n)` и константы контрактов (`IdentityDemo`, `BlogsDemo`, `PostsDemo`); согласованность ссылок
  и повторный запуск без добавлений проверяет `MvpDemoSeedTests`. Медиа в сидере пока нет. Правила —
  [ADR-0009](adr/0009-demo-data-and-factories.md).
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
  колонка прилипают под шапкой), `ShellHeader` (знак, поиск — открывает `ShellSearch` из слота `search` раскладки, E14-02, ADR-0097, «Написать» → `/write`,
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
- Общие состояния экранов (E02-07, #64, [ADR-0057](adr/0057-shared-states-and-error-pages.md), предложено, FR-UI):
  `UiSkeleton` (строка текста по токену размера, круг аватара, блок по высоте или пропорциям) и `UiState` (пусто,
  ошибка с `role="alert"` и «Повторить», «не найдено», «доступ закрыт») в `frontend/components/ui/` и на витрине
  `/ui`; `FeedCardSkeleton` повторяет раскладку карточки ленты — три на первой загрузке, один под списком при
  подгрузке. `frontend/error.vue` в обычном каркасе: 404 «Страница не найдена», остальное — «Что-то пошло не так» с
  «Повторить». Пост и блог при серверном рендеринге отдают 404 для ненайденного и 500 при сбое API
  (`utils/response-status.ts`). Плашка «Нет подключения» над содержимым (`ShellOfflineBanner`, `useOnline()`,
  `plugins/network.client.ts`); при возвращении сети перечитываются только данные со сбоем. Глобальные стили
  переехали из `app.vue` в `frontend/assets/base.css`. Пост, блог, комментарии, редактор и виджеты пока рисуют
  состояния своей разметкой; страниц «доступ закрыт» ещё нет.
- Арабский текст и письмо справа налево в контенте (E02-14, #67, [ADR-0068](adr/0068-rtl-content.md), предложено,
  FR-UI): абзацы, заголовки, подписи, цитаты и списки постов, заголовок поста и карточки ленты, текст комментария и
  лучшего комментария — `dir="auto"`, поля редактора, комментария и быстрого поста тоже. Арабские (и еврейские)
  фрагменты внутри LTR-строки обрамляются RLI…PDI (`frontend/utils/bidi.ts`: `isolateRtlHtml` после
  `cleanInlineHtml`, `isolateRtlText` для простого текста), поэтому цифры и пунктуация вокруг цитаты остаются на своих
  местах; блок, начинающийся с арабского, не трогается. Шрифт — Noto Naskh Arabic 400 и 700
  (`@fontsource/noto-naskh-arabic`, `frontend/assets/fonts.css`, `unicode-range` только арабского поднабора), первым в
  `--font-family-sans` и `--font-family-serif`. Схема документа и серверный санитайзер не менялись: `dir` не хранится.
  Сквозной тест — арабский пост фейкового API (`e2e/rtl.spec.ts`).
- Локализация интерфейса (E02-10, #65, [ADR-0063](adr/0063-interface-localization.md), предложено, FR-UI): все
  строки сайта — в словаре `frontend/i18n/locales/ru.ts` (группы по модулям), `t('ключ', параметры)` из
  `frontend/i18n` со слотами `{name}`, `{brand}` из `brand/brand.json` и формами множественного числа по
  `Intl.PluralRules`; текст с разметкой — `I18nText`. Даты, счётчики и числа в текстах форматируются по языку
  (`formatDate`, `formatNumber`: «1 000»), `lang` страницы — из `frontend/i18n/config.ts`. Второй язык — файл словаря и
  строка в `i18n/locales/index.ts`, недостающее берётся из русского. Правило ESLint `i18n/no-raw-text`
  (`frontend/eslint/i18n.mjs`) в `pnpm lint` отклоняет текст в шаблонах и кириллицу в коде; вне проверки — словари,
  тесты, витрины `/ui`, `/tokens`, `/brand` и маршруты `frontend/server/`. Клиент `admin/` не локализован;
  переключателя языка нет.
- Согласие на cookie (E02-13, #66, [ADR-0066](adr/0066-cookie-consent.md), предложено, FR-UI): баннер
  `ShellCookieBanner` в `app.vue` внизу каждой страницы, пока нет выбора, — «Только необходимые» и «Разрешить все»
  равного веса и «Настроить»; не модальный, фокус не забирает, рисуется только на клиенте после чтения выбора
  (на десктопе — над правой колонкой, на телефоне — во всю ширину над нижней панелью). Выбор — в необходимой cookie
  `cookie_consent` (`v1.analytics-0` или `-1`, год, `path=/`, `SameSite=Lax`, `Secure` на https); новая версия
  значения снова показывает баннер. Изменить — `/settings/cookies` (открыта гостям; служебная ссылка «Настройки
  cookie» в левом меню и на экране «Меню»). Необязательная категория одна — «Аналитические»; аналитики на сайте нет,
  будущий код с необязательными cookie стартует только через `whenConsented('analytics', start, cookies)` из
  `frontend/composables/useCookieConsent.ts`, при отзыве его cookie удаляются. Текст баннера — нейтральная заглушка
  до юридических документов. Сквозные тесты по умолчанию ставят выбор фикстурой `cookieChoice`,
  `e2e/cookie-consent.spec.ts` её выключает.
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
| Сервер, модульные | 314 (из них 7 — репосты E10-08: 6 в `RepostRulesTests` (репост опубликован сразу с текстом без блоков, пустой текст, не правится, кто что может репостнуть), 1 в `NotificationRulesTests` (уведомление автору оригинала один раз на человека); 2 — снимки писем о способах входа E03-08 в `TemplateTests`; из них 3 — закладки E10-07 в `BookmarkTests`; из них 12 — онбординг E03-10 в `OnboardingRulesTests`: имя 2–25 графем, онбординг один раз, у темы его нет, демо-блоги пройдены; из них 2 — почта модуля через его outbox E01-06b в `ModuleOutboxTests`: модули не берут обычные публикатор и почту, нарушение находится; из них 1 — `WorkersReadTheirOwnSettingsFiles` в `Platform.Configuration.Tests`: хост воркеров читает свой файл настроек, #411; из них 4 — набор реакций E02-12 в `ReactionSetDefaultsTests`: у каждой реакции по умолчанию есть картинка, коды и места различны, «Нравится» первая, незнакомый код без картинки; из них 2 — лента уведомлений E12-02 в `NotificationViewRulesTests`; из них 6 — чёрный список E11-06: 4 в `BlockRulesTests` (Preferences), 2 в `SharedFeedExclusionsApiTests` (Posts, `ReaderExclusions.Apply`: лучший комментарий заблокированного, скрытые посты и блоги); из них 10 — смена никнейма E04-05 в `NicknameChangeRulesTests` (первый никнейм бесплатно, смена начинает 30 дней, тот же никнейм — не смена, удержание старого 90 дней, зарезервированные и стоп-список); из них 24 — контракт событий аккаунта E18-05 в `Contracts/AccountEventContractTests` (Identity, без Docker): схема в `docs/contracts` описывает каждое из 4 событий, код с ней совместим, файл на каждое событие, имя и exchange с версией, нет почты, паролей и токенов, тело на шине как в схеме, 6 правил совместимости; из них 4 — веса реакций E10-05 в `ReactionWeightsTests`: вес у каждого вида и не отрицательный, «Нравится» — 1, сумма по весам, неизвестный вид и отрицательное число — ошибка; из них 2 — рейтинг в порядке комментариев E10-05 в `CommentSortTests`: «Популярные» сначала по рейтингу ветки, «По дате» рейтинг не читает; из них 1 — снимок письма `email-changed` E03-06 в `Platform.Mail.Tests`; из них 8 — настройки аккаунта E04-08 в `PreferenceRulesTests`: известные и неизвестные темы, имена в обе стороны, новый аккаунт с темой «как в системе»; из них 6 — лучший комментарий ленты E08-15 в `BestCommentsTests`: самый лайкнутый и ранний из равных, равное время — по id, повтор кандидата, пусто, короткий текст целиком, обрезка с «…» и без разрыва суррогатной пары; из них 4 — лайк E10-01 в `LikeRulesTests`: чужой контент, свой, гостевой комментарий, лайк поста под этим постом; из них 12 — правка и удаление комментария E09-05 в `CommentChangeRulesTests`: окно правки и его конец, удаление в любое время, только автор и не гостевой, правка с пометкой времени и пустой текст, заглушка над ответами без текста и ответов на неё, удаление без ответов с опустевшими заглушками выше, настройка `Comments:EditMinutes` и 3 неверных значения; из них 5 — порядок комментариев E09-04 в `CommentSortTests`: «Популярные» по числу ответов и старшинству, равные — старший первым, «По дате» — новые сверху с ответами на месте, счёт скрытых уровнем ответов, пустое дерево; из них 8 — блок «Фото» E05-06 в `Platform.Modules.Editor.Tests`: канонический блок, отказы по файлу, размеру и лишнему полю, предел подписи; из них 12 — пустой пост E05-09 в `Platform.Modules.Editor.Tests` (`HasContent`); из них 2 — снимки писем `password-reset` и `password-changed` E03-05 в `Platform.Mail.Tests`; `Platform.Modules.Editor.Tests`: 106 — из них 8 — вложенность списка E05-04: уровни пунктов по порядку, список без `levels` — плоский, 6 видов плохих уровней с местом ошибки; схемы блоков E05-08: все блоки версии 1 и канонический вид, 25 видов плохого блока и документа с местом ошибки, повтор id, неподдерживаемая версия, предел числа ошибок, проверка документа схемами его версии, 35 векторов XSS, разрешённое форматирование, текст и почтовые ссылки, API: очищенный документ, 401 без входа, 400 с путями ошибок; `Platform.Mail.Tests`: 15 — шаблоны писем E01-12 и письмо подтверждения почты E18-03: снимок каждого шаблона, экранирование в HTML, незаполненное поле, неизвестный шаблон, выбор культуры с откатом на русский, название из `brand/brand.json` и его отсутствие в шаблонах; `Platform.Architecture.Tests`: 46 — границы модулей на сборках платформы, модули-образцы с нарушениями, разрешённые и запрещённые ссылки, схемы; из них 15 — FR-ID-1 E18-01: почта, пароли и внешние идентификаторы только в `identity`, нет внешних ключей между схемами, таблицы только в своей схеме, нарочно нарушающий контекст, распознавание имён столбцов; `Platform.Api.Tests`: 13 — курсорная пагинация и часы; `Platform.Configuration.Tests`: 12, проверка настроек и файлы секретов; `Platform.Seeding.Tests`: 5, зерно, идентификаторы и фабрики) |
| Сервер, интеграционные | 949 (из них 7 — репосты E10-08: 6 в `RepostApiTests` (в блоге и «Моей ленте» с оригиналом и не в «Свежем», список по страницам, удаление оригинала скрывает, права и проверки, репост репоста и отказ в правке, событие `PostReposted`), 1 в `NotificationGenerationTests`; 5 — раздел «Закладки» E04-12 в `BookmarkSectionApiTests`: порядок по времени добавления у постов и комментариев, только свои и не гостю, удалённое — без содержимого на своём месте, страницы по курсору; из них 2 — письма о способах входа E03-08 в `External/SignInMethodLetterTests`; из них 14 — вход через провайдеров E03-07: 8 в `External/ExternalSignInTests` на Testcontainers, 6 в `External/ExternalProviderTests` без Docker; из них 10 — закладки E10-07 в `BookmarkApiTests`; из них 17 — вкладка «Комментарии» E09-15: 11 в `BlogCommentApiTests` (блог и тема, удалённые и неопубликованные, страницы и курсор, отсев с полной страницей, чёрный список, пусто, 404 и 400) и 6 в `BlogCommentsTests` (правила отбора и курсора без базы); из них 3 — комментарии в реальном времени E09-09 в `CommentRealtimeApiTests`: событие нового комментария и ответа, его JSON, отказы без события; из них 4 — открытие комментариев E09-08 в `CommentVisitApiTests`: первое без новых, новые в порядке дерева без своих, удалённых и заблокированных, открытие переносится, 401 гостю и 404; из них 2 — место комментария E09-07 в `CommentApiTests`: уровень и предки, 404 неизвестного и удалённого поста; из них 15 — фильтр «Свежего» E08-07: 10 в `FreshFeedRatingApiTests` (каждый порог, пост без прохода, черновик, страницы без повторов, кэш по порогам, чтение без входа, 3 неизвестных порога, значения порогов) и 5 в `PreferenceApiTests` (порог на всех устройствах, 3 неизвестных порога, гость); из них 13 — виджет «Популярные комментарии» E08-12: 5 в `PopularCommentsTests` без Docker и 8 в `PopularCommentApiTests`: лучшие нативные за сутки с постом, гостевой с рейтингом не входит, старше суток не входит, кэш на минуту, заблокированный автор только для заблокировавшего, `limit`, ошибка `limit`, пусто; из них 21 — топ за период E08-06: 10 в `TopPeriodsTests` без Docker и 11 в `TopFeedApiTests`: посты каждого периода по рейтингу, черновик не в топе, пустой период, кэш по периодам, страницы без повторов, чтение без входа, незнакомый период и неверная страница; из них 6 — онбординг E03-10 в `OnboardingApiTests`: шаги и подписки на темы, пропуск всего, пропущенный шаг не меняет блог, ошибки полей, старые и чужие аккаунты его не видят, без входа 401; из них 1 — `accountId` и `blocked` профиля блога E11-06 в `ProfileApiTests`; из них 10 — выбор реакции E10-02: 8 в `ReactionApiTests` (смена вида одним шагом, снятие любого вида, 4 случая вне набора, выключенный вид, гонка выборов) и 2 в `ReactionWeightsTests` (вес 1 у всех видов, коды видов и картинок); из них 6 — списки подписчиков и подписок E04-06 в `SubscriptionListApiTests`: порядок, курсор без потерь, числа шапки равны строкам, отметки смотрящего, пусто, 404 и 400; из них 13 — внешние идентичности E18-04 в `ExternalIdentityTests`: уникальность пары и гонка привязок, журнал, последняя возможность входа и гонка отвязок, удаление аккаунта, API владельца и чужих; из них 20 — подсказки поиска E14-02: `SearchSuggestionsApiTests` — гость получает блоги и посты по началу слова с текущим именем блога, заблокированные скрыты только у заблокировавшего, пустой ответ, пустой и длинный запрос — ошибка проверки; `SearchSuggestionRulesTests` — обрезка запроса, отрывок поста, видимость блога; `OpenSearchIndexTests` — префикс последнего слова на настоящем OpenSearch; `SearchDocumentTests` — документ читается так же, как записан; из них 2 — переход Blogs на `account.created.v1` #409 в `PersonalBlogActivationTests`: старое `AccountActivated` из очереди даёт один блог вместе с двойником, тип сообщения и обменник старого события; из них 2 — почта второго модуля хоста E01-06b в `ModuleMailTests`: письмо ждёт в outbox своей схемы и уходит после её коммита, без коммита пропадает; из них 2 — набор реакций E02-12 в `ReactionSetApiTests`: гость читает набор по умолчанию по порядку, код без картинки не предлагается; из них 7 — лента уведомлений E12-02 в `NotificationApiTests`: порядок и курсор, отметка и счётчик, чужая лента, неверные `ids` (2), неверный `limit`, гость; из них 13 — чёрный список E11-06: 5 в `BlockApiTests` (список, идемпотентность, себя нельзя, чужой список, гость, контракт), 1 в `AccountDeletedErasureTests` (стирание в обе стороны), 3 в `SharedFeedExclusionsApiTests` («Свежее» и лента темы после общего кэша, курсор короткой страницы), 4 в `CommentBlockApiTests` (свёрнутые для заблокировавшего, запрет ответа и комментария под постом, разблокировка); из них 12 — смена никнейма E04-05: 11 в `ProfileNicknameApiTests` (смена и старый адрес, раньше 30 дней — 409, тот же никнейм, занятый блогом и темой, зарезервированный и не по форме, удержание 90 дней, возврат своего старого, гость — 401), 1 в `TopicCreateApiTests` (тема не берёт удержанный никнейм, потом берёт); из них 13 — темы в меню и каталог E11-02: 5 в `TopicCatalogRulesTests` — поиск без лишних пробелов, длина поиска, экранирование `%` и `_`, 8 в `TopicCatalogApiTests` — порядок по подписчикам без личных блогов, поиск по названию и адресу без регистра, страницы с курсором без потерь при равных числах, отказ длинного поиска и неверной страницы, популярные темы гостя, короткий список меню, подписанные темы по названию, популярные для человека без подписок на темы; из них 9 — «Моя лента» E08-04 в `MyFeedApiTests`: посты подписок и людей по дате по страницам, черновики и удалённые вне её, скрытые и заблокированные исключены фейковым источником, пустая лента без подписок, лента одного не видна другому и не кэшируется, гостю 401, неверная страница — 400 (2), объединение исключений; из них 3 — события аккаунта E18-05: 2 в `Contracts/AccountEventPublishingTests` (Identity: создание, подтверждение почты, смена почты и удаление публикуются по порядку, в теле с шины нет почты, хэша пароля и токенов, повторное удаление — false; неактивированный аккаунт удаляется без события), 1 в `AccountDeletedErasureTests` (Preferences на настоящем хосте воркеров: `account.deleted` стирает настройки аккаунта, повтор и чужой аккаунт без вреда); из них 33 — «горячесть» и лента «Популярное» E08-05: 22 без Docker в `HotnessFormulaTests` (8 эталонных примеров формулы, правило десятикратного затухания, округление, веса, формула без окна, настройки по умолчанию и заданные, 8 неверных значений), 11 в `HotnessApiTests` на PostgreSQL (порядок ленты и черновик вне её, повторный проход ничего не пишет, пишутся только изменившиеся посты, пост вне окна не перечитывается, старый пост без счёта его получает, смена коэффициентов на ходу пересчитывает все счета, неверные коэффициенты ничего не меняют, удалённый пост уходит из ленты, страницы по курсору без повторов, чтение без входа, 3 ошибки страницы); из них 39 — создание темы E11-11: 26 в `TopicCreationRulesTests` — стоп-список по началу слова, без регистра и с ё как е, зарезервированные адреса и сверка с `frontend/pages`, длина названия, настройки и 4 неверных значения, 13 в `TopicCreateApiTests` — тема создана и принадлежит создавшему, вход и подтверждённая почта, дубли названия и адреса темы и блога, 8 запрещённых и неверных названий и адресов, длинное описание, лимит на аккаунт; из них 6 — счётчики в Redis E10-09: 3 в `WriteBufferTests` (пакеты, повтор брошенного пакета после аренды, записи переживают новое подключение; Redis и фейк), 3 в `ViewFlushTests` (счёт ждёт сброса, переживает перезапуск экземпляра, повтор пакета не считает дважды); из них 12 — кэш лент E08-09: 8 в `FeedCacheApiTests` (повторный запрос «Свежего» и ленты блога без SQL, срок жизни, сброс публикацией, удалением и правкой, несуществующий блог не кэшируется, ошибка страницы), 4 в `FeedPageCacheTests` (сброс, свои страницы у ленты, пустое чтение, работа без кэша); из них 6 — счётчики в Redis E10-09: 3 в `WriteBufferTests` (пакеты, повтор брошенного пакета после аренды, записи переживают новое подключение; Redis и фейк), 3 в `ViewFlushTests` (счёт ждёт сброса, переживает перезапуск экземпляра, повтор пакета не считает дважды); из них 11 — подписки E11-03: 8 в `SubscriptionApiTests` (подписка дважды — одна, отписка дважды, одновременные запросы, числа личного блога и подписки владельца, свой блог нельзя и своя тема можно, гость — 401, нет блога — 404, контракт `IBlogSubscriptions`), 3 в `SubscriptionRulesTests`; из них 12 — кэш лент E08-09: 8 в `FeedCacheApiTests` (повторный запрос «Свежего» и ленты блога без SQL, срок жизни, сброс публикацией, удалением и правкой, несуществующий блог не кэшируется, ошибка страницы), 4 в `FeedPageCacheTests` (сброс, свои страницы у ленты, пустое чтение, работа без кэша); из них 6 — счётчики в Redis E10-09: 3 в `WriteBufferTests` (пакеты, повтор брошенного пакета после аренды, записи переживают новое подключение; Redis и фейк), 3 в `ViewFlushTests` (счёт ждёт сброса, переживает перезапуск экземпляра, повтор пакета не считает дважды); из них 12 — тема E11-01: 2 в `SubsiteRulesTests` — тема с владельцем и без него нельзя, 3 в `TopicApiTests` — владелец в профиле темы, без него и у личного блога, демо-темы у демо-аккаунтов, 6 в `BlogFeedApiTests` — посты темы по страницам, без чужих, черновиков и удалённых, пустая тема, неизвестный блог, неверная страница, 1 в `PublishApiTests` — опубликованный в теме пост в её ленте; из них 3 — рейтинг E10-05 на PostgreSQL: 2 в `LikeApiTests` (рейтинг поста и комментария пересчитывается при каждом изменении, повтор не меняет; одновременные лайки и снятия оставляют рейтинг равным реакциям), 1 в `CommentApiTests` («Популярные» ставят оценённую ветку выше обсуждаемой и возвращают порядок после снятия лайков); из них 12 — устройства E03-09: 4 в `AccountSessionTests` — список с активностью и текущим, завершение одного, «выйти со всех», права и 404; 8 в `DeviceNamesTests`; из них 7 — обложка блога E04-04: 6 в `ProfileCoverApiTests` (обложка и положение видны всем, смена положения без новой загрузки, удаление, без обложки — `null`, чужой файл, не картинка и положение вне 0…100 — 400, гость — 401) и 1 в `SubsiteRulesTests` (пределы положения и удаление); из них 4 — несколько публикующих модулей в одном хосте E01-06a в `MultiModuleOutboxTests`: каждое событие в outbox своей схемы и доставлено после коммита своего модуля, откат одного модуля не задевает другой, обычный `IPublishEndpoint` — outbox первого модуля, повторная регистрация — ошибка; из них 2 — смена почты E03-06 в `AccountEmailTests`: почта меняется только кодом с нового адреса, прежний получает письмо без нового адреса, занятый адрес отклоняется без имени и почты владельца; из них 2 — ряд реакций E10-03 в `LikeApiTests`: событие с новыми счётчиками поста и комментария, повтор и отказ без события; из них 36 — учёт просмотров E07-06: 20 в `ViewRulesTests` (сутки UTC, псевдоним гостя под секретом суток, боты, люди, предзагрузка), 16 в `PostViewApiTests` (один раз в сутки для вошедшего и гостя, боты и предзагрузка, автор, 404 черновика, счётчик в карточке ленты, показы карточек, пределы отчётов, время чтения); из них 6 — настройки аккаунта E04-08 в `PreferenceApiTests`: значения по умолчанию, выбор на одном устройстве виден на другом, чужой выбор не виден, 2 неверные темы — 400, гость — 401; из них 14 — превью картинки E06-03: 10 в `ImagePreviewsTests` (цвет и размер превью PNG, JPEG и WebP, пропорции, без увеличения, взвешивание по прозрачности, один пиксель, градиент, нечитаемые файлы), 3 в `UploadApiTests` (превью в ответе и в `media_files`, без превью для нечитаемой картинки и PDF), 1 в `PostApiTests` (`imagePreviews` поста); из них 6 — лучший комментарий ленты E08-15 в `FeedBestCommentApiTests` на PostgreSQL: самый лайкнутый, пост без комментариев без блока, ранний из равных и без лайков, удалённый не выбирается, блог автора и имя гостя, обрезка длинного текста, страница из 6 постов — столько же SQL-запросов, сколько из одного; из них 7 — лайк E10-01 в `LikeApiTests` на PostgreSQL: поставить и снять, повтор идемпотентен, свой пост и комментарий — 403 `reactions.own_content`, гостевой — 403 `reactions.guest_comment`, лайки поста и комментариев одним чтением для вошедшего и гостя, черновик, удалённый и неизвестный — 404, вход и подтверждённая почта; из них 7 — правка и удаление комментария E09-05 в `CommentApiTests`: правка в окне и «изменено» в дереве, отказ после окна (403 `comments.edit_closed`), только автор (401, 403 `comments.not_author`, 404), пустой текст (теория из двух), заглушка над ответом, отказ ответить на неё, повторное удаление и правка заглушки — 404, удаление последнего ответа убирает и заглушку, свои комментарии `/comments/mine`; из них 3 — порядок комментариев E09-04 в `CommentApiTests`: по умолчанию и `sort=popular` — обсуждаемые ветки сверху, `sort=date` — новые сверху и вместе с `levels`, отказ на неизвестный порядок (теория из двух); из них 4 — дерево по уровням E09-02 в `CommentApiTests`: уровни и счёт скрытых ответов, отказ на уровни вне 1…32 (теория из двух), 1000 комментариев одним чтением; 2 — карточка ленты E08-02 в `FreshFeedApiTests`: автор и тема элемента ленты, аккаунт без блога — без автора; из них 2 — автор поста E07-04 в `ProfileApiTests`: личный блог по аккаунту владельца, аккаунт без блога — 404 без создания блога; из них 3 — демо-данные модулей MVP E01-19 в `MvpDemoSeedTests`: сидер с настоящим каталогом наполняет пустую базу аккаунтами, блогами, темами, постами и комментариями, ссылки между модулями сходятся через `SeedData.Id`, повторный запуск ничего не добавляет; из них 2 — блок «Фото» E05-06 в `PostApiTests`: своя картинка даёт источники в `images`, чужая или несуществующая — 400 по пути блока; из них 28 — комментарии E09-01 в `Platform.Modules.Comments.IntegrationTests`: 17 без базы на модель, путь, предел глубины, гостевой вид и настройку, 11 через API на PostgreSQL — ответы посту и комментарию, порядок дерева, ветка, по одному SQL-запросу на дерево и ветку, предел глубины из настройки, гостевой без аккаунта и без ответов, повторный импорт, ошибки текста и чужого родителя, черновик и удалённый пост, вход и подтверждённая почта; из них 13 — публикация E05-09 в `PublishApiTests` и `TopicApiTests`: проверки, права, темы; из них 9 — лента «Свежее» E08-01 в `FreshFeedApiTests`: страницы по курсору без пропусков и дублей при новых постах, одинаковое время публикации, черновики и удалённые вне ленты, неверный курсор и лимит; из них 16 — слаг адреса поста E07-02 в `PostSlugTests`: транслитерация, пост без слага, обрезка длинного слага, слаг следует за заголовком; из них 5 — капча E03-12: порог неудач на адрес почты и на адрес сети, одинаковый ответ `captcha_required`, вход с решением фейка, ввод кода, а также `ProofOfWorkCaptchaTests` — решение проходит один раз, чужая подпись, неверное число, срок; `Platform.Modules.Identity.IntegrationTests`: 10 — пароль E03-05: сброс закрывает прежнюю cookie провайдера и refresh-токен, новый пароль входит, старый нет, письма с кодом и о смене; код одноразовый и истекает через 15 минут; 5 неверных кодов тратят код, слабый пароль попытку не тратит; ответ одинаков для любого адреса, письмо только аккаунту; неподтверждённой регистрации письма нет; смена с текущим паролем, 401, неверный текущий, слабый новый, сессии остаются, письмо; аккаунт без пароля задаёт его сбросом; правила кода и метки сессий без базы; `Platform.Modules.Blogs.IntegrationTests`: 8 — профиль E04-03: правка имени, описания и аватара видна всем сразу (с подписанной ссылкой на аватар E06-02), профиль без аватара — без ссылки, пустые поля очищают, пределы в графемах (25 эмодзи — можно, 26 — нет) без частичного сохранения, аватар — только своё изображение (чужое, PDF и несуществующее — 400), без входа — 401, аккаунт без блога получает его, правило `SetAvatar`; `Platform.Modules.Media.IntegrationTests`: 65 — раздача E06-02: 13 без контейнера на ссылки imgproxy (подпись по примеру документации imgproxy, путь, другой ключ или соль, 5 неверных сторон, настройки и 4 неверных), 5 на imgproxy и MinIO в Testcontainers (квадрат WebP, ширина AVIF, без увеличения, 4 ссылки без верной подписи — 403, кэш на год и ETag → 304); загрузка E06-01: 34 без базы (14 форматов по сигнатуре, 3 чужих содержимых, 4 имени с верным расширением и без него, 5 подменённых расширений, пустой файл, пределы по видам, пределы из настроек и 3 неверных значения, случайные ключи, владелец и имя файла), 11 через API на PostgreSQL с фейком хранилища (оригинал под случайным ключом с владельцем и типом по содержимому, два ключа одному файлу, 4 подмены — 415 и ничего не сохранено, предел изображения из настройки — 413, PDF со своим пределом, без входа 401, пустой файл и запрос без файла — 400), 2 на MinIO в Testcontainers (запись, чтение, удаление, бакет при первой записи, общий бакет); `Platform.Modules.Posts.IntegrationTests`: 28 — пост E07-01: 12 без базы на правила (черновик native без источника, пустой заголовок — 3, источник Telegram, источник без ссылки и без сообщения, автор, блог и тело обязательны, публикация один раз, мягкое удаление и неизменность удалённого, кто видит, кто меняет, кто пишет в личный блог и тему), 16 через API на PostgreSQL (черновик в личном блоге с очищенным телом, пост в теме, чужой личный блог, неизвестный блог, ошибки схемы и нет тела, создание без входа, черновик только автору, опубликованный всем, происхождение и источник, уникальный источник в базе, правка автором, правка без блога, права на правку, мягкое удаление, права на удаление, демо-посты один раз и принимаются валидатором); `Platform.Modules.Blogs.IntegrationTests`: 30 — блоги E04-01: 18 без базы на правила (личный блог, 25 и 160 символов в графемах, пробелы, 12 форм никнейма), 11 через API на PostgreSQL (профиль по id и никнейму, дата регистрации только владельцу, 404, 3 ошибки валидации никнейма, `/me` без входа и с созданием блога один раз, уникальные индексы), 1 на настоящем хосте воркеров (событие создаёт один блог, повтор и дубль ничего не меняют); `Platform.Modules.Identity.IntegrationTests`: 150, из них 12 — сессии E03-03: обновление истёкшего токена доступа, ротация и продление, срок жизни refresh-токена, повтор погашенного токена отзывает цепочку, чужая цепочка цела, окно одновременных обновлений, отзыв, чужой клиент, удалённый аккаунт, выход у провайдера с просроченным `id_token_hint`, чужой адрес возврата; 2 — `AccountActivated` E04-01 (подтверждённая регистрация объявляется один раз, неподтверждённая — нет; аккаунт по коду объявляется); 13 — вход E03-02 через API с письмами в Mailpit: пароль до токена доступа, одинаковый ответ на неверный пароль, неизвестный адрес, неподтверждённую регистрацию и неверный ввод, код входит в аккаунт и один раз, одинаковый ответ на запрос кода и таймер для любого адреса и новый аккаунт по коду, код отнимает адрес у неподтверждённой регистрации и у аккаунта, вписавшего его, неверный, чужой, израсходованный и просроченный код, 3 ошибки валидации, лимит паролей на адрес почты, лимит писем на адрес почты, лимит запросов кода на адрес сети; 27 — регистрация E03-01: 14 без базы на пароль (длина, Argon2id и своя соль, проверка только своего пароля, отказ на подделанной строке) и 13 через API с письмами в Mailpit (регистрация, код из Mailpit, вход до токена доступа; неверный и просроченный код; таймер повторной отправки; 5 ошибок валидации; занятый адрес; замена чужой неподтверждённой регистрации; подтверждённый аккаунт не входит по идентификатору и коду; лимит на адрес сети); 52 — почта E18-03: 36 без базы на нормализацию (регистр, точки Gmail, `+метки`, IDN, NFC, неверные адреса) и 17 через API — код из письма подтверждает почту и сохраняет дату, неподтверждённый не публикует (403 `email_not_confirmed`), дубль после нормализации (409 только на подтверждённый, неподтверждённый ждёт), первый введённый код отнимает неподтверждённый адрес, уникальный индекс в базе, неподтверждённая почта не ключ слияния, просроченный, чужой и израсходованный код, подтверждённая почта остаётся до подтверждения новой, неверный адрес, аноним, лимит писем; 3 — хаб реального времени E01-11 с токенами провайдера: билет за токен открывает хаб, просроченный токен не даёт ни билета, ни хаба; 34 — вход E18-02: discovery и ключи, поток с PKCE и токены с `sub` = `account_id`, API видит аккаунт, окно входа и `prompt=none`, удалённый аккаунт, чужой клиент и адрес возврата, нет PKCE и `plain`, неверный верификатор, повтор, чужой и просроченный код, просроченный и поддельный токен, лимит по аккаунту, issuer из настройки, внешний провайдер по `Auth:Authority`, 10 без базы — ключи и клиенты из настроек; 6 — модуль Identity E18-01: UUID v7 по часам, порядок идентификаторов, проверка версии, миграция только в схему `identity`, запись и чтение аккаунта, демо-аккаунты один раз с идентификаторами `Id("account", n)`; `Platform.Mail.Tests`: 6 — доставка E01-12: письмо через очередь воркеров, повтор после двух сбоев, неверный адрес у вызывающего, 3 на Mailpit в Testcontainers — SMTP из настроек с входом и без, имя отправителя; `Platform.Api.Tests`: 30 в процессе, без Docker, из них 8 — наблюдаемость E01-09: пробы, метрики Prometheus, трасса из `traceparent`, ошибка в логе и спане под trace id, пробы не трассируются, JSON-логи; `Platform.Configuration.Tests`: 15, настоящие хосты и сверка с репозиторием, из них 5 — телеметрия воркеров, 3 — настройки почты воркеров; `Platform.Persistence.IntegrationTests`: 9 на PostgreSQL (миграции, сброс Respawn; E18-01 — в базе реальных модулей нет столбцов учётных данных вне `identity` и внешних ключей между схемами, и запросы это ловят), 7 без базы; `Platform.Messaging.IntegrationTests`: 6 на контейнерах, 11 без Docker; `Platform.Seeding.Tests`: 5, команда сидера без базы (окружения, настройки); `Platform.Api.IntegrationTests`: 34 (прежняя запись 17 отставала на 1), из них 13 — канал реального времени E01-11 на Redis: событие своей темы и не чужой, подписка и отписка на пост, отказ в чужом аккаунте и неизвестных темах (5), без билета и с поддельным, просроченный билет, билет заголовком `Bearer`, билет только за токен, backplane между двумя экземплярами, новое соединение снова в теме аккаунта; API на PostgreSQL и Redis и сидер на PostgreSQL, из них 2 — трасса до спана базы и готовность) |
| Клиент, компонентные | 1155 (из них 16 — репосты E10-08 в `tests/reposts/reposts.spec.ts`; 7 — раздел «Закладки» E04-12 в `tests/bookmarks/BookmarkSection.spec.ts`; из них 4 — «Способы входа» E03-08 в `tests/settings/SignInMethods.spec.ts`; из них 8 — вход через провайдеров E03-07 в `tests/auth/ExternalSignIn.spec.ts`; из них 14 — закладки E10-07 в `tests/bookmarks/bookmarks.spec.ts`; из них 10 — вкладка «Комментарии» E09-15 в `tests/comments/BlogComments.spec.ts` (ссылка на комментарий, склейка страниц, список, гость, состояния, «Показать ещё», вкладки темы); из них 14 — комментарии в реальном времени E09-09: 13 в `tests/comments/LiveComments.spec.ts` (кнопка «N новых», счётчик, полоса без кнопки, состояния, `parseCommentPosted`, `withArrived`, `loadCommentArrival`) и 1 в `tests/realtime.spec.ts` (общая тема двух слушателей); из них 11 — жесты ленты E08-14: 7 в `tests/feed/PullToRefresh.spec.ts` (`pullDistance`, обновление за порогом, короткий жест, прокрученная страница, сбой, жест браузера) и 4 в `tests/shell/hide-on-scroll.spec.ts` (`headerHidden`, класс оболочки); из них 6 — новые с прошлого визита E09-08 в `tests/comments/NewComments.spec.ts`: отметка отдельно от подсветки ссылки, кнопка с остатком, без кнопки, удалённый без отметки, `newCommentsOrder`; из них 15 — «Показать новые» E08-08 в `tests/feed/NewPosts.spec.ts`: что считается новым, порядок при равном времени, переполнение, показ, плашка, интервал, возвращение во вкладку, сбой и сброс; из них 11 — ссылка на комментарий E09-07: `commentPath`, `nearestShown`, `branchRootOf`, `loadCommentPlace` в `tests/comments/comments.spec.ts`, пункт меню и `commentUrl` в `tests/comments/comment-edit.spec.ts`, время-ссылка, подсветка и копирование в `tests/comments/CommentLink.spec.ts`; из них 8 — фильтр «Свежего» E08-07: 7 в `tests/feed/FreshRatingFilter.spec.ts` (кнопки, отметка, выбор, пороги, хранение гостя) и 1 в `tests/feed/feeds.spec.ts` (`minRating` в запросе); из них 13 — виджет «Популярные комментарии» E08-12 в `tests/comments/PopularComments.spec.ts`: адрес комментария, состояние чтения, список, неизвестный автор, пост без заголовка, состояния виджета в `ShellAside`; из них 6 — топ за период E08-06: `loadTopFeed`, периоды и их адреса в `tests/feed/feeds.spec.ts`, `tests/feed/TopPeriodSwitch.spec.ts`; из них 12 — «Поделиться» E07-07 в `tests/post/PostShare.spec.ts`: каноническая ссылка, адреса сетей, состав списка, системное меню на телефоне (с заголовком и без, закрытие, отказ → копия), список на широком экране, копия и её сбой, сеть в новой вкладке; из них 10 — онбординг E03-10 в `tests/onboarding/OnboardingSteps.spec.ts`: правило имени, локальный `returnTo`, шаги с отправкой в конце, пропуск всего, уже пройден, ошибка поля, сбой, загрузка, ошибка и пусто у тем; из них 14 — кнопки чёрного списка E11-06: `tests/blocks/blocking.spec.ts` (`BlockDialog`, `BlogBlock`, кнопка в `BlogView`, пункт меню поста и комментария), `commentMenuItems` и `withAuthorBlocked` в `comment-edit.spec.ts`, `comments.blocked` в `comment-send.spec.ts`, сообщение формы в `CommentForm.spec.ts`; из них 7 — выбор реакции E10-02: `tests/reactions/reactions.spec.ts` — картинки и имена в ряду, набор по кнопке, по наведению, по долгому нажатию, загрузка, ошибка, пусто, Escape; `tests/likes/likes.spec.ts` переписан на API реакций; из них 7 — списки подписчиков и подписок E04-06 в `tests/blog/SubscriptionList.spec.ts`: строки, кнопки и состояния `SubscriptionList`, `appendSubscriptionCards`, числа-кнопки `BlogSubscription`; из них 19 — поиск в шапке E14-02: `tests/shell/ShellSearch.spec.ts` — пауза ввода, отброс старых ответов, скелетон, ошибка с «Повторить», слишком длинный запрос, история и «Очистить», своя история у аккаунта и гостя, стрелки, Enter и Esc, фокус; `tests/shell/search-history.spec.ts` — порядок, повторы, предел, ключ-HMAC; из них 11 — набор реакций E02-12 в `tests/reactions/reaction-set.spec.ts`: картинки и коды сервера, лицензия, SVG без скриптов, `ReactionIcon`, состояния `ReactionSet`, `loadReactionSet`; из них 12 — лента уведомлений E12-02: 10 в `tests/notifications/NotificationList.spec.ts`, 2 в `ShellHeader.spec.ts` (счётчик, панель); из них 5 — чёрный список E11-06: 4 в `tests/settings/BlockList.spec.ts` (загрузка и список, пусто, ошибка с «Повторить», разблокировка и отказ), 1 в `tests/comments/CommentEdit.spec.ts` (свёрнутый комментарий и «Показать»); `frontend`: 909; из них 6 — никнейм E04-05 в `tests/settings/NicknameForm.spec.ts`; из них 14 — темы в меню и каталог E11-02: 7 в `tests/topic/TopicCatalog.spec.ts` — список со ссылками и числами, «Показать ещё», сбой следующей страницы, загрузка, ошибка с «Повторить», пусто и ничего не нашлось, поле поиска; 6 в `tests/topic/topic-menu.spec.ts`; 1 в `tests/shell/ShellCookieBanner.spec.ts` — место под баннером; из них 4 — «Моя лента» E08-04: 2 в `tests/feed/feeds.spec.ts` (`loadMyFeed`: страница по курсору, выход и сбой — ошибка), 2 в `tests/feed/MyFeedSuggestions.spec.ts` (до шести тем со ссылками и «Свежее», без тем — только «Свежее»); из них 11 — создание темы E11-11 в `tests/topic/TopicCreateForm.spec.ts`: пустая форма и адрес страницы, отказ пустой формы с фокусом, отправка, состояние отправки, ошибки полей API, лимит, неподтверждённая почта, сбой, истёкший вход, `topicInputErrors`, `createTopic`; из них 12 — подписки E11-03: 9 в `tests/blog/subscriptions.spec.ts` (PUT и DELETE, 401, 403, 404 и сбой, состояние до ответа, сокращение счёта, кнопка гостя, подписчика и владельца, занятая кнопка), 2 в `tests/blog/BlogView.spec.ts`, 1 в `tests/topic/TopicView.spec.ts`; из них 7 — тема E11-01: 4 в `tests/topic/TopicView.spec.ts` (шапка с владельцем, без описания и владельца, скелетон, ошибка с «Повторить»), 2 в `tests/feed/feeds.spec.ts` (`loadBlogFeed`), 1 в `tests/feed/FeedView.spec.ts` (лента без заголовка со своим пустым состоянием); из них 7 — устройства E03-09 в `tests/settings/SessionList.spec.ts`: загрузка и список, ошибка с повтором, одно устройство, выход одного и его сбой, «выйти со всех» с подтверждением, 401; из них 18 — обложка блога E04-04: 10 в `tests/blog/BlogCover.spec.ts` (показ по положению, пусто, мышь, палец, стрелки и «Отмена», загрузка по «Сохранить», не картинка, загрузка и ошибка, удаление, выход из сессии), 5 в `tests/blog/cover-position.spec.ts`, 2 в `tests/blog/profiles.spec.ts`, 1 в `tests/blog/BlogView.spec.ts`; из них 8 — смена почты E03-06 в `tests/settings/EmailChangeForm.spec.ts`: загрузка, пусто, ошибка с повтором, код и прежняя почта до него, неверный код, занятый и неверный адрес, уже своя почта, сессия, лимит и сбой; из них 6 — Ctrl+B и Ctrl+I без выделения #387 в `tests/editor/caret-marks.spec.ts`: команда и клавиша не доходит до Editor.js, Cmd, раскладка по коду клавиши, выделение остаётся Editor.js, другие клавиши, снятие обработчика; из них 9 — ряд реакций E10-03 в `tests/reactions/reactions.spec.ts`: порядок по количеству, разбор события, свой знак остаётся, компонент ряда; из них 8 — учёт просмотров E07-06 в `tests/views/views.spec.ts` и `tests/feed/FeedCard.spec.ts`: разбор отчётов, показы пачкой и по одному разу, пачки по 50, открытие опубликованного поста, время чтения только с согласием и при переходе к другому посту, счётчик с формами числа; из них 12 — разделы настроек E04-08: 5 в `tests/settings/SettingsNav.spec.ts` (разделы и адреса, страница на каждом адресе, правая колонка и список телефона), 7 в `tests/settings/GeneralSettingsForm.spec.ts` (скелетон, тема аккаунта поверх браузерной, сохранение с тостом, откат при сбое, «Повторить», переключатель левого меню сохраняет в аккаунт, гость — только в браузере); из них 12 — арабский текст E02-14 в `tests/bidi.spec.ts` и `tests/post/PostBlocks.spec.ts`: изоляты вокруг цитаты, цифр, огласовок и двух фрагментов, перенос строки, теги, сущности и атрибуты, блок на арабском не трогается, объявление шрифта и токены, снимок смешанных блоков; из них 3 — превью E06-03 в `tests/ui/UiImage.spec.ts`: цвет и размытая копия в HTML сервера, снятие после загрузки и ошибки, отбрасывание чужих значений; из них 18 — согласие на cookie E02-13 в `tests/shell/cookie-consent.spec.ts`, `tests/shell/ShellCookieBanner.spec.ts` и `tests/settings/CookieSettingsForm.spec.ts`; из них 17 — локализация E02-10 в `tests/i18n/i18n.spec.ts` и `tests/i18n/lint.spec.ts`: ключи, слоты, русские формы множественного числа, числа и даты по-русски, список словарей по файлам папки, второй язык с откатом на русский, `I18nText`, правило `i18n/no-raw-text` на тексте, атрибутах, кириллице в коде и исключениях; из них 6 — лучший комментарий ленты E08-15 в `tests/feed/FeedCard.spec.ts`, `tests/feed/feeds.spec.ts` и `tests/comments/comments.spec.ts`: без блока у поста без комментариев, автор, текст и ссылка на комментарий последним блоком карточки, имя гостя, адрес `#comment-<id>` и его разбор; из них 10 — лайки E10-01 в `tests/likes/likes.spec.ts`: PUT и DELETE на цель, отказ, 401 и сбой, чтение лайков поста и комментариев, состояние до ответа, имя отложенного действия, «Нравится» в панели — чужой пост, свой и черновик, лайк в дереве — чужой, свой, без лайка у гостевого и удалённого, отметка; тест панели E07-04 проверяет выключенное «Нравится» без лайков; из них 5 — дерево на телефоне E09-06 в `tests/comments/CommentTree.spec.ts` и `tests/comments/comments.spec.ts`: ссылка на экран ветки вместо «Показать ещё», экран ветки без заголовка, порядка и формы, «Ветка не найдена», адрес экрана, 404 ветки; из них 22 — правка и удаление комментария E09-05 в `tests/comments/comment-edit.spec.ts` и `tests/comments/CommentEdit.spec.ts`: окно правки, пункты меню своего и чужого, правка на месте и заглушка в дереве, опустевшие заглушки выше, ответы API, меню только у своих, только «Удалить» после окна, форма правки с отказом сервера и «Отмена», подтверждение и сбой удаления, «изменено», заглушка без автора, текста и действий; из них 11 — порядок комментариев E09-04 в `tests/comments/comment-sort.spec.ts`: группа «Популярные» и «По дате» с `aria-pressed`, выбор другого порядка, переключатель в загрузке и ошибке и без него у пустого дерева, запомненный порядок, неизвестное значение и недоступное хранилище, место отправленного комментария в каждом порядке; из них 18 — общие состояния E02-07 в `tests/ui/UiSkeleton.spec.ts`, `tests/ui/UiState.spec.ts`, `tests/network.spec.ts`, `tests/error-page.spec.ts`, `tests/shell/ShellLayout.spec.ts` и `tests/ui/showcase.spec.ts`: формы и размеры скелетона, тона сообщения, данные со сбоем, события сети, код ответа по запросу, страницы 404 и 500, плашка над содержимым, разделы витрины; из них 13 — меню «…» E07-05 в `tests/post/PostMenu.spec.ts` и `tests/post/posts.spec.ts`: состав по роли и флагам, нет продвижения и донатов, подтверждение и сбой удаления, копия ссылки, шторка на телефоне, `own` в `lookUpPost`, `deletePost`, меню в шапке; 18 — быстрый пост E05-29 в `tests/editor/quick-post.spec.ts`, `tests/editor/QuickPostForm.spec.ts`, `tests/editor/PostEditor.spec.ts` и `tests/shell/phone-layout.spec.ts`: документ из текста и фото, пустой пост, публикация с темой и фото, ошибки фото и публикации, «Развернуть», гость, «Написать» открывает лист; из них 19 — дерево комментариев E09-02 в `tests/comments`: вложение, скрытые ответы, ветка на место, счёт, чтения, состояния, сворачивание, «Показать ещё»; 11 — карточка ленты E08-02 в `tests/feed/FeedCard.spec.ts` и `tests/feed/feeds.spec.ts`: автор, тема, дата и канонический адрес, пост без автора, короткий пост целиком, раскрытие лонгрида, правило обрезки по блокам и знакам; из них 4 — шапка и панель действий E07-04 в `tests/post/posts.spec.ts`: автор поста в теме по аккаунту и без него, автор, тема и дата в шапке, панель-`toolbar` с выключенным «Нравится», якорем комментариев и копией ссылки, стрелки между кнопками; из них 7 новых — подгрузка ленты E08-03 в `tests/feed/FeedView.spec.ts` и `tests/feed/feeds.spec.ts`: метка и новая метка для короткого списка, конец ленты, сбой с «Повторить», кнопка без IntersectionObserver, снимки по записям истории и ключ записи; из них 18 — блок «Фото» E05-06 в `tests/editor/image.spec.ts`, `keyboard.spec.ts` и `tests/post/PostBlocks.spec.ts`: пусто и выбор файла, загрузка с индикатором и превью, четыре отказа загрузки с «Повторить», нечитаемый файл, сбой сети, отрисовка из данных, предел подписи, `Enter` в подписи, блок без файла не попадает в документ, картинка — не пустой пост, галерея и камера панели, `figure` с источниками и заглушка; из них 11 — телефон E05-10 в `tests/editor/keyboard.spec.ts`, `BlockEditor.spec.ts` и `PostEditor.spec.ts`; из них 15 — публикация E05-09 в `tests/editor/PostEditor.spec.ts` и `tests/post/publish.spec.ts`; из них 16 — отрисовка блоков E07-03 в `tests/post/PostBlocks.spec.ts`: снимки каждого типа блока, заглушка, разметка, белый список; из них 9 — свой блок «Текст» #331 в `tests/editor/text.spec.ts`: плоский список тегов инструментов и `br`, его не меняет Editor.js, превращение по полю `text`, вставка `P`, отрисовка, плейсхолдер, сохранение и слияние с разметкой, `preserveBlank`, вставка, очистка пустого абзаца; из них 12 — лента «Свежее» E08-01 в `tests/feed/`: состояния ленты, следующая страница, курсор и адрес поста из ленты; из них 8 — вставка списков E05-02: списки Word без маркеров и с уровнем, список Google Docs, вложенные списки веб-страницы, абзацы в пункте, вид по маркеру Word, `onPaste` и `pasteConfig` блока «Список»; из них 11 — адрес поста E07-02 в `tests/post/posts.spec.ts`: id из сегмента, одна схема для блога и темы, поиск поста и его блога, состояния страницы поста; из них 12 — компонент изображения E06-04: `srcset` по ширинам и `sizes`, ссылка вместо источников, место по пропорциям, ленивая загрузка и `eager`, состояния «загрузка», «загружено», «пусто», «ошибка», картинка, загруженная или сломанная до гидратации, крошечная картинка плотного экрана, ленивая картинка после гидратации, источники аватара под его размер, `srcset` аватара в шапке блога, раздел витрины; из них 16 — меню блоков и перестановка E05-07: место падения блока по серединам блоков, клавиши Ctrl+Shift+↑/↓ и Cmd, перенос текущего блока клавишами с пределами у краёв, чужие клавиши не трогает, перетаскивание за «⋮⋮» с метками «до» и «после», каретка в перенесённом блоке, клик после перетаскивания не открывает меню, короткое нажатие и отмена без переноса, только с ручки; поиск шторки на телефоне — поиск по части названия без учёта регистра, скрытие и «Ничего не найдено», поле в открытом меню без фокуса, `Enter` добавляет первый найденный, сброс при закрытии, на десктопе не вмешивается; из них 5 — капча E03-12: форма просит капчу, ошибка и загрузка проверки, решение тратится одной попыткой, капча при вводе кода, перебор `api/captcha.ts`; из них 1 — аватар в шапке блога по ссылке E06-02; из них 16 — пароль E03-05: форма сброса — почта из формы входа, код и новый пароль, загрузка, короткий пароль без вызова API, неверный код и правило пароля у полей, новый код по таймеру, ошибки адреса, лимита и связи, возврат ко входу, «Забыли пароль?» только с вызовами сброса и вход после сброса; форма смены — пусто, загрузка и успех, короткий пароль без вызова, неверный текущий у поля, правило API у нового, 401, 429 и сбой; из них 16 — профиль E04-03: счёт символов по графемам и пределы 25 и 160, PUT `/profiles/me` и его исходы, загрузка аватара `multipart/form-data` и отказы 415, 413, 401; из них 11 — окно входа E03-04: модальное окно на десктопе и шторка на весь экран на телефоне, формы без своих заголовков, переход между входом и регистрацией, `signed-in` после пароля и после кода регистрации, закрытие; отложенное действие выполняется один раз на своей странице, не трогает чужие, забывается на другой странице и через 10 минут, переживает битую запись и недоступное хранилище; 25 — цитата и разделитель E05-05: пункты меню, два поля цитаты с плейсхолдерами, текст и подпись хранятся раздельно с разметкой, поле с одним `<br>` пусто, `Enter` в тексте ведёт в подпись, `Shift+Enter` — перенос, `Enter` в подписи даёт текст после цитаты, только чтение, превращение с подписью через тире, документ схемы; разделитель пуст, не редактируется, `role="separator"`, щелчок выделяет, `Backspace` и `Delete` удаляют выделенный и ставят каретку рядом (4 случая), без выделения и с модификатором клавиши не трогает, обработчик снимается, каретка после добавленного разделителя (4 случая); 26 — списки E05-04: пункты меню, `ul`/`ol` с уровнями, пустой список с плейсхолдером, сохранение и выправление уровней, `Tab` и `Shift+Tab` с пределом и кареткой, `Enter` делит пункт, поднимает пустой вложенный, завершает список в конце и в середине, превращает пустой список в текст, `Backspace` поднимает и соединяет, смена типа в меню блока и через превращение без потери уровней, текст ↔ список построчно, слияние, только чтение, 6 случаев `normalizeLevels`; 11 — заголовки E05-03: пункты меню H2 и H3, уровень и плейсхолдер, уровень 2 по умолчанию, якорь из id блока и его неизменность при правке текста, сохранение разметки и уровня, превращение по полю `text` без своего `sanitize`, только чтение, круговое преобразование документа с заголовками, текст в заголовок с разметкой; 35 — блок «Текст» E05-02: ссылки — допустимые адреса и их вид, отказ `javascript:`, `data:`, `ftp:`, относительных, без точки в хосте, с логином; очистка поля — разрешённое без изменений, `rel` и только `href`, опасные ссылки, скрипты и неизвестные элементы, `strong`/`em` в `b`/`i`, текст и пробелы как есть; вставка из Word, Google Docs и веб-страницы, переносы строк и слияние, пустая вставка; документ чистит все текстовые поля; из них 15 — сессия E03-03: refresh-токен и срок сессии, когда обновлять, ротация, отказ и чужой `sub` завершают сессию, недоступный провайдер, отзыв и адрес выхода, выход без end-session; хранилище — свежая сессия, одно обновление на одновременные запросы, токены другого экземпляра, принудительное обновление после 401, отказ, провайдер недоступен, срок жизни, выход во время обновления; 35 — ядро редактора E05-01: круговое преобразование документа всех блоков версии 1 через данные Editor.js и JSON, служебные поля и порядок полей, плоский список из вложенного, неизвестный блок, пустой документ; история — отмена и повтор между блоками, слияние ввода, новый шаг после паузы, в другом блоке и после отмены, сброс повтора, эхо отрисовки, предел шагов; каретка по смещению через разметку; `BlockEditor` на фейковом движке — загрузка, пусто, ошибка и «Повторить», документ в формате схемы, клавиши отмены и повтора по коду клавиши, Ctrl+Z без фокуса и не из чужого поля, запись несообщённой правки перед отменой; окно — диалог, закрытие, развёртывание, блокировка прокрутки; 16 — страница блога E04-02: шапка гостя и владельца с датой и кнопками, блог без названия и никнейма, адреса вкладок и текущая, скелетон, «не найден», ошибка с «Повторить», пустые вкладки, разбор адреса (UUID, никнейм, не `id123`), ответы API в состояния, владелец по `registeredAt`, свой блог; 35 — ядро редактора E05-01: круговое преобразование документа всех блоков версии 1 через данные Editor.js и JSON, служебные поля и порядок полей, плоский список из вложенного, неизвестный блок, пустой документ; история — отмена и повтор между блоками, слияние ввода, новый шаг после паузы, в другом блоке и после отмены, сброс повтора, эхо отрисовки, предел шагов; каретка по смещению через разметку; `BlockEditor` на фейковом движке — загрузка, пусто, ошибка и «Повторить», документ в формате схемы, клавиши отмены и повтора по коду клавиши, Ctrl+Z без фокуса и не из чужого поля, запись несообщённой правки перед отменой; окно — диалог, закрытие, развёртывание, блокировка прокрутки; 7 — вход E03-02: пустая форма, загрузка и вход, отказ без подсказки о почте, слишком много попыток и нет связи, код с таймером, неверным и новым кодом, ошибка адреса и таймер из `Retry-After`, возврат только в `/connect/authorize` провайдера; 13 — регистрация E03-01: пустая форма, загрузка, короткий пароль без вызова API, 4 ошибки API, обратный отсчёт и новый код, таймер из `Retry-After`, неверный и верный код, устаревшая регистрация, формат отсчёта, чтение `Retry-After`; 9 — канал реального времени E01-11: новый билет на каждое подключение, бесконечные задержки переподключения, повторная подписка, повтор первого подключения и после отказа SignalR, остановка при выходе, события по темам, запрос билета у API с токеном и 401; 12 — вход E18-02: запрос кода с PKCE S256, обмен кода с верификатором, отказ при чужих state, nonce и issuer, возврат только на страницу сайта, настройки клиента; 5 — трассировка E01-09: разбор `traceparent`, трасса страницы, новый спан на вызов, один trace id на вызовы страницы, новая трасса в браузере; 4 — клиент API E01-08: команда генерации из снимка, файл совпадает с генератором, нет ручных обращений к API, базовый адрес и problem details; 5 — настройка Playwright E01-16: два профиля, каждый тест в обоих, трасса и снимок при падении, отчёты JUnit, продакшен-сборка; 32 — каркас телефона E02-06: пять вкладок по ТЗ и их адреса, текущая вкладка, счётчики и их озвучивание, прокрутка наверх повторным нажатием, экран «Меню» для гостя и вошедшего без «коллекции», «подписки» и монетизации, открытие из панели, контракт стилей — панель ниже 925 px, плавающая кнопка ниже 640 px, безопасные зоны и «чёлка», 44 px, вкладки не расширяют страницу, место под панелью и тосты над ней; 41 — каркас десктопа E02-05: ориентиры, ссылка к содержимому, порядок фокуса, пункты меню без монетизации и «коллекции», текущий пункт на разных адресах, шапка гостя и вошедшего, переключатель темы, состояния виджетов, контракт стилей — ширины колонок из токенов, прилипание, скрытие колонок по точкам перелома; 62 — базовые компоненты E02-04: кнопка, поле, меню, вкладки, окно и шторка, тосты, аватар, полнота витрины `/ui` и переключение темы; прежние 34 — бренд в одном месте, копии `BrandLogo` совпадают, нет картинок, иконки и лицензия, контраст WCAG AA, отличие от цветов DTF, `BrandLogo`; токены: файл совпадает с генератором, роли палитр, контраст всех текстовых цветов, типографика чтения, линтер стилей ловит 10 видов сырых значений и пропускает токены, линтер в обоих клиентах, только известные переменные; витрина токенов; `admin`: 12 — те же 4 проверки клиента API, `BrandLogo`, лицензия иконок, те же 5 проверок настройки Playwright) |
| Сквозные (в каждом из двух профилей) | 410 прогонов (из них 4 — репосты E10-08 в `e2e/reposts.spec.ts`; 3 — раздел «Закладки» E04-12 в `e2e/bookmark-section.spec.ts`; из них 1 — «Способы входа» E03-08 в `e2e/sign-in-methods.spec.ts`; из них 1 — вход через провайдера E03-07 в `e2e/external-sign-in.spec.ts`; из них 5 — закладки E10-07 в `e2e/bookmarks.spec.ts`; из них 6 — вкладка «Комментарии» E09-15 в `e2e/blog-comments.spec.ts`; из них 1 — комментарии в реальном времени E09-09 в `e2e/comment-live.spec.ts`; из них 4 — жесты ленты E08-14 в `e2e/feed-gestures.spec.ts`: «потянуть» на «Свежем» и в топе, шапка при прокрутке, плашка вслед за шапкой; жесты — только в `mobile`, в `desktop` три из них пропускаются; из них 2 — новые с прошлого визита E09-08 в `e2e/comment-new.spec.ts`: второе открытие отмечает новые и кнопка ведёт по ним, у гостя новых нет; из них 2 — «Показать новые» E08-08 в `e2e/new-posts.spec.ts`: пост не вставляется сам, плашка при прокрутке, нажатие ставит его сверху и прокручивает вверх, пост ниже порога плашку не вызывает; из них 4 — ссылка на комментарий E09-07 в `e2e/comment-link.spec.ts`: глубокий комментарий с веткой и подсветкой, несуществующий, время-ссылка, «Скопировать ссылку»; из них 2 — фильтр «Свежего» E08-07 в `e2e/fresh-rating.spec.ts`: выбор гостя в браузере, пустая лента и «Показать все», порог аккаунта на другом устройстве с серверной отрисовки; из них 4 — виджет «Популярные комментарии» E08-12 в `e2e/popular-comments.spec.ts`: список и переход к комментарию, без заблокированного автора, загрузка, ошибка с «Повторить» и пусто на десктопе, на телефоне не запрашивается; из них 6 — топ за период E08-06 в `e2e/top.spec.ts`: адрес и серверный рендер каждого периода, 404 незнакомого, переключатель и «назад», пустой период, загрузка и сбой с «Повторить»; из них 3 — «Поделиться» E07-07 в `e2e/post-share.spec.ts`: системное меню на телефоне и список на десктопе, копия канонической ссылки, Telegram в новой вкладке; старый тест копирования из `e2e/post.spec.ts` перенесён туда, −1; из них 3 — онбординг E03-10 в `e2e/onboarding.spec.ts`: шаги с проверкой имени и темой, пропуск всех шагов, прошедший аккаунт его не видит; из них 10 — кнопки чёрного списка E11-06 в `e2e/black-list.spec.ts`: блог, меню поста и комментария, гость без кнопки, форма под постом заблокировавшего; из них 2 — выбор реакции E10-02 в `e2e/reaction-choice.spec.ts`: набор наведением или долгим нажатием, смена одним выбором, перезагрузка, снятие; «Нравится» меняется, а не добавляется; из них 6 — списки подписчиков и подписок E04-06 в `e2e/subscription-lists.spec.ts`: подгрузка по курсору до числа шапки, заглушки, пусто, ошибка, отписка из строки, окно входа гостю; из них 4 — поиск в шапке E14-02 в `e2e/search.spec.ts`: панель на ширину ленты и отдельный экран на телефоне, подсказки, стрелки и Enter, история после перезагрузки и «Очистить», «ничего не нашлось», Esc и «Отмена», скелетон и ошибка с «Повторить»; из них 5 — набор реакций E02-12 в `e2e/reaction-set.spec.ts`: картинки в обеих темах, загрузка, пусто, ошибка с повтором; из них 3 — лента уведомлений E12-02 в `e2e/notifications.spec.ts`: счётчик, отметка и подгрузка, пусто, скелетон и сбой; из них 2 — чёрный список E11-06 в `e2e/black-list.spec.ts`: список и «Разблокировать» до пустого, скелетон и сбой с «Повторить»; `frontend`: 327; из них 3 — никнейм E04-05 в `e2e/nickname.spec.ts`; из них 5 — темы в меню и каталог E11-02 в `e2e/topics.spec.ts`: популярные темы гостя в меню и «Показать все», порядок и поиск, поиск из адреса, пусто и ошибка, загрузка, подписанные темы в меню человека; из них 4 — «Моя лента» E08-04 в `e2e/my-feed.spec.ts`: гостю «Войдите» и окно входа, пустая лента с темами и «Свежим», пост темы после подписки, загрузка и сбой с «Повторить»; из них 5 — создание темы E11-11 в `e2e/topic-create.spec.ts`: гость входит и создаёт тему, ошибки формы и API, предел тем, неподтверждённая почта, ссылка из меню; из них 5 — подписки E11-03 в `e2e/subscriptions.spec.ts`: гость видит счёт и окно входа, подписка сама после входа, подписка на тему переживает перезагрузку и отменяется, подписка на блог в счётчике своего, сбой с откатом и тостом; из них 5 — страница темы E11-01 в `e2e/topic.spec.ts`: сервер отдаёт шапку и посты, один адрес темы, пустая тема и «Написать в тему», сбой ленты, 404; из них 2 — устройства E03-09 в `e2e/sessions.spec.ts`: список, текущее, выход одного и перезагрузка, «выйти со всех» с отменой и подтверждением; из них 2 — обложка блога E04-04 в `e2e/blog-cover.spec.ts`: загрузка, перетаскивание мышью на десктопе и пальцем на телефоне, то же положение после перезагрузки и на другой ширине, удаление; чужой блог без обложки; из них 2 — смена почты E03-06 в `e2e/email-change.spec.ts`: код на новую почту, неверный код, смена и перезагрузка, занятый адрес; из них 1 — #387 в `e2e/editor.spec.ts`: Ctrl+B без выделения в новом пункте списка после паузы 1,5 с после Enter даёт жирный; из них 2 — ряд реакций E10-03 в `e2e/reactions.spec.ts`: своя реакция выделена, живые счётчики поста и комментария; из них 3 — учёт просмотров E07-06 в `e2e/views.spec.ts`: счётчик на карточке и отчёт о показах, отчёт об открытии и нет времени чтения без согласия, время чтения с согласием; из них 6 — разделы настроек E04-08 в `e2e/settings-sections.spec.ts`: адрес каждого раздела в обоих профилях, правая колонка на десктопе, список и «←» на телефоне, тема на другом устройстве в обоих профилях; из них 4 — арабский текст E02-14 в `e2e/rtl.spec.ts`: `dir=auto` и RTL у блока, цитаты и списка, огласовки шрифтом Noto Naskh Arabic с сайта, страница без арабского без шрифта, порядок пунктуации и цифр смешанных строк; из них 1 — превью E06-03 в `e2e/image.spec.ts`: пока картинка задержана, у коробки её цвет и размытый фон, после загрузки их нет; из них 4 — согласие на cookie E02-13 в `e2e/cookie-consent.spec.ts`; из них 2 — локализация E02-10 в `e2e/i18n.spec.ts`: язык страницы, нет ключей вместо текстов, даты ленты по-русски, страница 404; из них 4 — лучший комментарий ленты E08-15 в `e2e/feed-best-comment.spec.ts`: лучший под постами с комментариями и ничего под постом без них, обрезка длинного текста в три строки, переход к комментарию на странице поста, комментарий глубже прочитанного — на телефоне экран ветки, на десктопе на месте; из них 4 — лайки E10-01 в `e2e/likes.spec.ts`: гость видит счётчики и окно входа, гостевой комментарий без лайка, лайк поста ставится, переживает перезагрузку и снимается, свой пост и комментарий не лайкаются, чужой комментарий — да, лайк гостя ставится сам после входа; тест панели E07-04 ждёт включённое «Нравится»; из них 5 — дерево на телефоне E09-06 в `e2e/comment-branch.spec.ts`: переход по глубокой ветке и ширина текста (только mobile), экран ветки с возвратом к посту, «Ветка не найдена» для неверного адреса и ветки другого поста, скелетон и «Повторить»; два прежних теста подгрузки на месте в `e2e/comments.spec.ts` теперь только desktop; 258 по прогону после E09-05 #61 — прежняя запись 240 отставала; из них 4 — правка и удаление комментария E09-05 в `e2e/comment-edit.spec.ts`: меню только у своих и после срока только «Удалить», гость без меню, правка своего с «изменено», удалённый с ответами — заглушка, без ответов — пропадает; из них 2 — порядок комментариев E09-04 в `e2e/comment-sort.spec.ts`: «Популярные» по умолчанию, «По дате» с сервера, выбор переживает перезагрузку, нет переключателя у поста без комментариев; из них 4 — общие состояния E02-07 в `e2e/states.spec.ts`: 404 в каркасе при серверном рендеринге, 500 при сбое API, плашка «Нет подключения» и повторная загрузка, скелетон карточки по размерам настоящей; из них 4 — меню «…» E07-05 в `e2e/post-menu.spec.ts`: гость и чужой пост — меню читателя и копия ссылки, автор удаляет после подтверждения, сбой удаления; 4 — быстрый пост E05-29 в `e2e/quick-post.spec.ts`: гость, пустой и короткий пост, фото, «Развернуть» с отменой и повтором; из них 6 — дерево комментариев E09-02 в `e2e/comments.spec.ts`: вложение и сворачивание, глубокие ответы по кнопке, пусто, сбой дерева и ветки, 1000 комментариев; 1 — карточка ленты E08-02 в `e2e/feed.spec.ts`: автор и тема, короткий пост целиком, лонгрид с «Показать полностью», карточка в ширине колонки на 1440 и 390 px; из них 4 — шапка и панель действий E07-04 в `e2e/post.spec.ts`: шапка и панель с сервера, панель у низа окна при прокрутке, «Поделиться» копирует ссылку, на телефоне панель над нижней панелью и без «Написать» (только в профиле mobile); из них 3 новых — E08-03 в `e2e/feed.spec.ts`: сбой подгрузки с «Повторить», «назад» из поста на то же место без запроса к API, новый заход сверху; из них 4 — блок «Фото» E05-06 в `e2e/photo.spec.ts`: загрузка с индикатором, подпись и публикация с `srcset`, отказ формата не ломает пост, предел подписи, галерея на панели телефона (только в профиле mobile); из них 2 — телефон E05-10 в `e2e/mobile-editor.spec.ts`; из них 4 — публикация E05-09 в `e2e/publish.spec.ts`; из них 1 — блоки поста E07-03 в `e2e/post.spec.ts`; из них 1 — #331: курсив, маркер и ссылка переживают текст → заголовок → текст, а тест текст ↔ заголовок ждёт жирный после обратного превращения; из них 3 — лента «Свежее» E08-01 в `e2e/feed.spec.ts`: первая страница с сервера, порядок и следующая страница, переход к посту; из них 2 — вставка списков E05-02: списки Word и Google Docs становятся блоками «Список» с уровнями, список веб-страницы тоже; из них 6 — адрес поста E07-02 в `e2e/post.spec.ts`: пост по адресу со слагом, 301 с неверного слага и блога, пост без заголовка по id, та же схема в теме, 404, `/u/<блог>/comments` не пост; из них 4 — изображение E06-04 на витрине `/ui`: картинка вне экрана не загружается до прокрутки, место занято до загрузки и макет не сдвигается, ширина под плотность экрана, заглушки пустой и сломанной картинки того же размера; плюс в тесте настроек блога аватар берёт 64 или 192 px под плотность; из них 5 — меню блоков и перестановка E05-07: поиск в меню, стрелки и `Enter` (на телефоне — поле шторки и `Enter`), «/» и `Esc`, меню под «+» на десктопе и шторкой на телефоне, перетаскивание за «⋮⋮» с отменой и повтором, Ctrl+Shift+↑/↓ с кареткой; из них 1 — капча E03-12 после неудачных входов на `/login`; из них 4 — пароль E03-05: восстановление на `/login` с неверным кодом, коротким паролем и входом с новым паролем, восстановление в окне входа, смена в `/settings/account` с неверным и верным текущим паролем, гость в настройках аккаунта входит и возвращается; из них 3 — настройки блога E04-03: гость входит и возвращается в настройки; из них 2 — окно входа E03-04: гость нажимает «Нравится» на `/ui`, видит модальное окно или шторку на весь экран, axe без нарушений пустым и с ошибкой у поля, входит и возвращается на ту же страницу с поставленной отметкой один раз; регистрация в окне с ошибкой у поля, axe, переход ко входу и закрытие без действия; 6 — цитата и разделитель E05-05 на настоящем Editor.js: цитата из меню «+» с текстом и подписью раздельно, `Enter` текст → подпись → текст после цитаты, отмена и повтор; текст ↔ цитата через «Превратить в»; разделитель не редактируется и переживает отмену и повтор; `Backspace` в начале следующего блока удаляет его; `Delete` в конце предыдущего тоже; щелчок или стрелка выделяют, `Backspace` или `Delete` удаляют без пустого блока; 4 — списки E05-04 на настоящем Editor.js: `Enter`, `Tab` и `Shift+Tab` с пределом вложенности и делением пункта, пустой пункт поднимает вложенный и завершает список, смена типа через «Превратить в» без потери разметки и уровней, отмена и повтор с вложенностью; 2 — заголовки E05-03 на настоящем Editor.js: текст в «Заголовок», в «Подзаголовок» и обратно с тем же текстом, якорь `h-<id>` переживает правку, отмену и повтор; 4 — блок «Текст» E05-02 на настоящем Editor.js: панель над выделением и в поле зрения, жирный, курсив и маркер; ссылка — отказ `javascript:` с ошибкой, домен с `https://` и `rel`, ссылка переживает отмену и повтор из документа; вставка из Word; вставка веб-страницы без скриптов, фреймов и обработчиков; прежние 34 — прежняя запись 21 опережала на 1, из них 2 — сессия E03-03 на фейковом провайдере: сессия переживает истёкший токен доступа и билет хаба выдаётся, «Выйти» отзывает refresh-токен, завершает сессию провайдера и оставляет гостя; 5 — страница блога E04-02 на фейковом API: серверная отрисовка для гостя, вкладки без перезагрузки и по адресам, владелец с датой и кнопками и чужой блог, «Профиль» ведёт в свой блог, один адрес блога и блог без никнейма по UUID, 404 и ошибка с «Повторить»; 7 — редактор E05-01 на настоящем Editor.js: окно 800×700 по центру и развёрнутое, весь экран на телефоне поверх нижней панели и «Написать», пусто с плейсхолдером, отмена и повтор между блоками с кареткой, форматированный текст восстанавливается из документа без потерь, открытие из «Написать» и возврат по «✕», `Esc`; 2 — вход E03-02 на фейковом API: пустая форма, неверный и верный пароль, вход с cookie провайдера; код из письма, неверный адрес и код, таймер и новый код, вход; 2 — регистрация E03-01 на фейковом API: регистрация, неверный код, таймер и новый код, вход с cookie провайдера; ошибки короткого пароля, неверного адреса и занятой почты; 2 — реальное время E01-11 на фейковом API: гость не подключается, вошедший подключается по билету и переподключается после обрыва с новым; 2 — вход E18-02 на фейковом провайдере: код с PKCE, токенов нет в хранилищах и cookie страницы, ошибка обратного вызова; раскладка профиля, прилипание колонок, 1100 px, 800 px, экран «Меню», прокрутка наверх, безопасные зоны, витрины `/brand`, `/tokens`, `/ui` в светлой и тёмной теме; `admin`: 1 — дымовой) |

Число не должно уменьшаться; уменьшение объясняется в PR.

Интеграционные тесты одной сборки делят один PostgreSQL, у каждого класса — своя база и свой пул Npgsql. Тестовые
базы (`PostgresFixture.CreateDatabaseAsync`) закрывают простаивающие соединения через 10 с, а сервер в тестах
допускает 500 соединений: с пулом по умолчанию (300 с) сборка Identity после новых классов внешнего входа (#451, #452) упиралась в 100
соединений («53300: sorry, too many clients already»). Сворачивание ветки в `e2e/comments.spec.ts` («пост с 1000
комментариев») меряется внутри страницы, от нажатия до кадра после изменения дерева (#449), а не временем шага Playwright.

## Открытые вопросы к владельцу

Ни один не блокирует M0–M3.

| Вопрос | Когда нужен ответ | Связанные задачи |
| --- | --- | --- |
| «Показать новые» ([ADR-0108](adr/0108-show-new-posts.md)): новые посты ищутся перечитыванием первой страницы «Свежего» раз в минуту (гостям тоже), а не событием SignalR, которое пришло бы только вошедшим; плашка только на «Свежем», не на «Моей ленте» и ленте блога (рекомендация: так и оставить; событие можно добавить поводом для той же проверки) — подтвердить или задать иначе | До выхода M2 | #100 |
| Онбординг ([ADR-0102](adr/0102-onboarding.md)): ТЗ даёт имени 2–30 символов, а онбординг заполняет название блога, у которого предел 25 (FR-PROFILE). Сейчас 2–25. Оставить 25 (рекомендация) или поднять предел названия до 30 везде? И шаг «включение уведомлений» — добавить в онбординг после E12-05 или предложить уже прошедшим отдельной плашкой (рекомендация)? | До закрытия E03-10 | #93, #202 |
| Списки подписчиков ([ADR-0099](adr/0099-subscriber-lists.md)): списки публичные, как числа шапки; заблокированные в списках не скрываются, чтобы число совпадало со строками (рекомендация: так и оставить). И перенести критерий «скрытый профиль не раскрывает списки» в #181, закрыв #96, — или держать #96 открытой до #181? | До E04-10 | #96, #181 |
| Раздел настроек ([ADR-0070](adr/0070-settings-sections-and-account-preferences.md)): раздел «Основные» (тема, потом шрифт и язык) заменяет «Оформление» из `docs/ui/settings.md`; разделы — в правой колонке десктопа; настройки аккаунта — в своём модуле Preferences. Подходит ли так? | До E08-13 и E12-05 | #69, #102, #202 |
| Срок правки комментария ([ADR-0059](adr/0059-comment-edit-and-delete.md)): 15 минут от отправки, удалять можно всегда, удалённый с ответами — заглушка, текст не хранится (рекомендация: так и оставить; срок меняется настройкой `Comments:EditMinutes`) — подтвердить или задать иначе | До выхода M1 | #61 |
| Экран ветки на телефоне ([ADR-0060](adr/0060-comment-branch-screen.md)): адрес `<пост>/comments/<id>`, глубже одного чтения (3 уровня) ветка открывается этим экраном, на десктопе — «Показать ещё» на месте; читаемая ширина — колонка без трёх отступов (рекомендация: так и оставить) — подтвердить или задать иначе | До выхода M1 | #62 |
| Порядок комментариев ([ADR-0058](adr/0058-comment-sorting.md), [ADR-0081](adr/0081-post-and-comment-rating.md)): «Популярные» — по рейтингу (E10-05), при равенстве по числу ответов, потом старший; «По дате» — новые сверху, выбор в браузере на все посты (рекомендация: так и оставить) — подтвердить или задать иначе | До выхода M1 | #60, #73 |
| Лучший комментарий в ленте ([ADR-0062](adr/0062-feed-best-comment.md), [ADR-0081](adr/0081-post-and-comment-rating.md)): по рейтингу (E10-05), при равенстве более ранний; гостевой комментарий тоже может быть лучшим; ссылка на комментарий глубже прочитанного открывает экран его ветки (рекомендация: так и оставить) — подтвердить или задать иначе | До выхода M1 | #335, #73 |
| Веса реакций ([ADR-0081](adr/0081-post-and-comment-rating.md)): справочник в коде `ReactionWeights`, «Нравится» — 1, веса не отрицательные (реакция рейтинг не понижает); смена веса — правка кода с миграцией пересчёта (рекомендация: так и оставить; веса новых реакций E10-02 задать вместе с их набором; E10-02 задал всем семи видам вес 1, ADR-0101) — подтвердить или задать иначе | После E10-02 | #73, #87, #173 |
| Формула «горячести» ([ADR-0086](adr/0086-hotness-and-popular-feed.md)): логарифмическая, как «hot» у Reddit; коэффициенты по умолчанию — лайк (единица рейтинга) 1, комментарий 2, открытие 0,02, затухание 12,5 часа на десятикратные очки, окно перечитывания 7 дней, проход раз в 60 секунд (рекомендация: оставить и подстроить по данным stage — коэффициенты меняются настройками без выкладки); данные «горячести» в схеме `posts`, а не в отдельном модуле `Feeds`, как предполагал ADR-0043 (рекомендация: так, пока у постов нет событий) | До открытой беты | E08-05, E08-06 |
| Порог обрезки лонгрида в ленте ([ADR-0052](adr/0052-feed-post-card.md)): оставить «не больше 3 блоков и до 600 знаков текста» (рекомендация: по данным, без скачка после гидратации) или добавить предел по высоте экрана | До выхода M1 | #55 |
| Капча входа ([ADR-0038](adr/0038-sign-in-captcha.md)): оставить свою проверку вычислением `ProofOfWork` (рекомендация: без третьих лиц и ключей) или подключить Cloudflare Turnstile / hCaptcha (нужны ключ сайта, секретный ключ, реализация `ICaptcha` и виджет) | до запуска M1 | #32 |
| Создание темы ([ADR-0084](adr/0084-topic-creation-by-people.md)): лимит 5 тем на аккаунт, название от 2 символов, стоп-список по умолчанию `admin,moderator,official,support,администр,модератор,официальн,поддержк` (настоящий — по политике контента, задаётся настройкой `Blogs:Topics:StopWords`), список зарезервированных адресов; ТЗ говорило «тема создаётся администрацией», задача — пользователями (рекомендация: оставить пользователей с лимитом) | До открытой беты (M5) | E11-11 |
| Финальное название и бренд (сейчас временные kitabber и палитра ADR-0011; меняются правкой `brand/brand.json`) | До открытой беты (M5) | E02-01 |
| Подтвердить ADR-0002: свой провайдер OpenID Connect с первого дня | До начала M1 | E18-01, E18-02 |
| Для выкладки входа на stage (ADR-0018) — завести секреты `IDENTITY_SIGNING_KEY`, `IDENTITY_ENCRYPTION_KEY`, `SESSION_PASSWORD` (команды — DEPLOY.md) | До первой выкладки на stage | E18-02 |
| Для выкладки медиа на stage (ADR-0030) — S3-совместимый бакет и ключ доступа: переменные `MEDIA_STORAGE_URL`, `MEDIA_STORAGE_REGION`, `MEDIA_STORAGE_BUCKET`, `MEDIA_STORAGE_ACCESS_KEY` и секрет `MEDIA_STORAGE_SECRET_KEY` ([DEPLOY.md](DEPLOY.md#что-нужно-от-владельца)); без них выкладка останавливается. Подтвердить ADR-0030: файл с расширением другого формата отклоняется, пределы 20/500/100/50 МБ | До следующей выкладки на stage | E06-01 |
| Для раздачи изображений на stage (ADR-0037) — секреты `IMGPROXY_KEY` и `IMGPROXY_SALT` (hex, `openssl rand -hex 32`, DEPLOY.md); по желанию — CDN перед сайтом и ключ хранилища только для чтения для imgproxy. Подтвердить ADR-0037: ключ объекта виден (в Base64) в подписанной ссылке, что уточняет п. 6 ADR-0030 | До выкладки медиа на stage | E06-02 |
| Для телеметрии на stage (ADR-0069) — запись DNS для Grafana, переменная `GRAFANA_ADDRESS` и секрет `GRAFANA_ADMIN_PASSWORD` в окружении `stage` (без них выкладка на stage останавливается на проверке значений); на сервере ещё около 1 ГБ памяти и до 20 ГБ диска под 14 дней хранения, новых портов не нужно; позже — канал оповещений алерта `ApiServerErrors` (Telegram или почта) | До подключения stage | #318 |
| Какие проекты будут объединять аккаунты и на каком они стеке | До M7 | E18-06 |
| Хранилище телеметрии: на stage — свой Grafana-стек (решено 21 ноября 2026, сделано в #318, ADR-0069; от владельца — DNS для `GRAFANA_ADDRESS`, секрет `GRAFANA_ADMIN_PASSWORD`, позже канал алертов: Telegram или почта); для production — общее решение под Grafana и телеметрию для всех проектов владельца, скорее всего одно хранилище на все проекты (#321) | Production — до открытой беты | E01-09, #318, #321 |
| Хостинг (сервер с Docker), домен, почтовый сервис, хранилище; что задать для stage — в [DEPLOY.md](DEPLOY.md#что-нужно-от-владельца) | До первой выкладки на stage | E01-03, E01-12 |
| Почтовый сервис: решено — транзакционный сервис на бесплатном тарифе для stage и production; выбор конкретного сервиса, домен отправителя (SPF, DKIM, DMARC) и переменные `SMTP_*` — после первого релиза (#320) | После первого релиза | E01-12, #320 |
| Первый релиз — 0.1.0, не 1.0.0: Release PR release-please копит изменения и не сливается до первой беты (решение владельца 21 ноября 2026) | До первой беты | release-please |
| Где держать секреты стендов: выкладка сделана на секретах окружений GitHub (рекомендация ADR-0007) — подтвердить или выбрать другое | До первой выкладки на stage | E01-03 |
| Ключи внешних провайдеров входа (код готов, #91, [ADR-0115](adr/0115-external-sign-in.md)): приложение Яндекс ID (ClientID и secret, доступ к почте), VK ID (ID веб-приложения), OAuth-клиент Google (Client ID и secret), бот Telegram (токен и `/setdomain`); у каждого провайдера зарегистрировать адрес возврата `{Identity:Issuer}api/v1/sign-in/external/{yandex,vk,google}/callback`, у Telegram — домен сайта (возврат на `/auth/telegram`); ключи — секреты стенда `Identity__External__*` (docs/CONFIG.md) | До M2 | E03-07 |
| Регистрация через Telegram: сейчас Telegram (и любой провайдер без подтверждённой почты) входит только в аккаунт, к которому уже привязан; новый человек сначала входит по почте. Нужна ли регистрация в один шаг через Telegram — это аккаунты без почты и решение про их события ([ADR-0115](adr/0115-external-sign-in.md), п. 4) | До M2 | E03-07, #119 |
| Подтвердить ADR-0013: витрина компонентов — страница `/ui` сайта вместо Histoire или Storybook, как предлагала задача #14 | До E02-05 (#15) | E02-04 |
| Итоговый текст правил сообщества | До M4 | E15-17 |
| Бот Telegram, служебный аккаунт и `api_id`, сервер вне России, тестовый канал | До M6 | E19-01 |
| Переносить ли мост с Telegram раньше (до открытой беты) | До конца M3 | эпик E19 |
| Предложения в спецификациях экранов ([docs/ui](ui/README.md), пометка «предложение, требует подтверждения»); пункты меню «коллекция» и «подписка» из раздела 5 ТЗ не показываются. Каркас десктопа #15 сделан по ним: промежуток колонок `--space-6`, высота шапки `--space-16`, адреса меню (`frontend/utils/navigation.ts`), тема хранится в браузере, а не в аккаунте. Каркас телефона #16: нижняя панель и экран «Меню» (`/menu`, отдельная страница) показываются ниже 925 px, плавающая «Написать» — ниже 640 px; адреса `/menu`, `/profile`, `/drafts`, `/bookmarks`, `/settings`; на экране «Меню» есть темы и «Выйти»; на этих предложениях построены и сквозные тесты #17 | До экранов лент (M1): пока ответа нет, тесты каркаса фиксируют предложения | E02-03, E01-16 |
| Юридические документы | До M7 | E17-08 |
| Нужен ли короткий числовой номер блога для адреса `id123` из ТЗ или хватит UUID (страница блога E04-02 пока живёт на `/u/<никнейм>` и `/u/<UUID>`; адрес `/u/` — предложение ui/blog.md) | До M2 (E04-05 обошлась без него) | E04-01 |
| Смена никнейма (ADR-0090, предложения): сколько держать оставленный никнейм за блогом (сейчас 90 дней) и считать ли первый никнейм блога сменой (сейчас нет, отсчёт 30 дней — со второй) | До M2 | E04-05 |
| Поиск (ADR-0094, предложения): адрес OpenSearch по умолчанию `http://localhost:9200` в воркерах и «снятый с публикации» пост = удалённый, пока снятия нет (E15) | До M2 | E14-01 |
| Подсказки поиска (ADR-0097, предложения): 4 блога и 5 постов в подсказках, 10 запросов истории, пауза 300 мс, лимит 120 в минуту; скрытые из лент посты и темы в поиске видны; Enter без выбранной строки только пишет историю до страницы результатов E14-03 | До M2 | E14-02 |
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
| Окружения `stage` и `production`, переменная `DEPLOY_STAGE` | Подключение стендов, в том числе Grafana стенда (`GRAFANA_ADDRESS`, `GRAFANA_ADMIN_PASSWORD`, ADR-0069); полный список значений — [DEPLOY.md](DEPLOY.md#что-нужно-от-владельца) | Settings → Environments, Settings → Variables |
