using JasperFx.Resources;
using Wolverine;
using Wolverine.Postgresql;

namespace DotnetVue3TemplateRu.Core.Infrastructure.Messaging;

/// <summary>
/// Профиль обмена Wolverine. Оба значения работают в <see cref="DurabilityMode.Solo"/>
/// (async-очереди доступны, один узел без выборов лидера) и различаются только наличием
/// message store. MediatorOnly не используется - он отключил бы асинхронное сообщение
/// целиком (в т.ч. локальные очереди).
/// </summary>
public enum MessagingProfile
{
    /// <summary>
    /// Solo без хранилища: локальные очереди только в памяти. Для build-time экспорта
    /// OpenAPI (под GetDocument, без БД) и запусков без доступной БД.
    /// </summary>
    InMemory,

    /// <summary>
    /// Solo + message store в PostgreSQL: фундамент для durable-очередей и транзакционного outbox.
    /// Рантайм-профиль (dev/прод).
    /// </summary>
    Persistent,
}

public static class WolverineDurability
{
    /// <summary>
    /// Настраивает durability Wolverine по профилю. Всегда <see cref="DurabilityMode.Solo"/>
    /// (sync через InvokeAsync и async через PublishAsync доступны одновременно); при
    /// <see cref="MessagingProfile.Persistent"/> дополнительно поднимает message store.
    /// Локальные очереди остаются буферными (in-memory) по умолчанию - durable включается
    /// точечно per-queue там, где потеря сообщения при рестарте недопустима.
    /// </summary>
    public static WolverineOptions UseDotnetVue3TemplateRuDurability(
        this WolverineOptions options,
        MessagingProfile profile,
        string? connectionString)
    {
        options.Durability.Mode = DurabilityMode.Solo;

        if (profile == MessagingProfile.Persistent)
        {
            // Служебные таблицы уходят в отдельную схему: psql показывает предметную область
            // без примеси, а Respawn чистит между тестами только public и не выдёргивает
            // конверты из-под работающего durability agent (ADR 0007, ADR 0031).
            options.PersistMessagesWithPostgresql(connectionString!, "wolverine");

            // Wolverine владеет схемой message store и создаёт/обновляет её на старте
            // Схема не входит в EF-миграции; апгрейд Wolverine,
            // меняющий таблицы, применяется автоматически при следующем старте (ADR 0014).
            options.Services.AddResourceSetupOnStartup();
        }

        return options;
    }
}
