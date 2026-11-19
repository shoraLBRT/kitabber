# kitabber

Этичная, преимущественно текстовая UGC-платформа: авторский контент, лонгриды, научные статьи. Название
рабочее.

- Функции и раскладка интерфейса повторяют DTF: десктоп и мобильная версия.
- Мост с Telegram: импорт истории канала в один клик, ретрансляция новых постов, гостевые комментарии.
- Без монетизации. Без пошлости, оскорблений и запрещённого контента.

## Состояние

Код ещё не начат: в репозитории лежат требования, решения и порядок работ. Текущее состояние —
[docs/PROJECT_STATE.md](docs/PROJECT_STATE.md).

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
