# kitabber

Этичная, преимущественно текстовая UGC-платформа: авторский контент, лонгриды, научные статьи. Название
рабочее.

- Функции и раскладка интерфейса повторяют DTF: десктоп и мобильная версия.
- Мост с Telegram: импорт истории канала в один клик, ретрансляция новых постов, гостевые комментарии.
- Без монетизации. Без пошлости, оскорблений и запрещённого контента.

## Состояние

Есть каркас: решение .NET, веб-клиент, админка и окружение для локальной разработки; функций продукта пока
нет. Текущее состояние — [docs/PROJECT_STATE.md](docs/PROJECT_STATE.md).

## Локальное окружение

Нужен Docker с Compose v2. Три команды поднимают всю инфраструктуру:

```sh
git clone https://github.com/shoraLBRT/kitabber.git && cd kitabber
cp .env.example .env
docker compose up -d --wait
```

`--wait` возвращает управление, когда у всех сервисов прошёл healthcheck. Порты и пароли — в `.env` (шаблон —
[.env.example](.env.example)); сервисы слушают только `127.0.0.1`. Адреса при значениях из шаблона:

| Сервис | Адрес |
| --- | --- |
| PostgreSQL | `localhost:5432` |
| Redis | `localhost:6379` |
| RabbitMQ | AMQP `localhost:5672`, панель http://localhost:15672 |
| OpenSearch | http://localhost:9200 (локально без TLS и пароля) |
| MinIO | S3 API http://localhost:9000, консоль http://localhost:9001 |
| imgproxy | http://localhost:8081 (берёт исходники из MinIO) |
| Mailpit | SMTP `localhost:1025`, письма — http://localhost:8025 |
| ClickHouse | HTTP http://localhost:8123, native `localhost:19000` |

Если порт занят, поменяйте его в `.env`. Остановить — `docker compose down`, стереть данные —
`docker compose down -v`. Все внешние сервисы локально — фейки: почта уходит в Mailpit, файлы — в MinIO.

Схемы базы создаёт мигратор: `dotnet run --project backend/src/Migrator` (подробнее — в
[ARCHITECTURE.md](docs/ARCHITECTURE.md#база-данных)).

## Документы

| Документ | О чём |
| --- | --- |
| [docs/spec.md](docs/spec.md) | Техническое задание: исследование DTF, роли, требования, раскладки, архитектура |
| [docs/telegram-bridge.md](docs/telegram-bridge.md) | Мост с Telegram |
| [docs/unified-accounts.md](docs/unified-accounts.md) | Подготовка к единому аккаунту нескольких проектов |
| [docs/content-policy.md](docs/content-policy.md) | Правила контента и модерация |
| [docs/ROADMAP.md](docs/ROADMAP.md) | Вехи и порядок задач |
| [docs/TESTING.md](docs/TESTING.md) | Как проект тестируется |
| [docs/adr/](docs/adr/) | Архитектурные решения |
| [CLAUDE.md](CLAUDE.md) | Правила работы для сессий разработки |

## Как идёт работа

Задачи живут в GitHub Issues, порядок — в `docs/ROADMAP.md`. Одна задача — один PR. Перед PR должна пройти
одна команда:

```powershell
pwsh scripts/verify.ps1
```

Коммиты и версии — по стандарту [habze](https://github.com/shoraLBRT/habze/blob/main/STANDARD.md).
