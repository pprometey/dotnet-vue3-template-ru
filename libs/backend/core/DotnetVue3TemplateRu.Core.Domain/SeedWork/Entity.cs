using System.Diagnostics.CodeAnalysis;

namespace DotnetVue3TemplateRu.Core.Domain.SeedWork;

/// <summary>
/// Базовый тип сущности: равенство по идентичности - две сущности равны, если это один и тот
/// же конкретный тип и один и тот же Id. Id присваивается при создании и не меняется. Этим
/// сущность отличается от value object, который сравнивается по значению (моделируется record'ом).
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    [SuppressMessage(
        "Major Code Smell",
        "S3875:\"operator==\" should not be overloaded on reference types",
        Justification = "Равенство сущности - по идентичности (ADR-0008). Оператор идёт парой "
            + "к переопределённым Equals и GetHashCode; без него сравнение сущностей молча "
            + "вернулось бы к сравнению ссылок.")]
    public static bool operator ==(Entity? left, Entity? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity? left, Entity? right) => !(left == right);
}
