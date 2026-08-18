# Architecture Decision Records (ADR)

Записи о значимых архитектурных решениях: контекст, само решение, рассмотренные
альтернативы и последствия. Цель - чтобы новый участник понимал, **почему**
проект устроен так, а не только **как**.

Общий обзор архитектуры - [docs/architecture.md](../architecture.md).

## Решения

Порядок тематический, а не хронологический: номер отражает тему решения.
Стартовый набор написан одним заходом, поэтому хронологии у него нет.
Новые ADR получают следующий свободный номер и в этом индексе встают
в свою тематическую группу.

### Репозиторий и сборка

| ADR                                                                  | Решение                                                     | Статус  |
| -------------------------------------------------------------------- | ----------------------------------------------------------- | ------- |
| [Nx](0001-nx.md)                                                     | Nx для управления монорепозиторием                          | Принято |
| [Yarn](0002-yarn.md)                                                 | Yarn 4 (Berry) как пакетный менеджер                        | Принято |
| [Источники пакетов](0003-nuget-config-package-sources.md)            | nuget.config фиксирует источники (один - nuget.org)         | Принято |
| [Анализ кода и editorconfig](0004-code-analysis-and-editorconfig.md) | Встроенные .NET-анализаторы + SonarAnalyzer + правила стиля | Принято |
| [Aspire TS AppHost](0005-aspire-typescript-apphost.md)               | .NET Aspire с AppHost на TypeScript                         | Принято |
| [Локальная конфигурация](0037-local-configuration-and-secrets.md)    | Приватные файлы у потребителя, реестр и образцы .example    | Принято |

### Backend: архитектура и слои

| ADR                                                                  | Решение                                                    | Статус  |
| -------------------------------------------------------------------- | ---------------------------------------------------------- | ------- |
| [Модульный монолит](0006-modular-monolith.md)                        | Backend - модульный монолит на Clean Architecture          | Принято |
| [PostgreSQL](0007-postgresql.md)                                     | PostgreSQL как СУБД, имена в схеме - snake_case            | Принято |
| [Тактический DDD (SeedWork)](0008-tactical-ddd-seedwork.md)          | SeedWork; VO на record; домен - источник инвариантов       | Принято |
| [Структура Domain-слоя](0009-domain-layer-folder-structure.md)       | Папка-агрегат: Models/ + Repositories/, namespace зеркалит | Принято |
| [Структура Application](0010-application-layer-folder-structure.md)  | Операции в Commands/Queries по подпапкам, класс на файл    | Принято |
| [Граница Api/Application](0011-api-application-boundary.md)          | Команда - полный вход операции; край дообогащает           | Принято |
| [Медиатор/шина Wolverine](0012-wolverine-mediator-and-messaging.md)  | Wolverine как медиатор/CQRS и шина сообщений               | Принято |
| [Static-хендлеры Wolverine](0013-wolverine-static-handlers.md)       | Хендлер - static-класс со static-методом Handle            | Принято |
| [Профиль durability Wolverine](0014-wolverine-durability-profile.md) | Solo + message store; под экспортом Solo без store         | Принято |
| [Архитектурные тесты](0015-architecture-tests.md)                    | NetArchTest.Rules проверяет правила слоёв                  | Принято |

### Backend: сквозные механизмы

| ADR                                                                 | Решение                                                | Статус  |
| ------------------------------------------------------------------- | ------------------------------------------------------ | ------- |
| [Версионирование API](0016-api-versioning.md)                       | Версия в URL-сегменте, один документ OpenAPI           | Принято |
| [Ошибки и CORS](0017-error-handling-and-cors.md)                    | Формат ошибок RFC 9457 (IExceptionHandler) + CORS      | Принято |
| [Ошибки, коды и i18n](0018-domain-errors-codes-and-localization.md) | DomainException + коды ErrorCodes + resx-локализация   | Принято |
| [Валидация ввода](0019-input-validation.md)                         | FluentValidation на командах через Wolverine           | Принято |
| [long строкой](0020-long-as-string.md)                              | long сериализуется строкой (точность int64 в JS)       | Принято |
| [Локализация контента](0021-entity-content-localization.md)         | Контент сущностей - таблица переводов + инлайн-дефолт  | Принято |
| [Глобальное мягкое удаление](0022-global-soft-delete.md)            | ISoftDeletable + интерцептор + query-filter конвенция  | Принято |
| [Аутентификация по OIDC](0023-authentication-oidc.md)               | Resource server, JWKS; из токена - только идентичность | Принято |
| [Провайдер идентичности](0036-identity-provider-logto.md)           | Logto своим контейнером в обеих средах                 | Принято |
| [Rate limiting](0024-rate-limiting.md)                              | Встроенный rate limiter, политика opt-in, 429 RFC 9457 | Принято |
| [Конвенции HTTP-клиента](0025-outbound-http-client-conventions.md)  | Типизированный клиент, устойчивость из ServiceDefaults | Принято |

### Контракт API и фронтенд

| ADR                                                     | Решение                                                  | Статус  |
| ------------------------------------------------------- | -------------------------------------------------------- | ------- |
| [Автогенерация API](0026-auto-generate-api.md)          | TS-клиент генерируется из OpenAPI через Orval            | Принято |
| [Фронтенд - SPA](0027-frontend-spa.md)                  | Фронтенд - самостоятельное SPA, а не библиотека виджетов | Принято |
| [Структура фронтенда](0028-frontend-structure.md)       | Уровни page/feature, изоляция через boundaries           | Принято |
| [TanStack Query](0029-tan-stack-query.md)               | Серверное состояние через TanStack Query                 | Принято |
| [Доступность фронтенда](0030-frontend-accessibility.md) | Базовый a11y: Storybook addon + eslint-plugin-vuejs-a11y | Принято |

### Тестирование

| ADR                                               | Решение                                           | Статус  |
| ------------------------------------------------- | ------------------------------------------------- | ------- |
| [Интеграционные тесты](0031-integration-tests.md) | Testcontainers + TUnit + Respawn для API-тестов   | Принято |
| [Фронтенд-тесты](0032-frontend-unit-tests.md)     | Vitest + happy-dom + @vue/test-utils              | Принято |
| [Моки API (MSW)](0033-api-mocks-msw.md)           | MSW: моки API для Storybook, dev и тестов (Orval) | Принято |

### Документация

| ADR                                                         | Решение                                              | Статус  |
| ----------------------------------------------------------- | ---------------------------------------------------- | ------- |
| [Слои документации](0034-documentation-lifecycle-layers.md) | design-time (arch-design) vs steady-state (Diátaxis) | Принято |
| [Документация Diátaxis](0035-documentation-diataxis.md)     | Документация по Diátaxis: 4 квадранта, per-module    | Принято |

## Формат

Каждый ADR - отдельный файл с порядковым номером и темой решения
(`NNNN-<slug>.md`), в стиле: **Статус / Дата / Контекст / Решение / Почему не
альтернативы / Плюсы / Минусы / Последствия**. Новый ADR - следующий по
порядку номер; в индекс выше добавляется строка в подходящую группу.
