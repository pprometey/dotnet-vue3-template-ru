# ADR: Архитектурные тесты слоёв (NetArchTest.Rules)

- Статус: Принято
- Дата: 2026-06-18
- Контекст: монорепозиторий DotnetVue3TemplateRu, backend - модульный монолит на Clean Architecture (ADR-0006); правило зависимостей слоёв держится на ProjectReference-графе и соглашении

## Контекст

Backend организован по слоям Clean Architecture с правилом зависимостей `Api -> Application -> Domain` и `Infrastructure -> Application/Domain` (см. Project Map в [.claude/project-map.md](../../.claude/project-map.md)). Application при этом не должен ничего знать про EF Core - доступ к данным живёт в Infrastructure. Эти границы держатся только на двух вещах: вручную выставленных `ProjectReference` и дисциплине разработчика. Ничто не мешает в коде Application написать `using` на тип из Infrastructure, если ссылка на проект появится по ошибке (например, транзитивно или при копипасте), - компилятор это пропустит, а ревью может не заметить.

На фронтенде аналогичные границы проверяются принудительно: `eslint-plugin-boundaries` ([eslint.config.mjs](../../eslint.config.mjs)) запрещает модулям импортировать друг друга в обход `core`. Backend - без симметричной защиты. Архитектурный тест закрывает этот разрыв: нарушение правила слоёв падает как обычный тест, а не доезжает до ревью или прод.

Ограничения: уважать "Simplicity First" (не тащить тяжёлый фреймворк ради нескольких правил); не смешивать быстрые статические проверки с медленными интеграционными тестами на Testcontainers (ADR-0031), которым нужен Docker.

## Решение

**NetArchTest.Rules в отдельном быстром тест-проекте на TUnit.**

- Отдельный проект `tests/DotnetVue3TemplateRu.ArchitectureTests` ссылается на слои всех проверяемых модулей (сейчас только Core) и на Api - ссылки нужны, чтобы получить ассемблии для анализа по типу-якорю (например `typeof(Note).Assembly`). Правила разложены по файлам: `LayeringTests` (правила слоёв) и `DomainModelingTests` (тактический DDD: сущность не record и подобное). Каждый новый модуль добавляет свой файл `<Module>LayeringTests`. Стек тестов - TUnit, как и в интеграционных тестах (ADR-0031), но без Testcontainers/Respawn/Mvc.Testing: тесты чисто статические, гоняются без Docker за миллисекунды.
- Библиотека - **NetArchTest.Rules**: fluent-API над рефлексией ассемблий (`Types.InAssembly(...).ShouldNot().HaveDependencyOnAny(...)`). Проверяемые инварианты:
  - Domain не зависит от Application, Infrastructure, Api.
  - Application не зависит от Infrastructure, Api и от EF Core (`Microsoft.EntityFrameworkCore`, `Microsoft.Data.SqlClient`).
  - Infrastructure не зависит от Api.
- Утверждения пинуют полный список нарушителей пустым (`await Assert.That(result.FailingTypeNames ?? []).IsEmpty()`), а не булев `IsSuccessful` - при провале тест печатает конкретные типы-нарушители (правило 8 из [.claude/CLAUDE.md](../../.claude/CLAUDE.md)).
- Проект зарегистрирован в `DotnetVue3TemplateRu.slnx` и в Nx (`architecture-tests`), версия пакета - в Central Package Management ([Directory.Packages.props](../../Directory.Packages.props)).

Для Api отдельного правила нет: Api - внешний слой, ему разрешено зависеть от всех нижних.

## Почему не альтернативы

**Дописать тесты в существующий `DotnetVue3TemplateRu.IntegrationTests`.** Меньше файлов, но смешивает быстрые статические проверки с медленными интеграционными (Testcontainers, Docker): arch-тесты тогда нельзя прогнать без поднятого Docker и быстрого gate в CI не получится. Отдельный проект изолирует их и оставляет мгновенными.

**ArchUnitNET** вместо NetArchTest. Мощнее и выразительнее, но и тяжелее по API; для набора правил "слой X не зависит от слоя Y" возможностей NetArchTest с запасом хватает, а fluent-синтаксис проще читается. При росте требований (правила на именование, атрибуты, циклы) к ArchUnitNET можно вернуться.

**Полагаться только на ProjectReference-граф.** Граф задаёт _разрешённые_ ссылки, но не _запрещает_ нежелательные зависимости внутри уже сослоенных проектов (например, Application не должен тянуть EF Core, хотя ссылку на Infrastructure кто-то мог добавить). Тест фиксирует инвариант явно и ловит регресс.

## Плюсы

- Правило слоёв проверяется автоматически: нарушение падает как тест, симметрично фронтенду (`eslint-plugin-boundaries`).
- Тесты статические и мгновенные - не требуют Docker, годятся как быстрый gate в CI до тяжёлых интеграционных тестов.
- Запрет EF Core в Application фиксируется явно, а не держится на дисциплине.
- При провале видно конкретный тип-нарушитель - диагностика без догадок.

## Минусы

- Ещё одна зависимость (`NetArchTest.Rules`) и ещё один тест-проект в решении.
- Правила заданы строковыми именами ассемблий: переименование проекта требует синхронной правки строк в тестах.
- Проверяется только то, что описано правилами; новый вид нежелательной зависимости нужно дописывать вручную.

## Последствия

- Новый слой или домен (`libs/backend/<domain>/`) обязан соблюдать правило зависимостей, иначе `LayeringTests` упадёт; при добавлении домена правила расширяются по тому же образцу (новые ассемблии-якоря, те же `HaveDependencyOnAny`).
- Arch-тесты гоняются отдельно от интеграционных: `dotnet test tests/DotnetVue3TemplateRu.ArchitectureTests/...` или `nx test architecture-tests`; в CI их логично ставить раньше интеграционных как быстрый барьер.
- Backend получил автоматическую проверку границ, симметричную фронтенд-границам из ADR-0028 (`eslint-plugin-boundaries`).

## Ссылки

- [ADR-0006: модульный монолит на Clean Architecture](0006-modular-monolith.md)
- [ADR-0031: интеграционные тесты (Testcontainers + TUnit)](0031-integration-tests.md)
- [docs/spec/test-strategy.md](../spec/test-strategy.md)
