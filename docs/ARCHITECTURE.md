# Архитектура: структура репозитория

Стек и границы модулей — в [ADR-0001](adr/0001-stack.md). Здесь — как это разложено по каталогам и какими
инструментами проверяется. Документ обновляется в том же PR, что меняет структуру.

## Каталоги

```text
backend/                   решение .NET (Platform.slnx)
  Directory.Build.props    общие настройки проектов: net10.0, nullable, анализаторы, предупреждения = ошибки
  Directory.Packages.props версии NuGet-пакетов (центральное управление пакетами)
  src/Api/                 Platform.Api — веб-хост API (ASP.NET Core)
  src/Workers/             Platform.Workers — хост фоновых воркеров
  src/Modules/             модули из ADR-0001 (появляются с задачами модулей)
  tests/                   тестовые проекты (каркас — задача E01-15)
frontend/                  веб-клиент на Nuxt 3 (Vue 3, TypeScript)
admin/                     админ-панель на Vue 3 + Vite (TypeScript)
tools/                     сидер, генерация клиента API, фейковые сервисы (появится в своих задачах)
deploy/                    compose-файлы и инструкции выкладки (появится в E01-04)
docs/                      ТЗ, ADR, состояние проекта
scripts/verify.ps1         единственная команда проверки
```

`frontend/` и `admin/` — независимые пакеты pnpm, у каждого свой `pnpm-lock.yaml` и свой
`pnpm-workspace.yaml` (там же список пакетов, которым разрешены скрипты установки, `allowBuilds`).

## Имена в коде

Название продукта временное и задаётся в одном месте, поэтому в именах сборок, пространств имён и пакетов его
нет: сборки называются `Platform.*`, пакеты — `platform-frontend` и `platform-admin`. Модули получат имена
вида `Platform.Modules.<Модуль>` и `Platform.Modules.<Модуль>.Contracts`.

## Инструменты

| Что | Версия | Где задана |
| --- | --- | --- |
| .NET SDK | 10.0.400 и новее в той же линейке | `global.json` |
| Node.js | 22 и новее | `engines` в `package.json` |
| pnpm | 12.21.0 | `packageManager` в `package.json` |

## Линтеры и форматирование

| Часть | Проверка | Настройки |
| --- | --- | --- |
| Все файлы | Кодировка, переводы строк, отступы | `.editorconfig` |
| backend | `dotnet build -warnaserror` с анализаторами (`AnalysisLevel=latest-recommended`, стиль кода при сборке) и `dotnet format --verify-no-changes` | `.editorconfig` (раздел `[*.cs]`), `backend/Directory.Build.props` |
| frontend | ESLint (`@nuxt/eslint`, плоская конфигурация) и `prettier --check` | `frontend/eslint.config.mjs`, общий `.prettierrc.json` |
| admin | ESLint (`eslint-plugin-vue`, `@vue/eslint-config-typescript`) и `prettier --check` | `admin/eslint.config.mjs`, общий `.prettierrc.json` |

Prettier отвечает за форматирование, ESLint — за ошибки; правила ESLint, спорящие с Prettier, выключены
через `eslint-config-prettier`. Автоисправление: `pnpm run format` в каталоге приложения и `dotnet format` в
`backend/`.

## Что запускает проверка

`scripts/verify.ps1` (его же запускает CI) проверяет:

- документы и ссылки в них, JSON-файлы в корне;
- структуру репозитория: части выше на месте и описаны в этом документе;
- backend: сборку без предупреждений, форматирование, тесты;
- frontend и admin: установку по lock-файлу, линтеры, проверку типов, сборку (`pnpm build`), тесты, если
  они есть;
- саму себя (`scripts/verify-selftest.ps1`): упавший шаг роняет проверку, линтеры отвергают плохой код, workflow
  `ci` вызывает этот скрипт и укладывается в 10 минут.

Обвязка шагов (таблица результатов, отчёты о тестах, код выхода) — в `scripts/verify-lib.ps1`. Workflow
`.github/workflows/ci.yml` только готовит машину (.NET по `global.json`, pnpm, Node 22, кэши NuGet и pnpm),
запускает `scripts/verify.ps1` и выкладывает `artifacts/test-results` артефактом `test-results`; своих проверок
в нём нет.
