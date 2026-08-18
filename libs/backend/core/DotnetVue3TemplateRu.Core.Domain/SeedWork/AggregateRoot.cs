namespace DotnetVue3TemplateRu.Core.Domain.SeedWork;

/// <summary>
/// Корень агрегата: сущность с токеном оптимистичной блокировки (rowversion). Наследуют корни,
/// которым нужна защита от потерянного обновления (конкурентная перезапись -> конфликт 409).
/// Version хранится как rowversion, настраивается fluent-конфигом в Infrastructure.
/// </summary>
public abstract class AggregateRoot : Entity
{
    public byte[] Version { get; protected set; } = [];
}
