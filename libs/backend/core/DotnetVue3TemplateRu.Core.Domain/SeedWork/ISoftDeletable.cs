namespace DotnetVue3TemplateRu.Core.Domain.SeedWork;

/// <summary>
/// Маркер сущности с мягким удалением: вместо физического DELETE строка помечается временем
/// удаления (DeletedAtUtc), скрывается из обычных запросов глобальным query-filter и остаётся в
/// БД. Оживление (сброс DeletedAtUtc) - явная доменная операция, поэтому здесь только чтение
/// признака. Свойство - доменное состояние; сам механизм (интерцептор, переводящий Remove в
/// мягкое удаление, и query-filter) живёт в Infrastructure, чтобы домен не тянул инфра-атрибуты.
/// </summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAtUtc { get; }
}
