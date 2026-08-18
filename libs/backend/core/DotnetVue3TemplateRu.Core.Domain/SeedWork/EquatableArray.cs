using System.Collections;

namespace DotnetVue3TemplateRu.Core.Domain.SeedWork;

/// <summary>
/// Неизменяемая обёртка над массивом со значимым равенством по содержимому (поэлементный
/// SequenceEqual). Нужна, чтобы value object на record'е с вложенной коллекцией сравнивался по
/// элементам, а не по ссылке списка: авто-равенство record'а сравнивает поля, и когда поле -
/// EquatableArray, сравнение идёт по значению. Элемент обязан сам иметь значимое равенство.
/// </summary>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly T[] _items;

    public EquatableArray(IEnumerable<T> items) => _items = [.. items];

    public T this[int index] => _items[index];

    public int Count => _items?.Length ?? 0;

    public ReadOnlySpan<T> AsSpan() => _items ?? [];

    public bool Equals(EquatableArray<T> other) => AsSpan().SequenceEqual(other.AsSpan());

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (T item in AsSpan())
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_items ?? [])).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
