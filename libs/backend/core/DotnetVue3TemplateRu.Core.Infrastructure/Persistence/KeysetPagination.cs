using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace DotnetVue3TemplateRu.Core.Infrastructure.Persistence;

/// <summary>
/// Страница курсорной (keyset) пагинации: элементы, курсор на следующую страницу (null - дальше
/// нет) и признак наличия следующей. Курсор непрозрачен для клиента.
/// </summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);

/// <summary>
/// Keyset-пагинация по составному ключу (сортировочное поле + Id-тайбрейк): страница берётся
/// условием `(SortKey, Id) > cursor ORDER BY SortKey, Id LIMIT size+1`, что даёт стабильный обход
/// без пропусков/дублей на глубоких страницах (в отличие от offset). Ключ и тайбрейк должны быть
/// упорядочиваемыми (`IComparable`), лежать в запрашиваемой таблице и совпадать с ORDER BY.
/// Курсор - base64(JSON) пары (ключ, Id).
/// </summary>
public static class KeysetPagination
{
    public static async Task<CursorPage<T>> ToCursorPageAsync<T, TKey, TId>(
        this IQueryable<T> source,
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, TId>> idSelector,
        string? cursor,
        int size,
        CancellationToken ct = default)
        where TKey : IComparable<TKey>
        where TId : IComparable<TId>
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        IOrderedQueryable<T> ordered = source.OrderBy(keySelector).ThenBy(idSelector);

        IQueryable<T> afterCursor = string.IsNullOrEmpty(cursor)
            ? ordered
            : ordered.Where(After(keySelector, idSelector, DecodeCursor<TKey, TId>(cursor)));

        // size+1: лишний элемент - индикатор наличия следующей страницы.
        List<T> fetched = await afterCursor.Take(size + 1).ToListAsync(ct);
        return BuildPage(fetched, keySelector.Compile(), idSelector.Compile(), size);
    }

    // Сборка страницы из size+1 материализованных строк (чистая функция, тестируется отдельно).
    internal static CursorPage<T> BuildPage<T, TKey, TId>(
        IReadOnlyList<T> fetched,
        Func<T, TKey> keySelector,
        Func<T, TId> idSelector,
        int size)
    {
        bool hasMore = fetched.Count > size;
        List<T> items = hasMore ? fetched.Take(size).ToList() : fetched.ToList();

        string? nextCursor = null;
        if (hasMore && items.Count > 0)
        {
            T? last = items[^1];
            nextCursor = EncodeCursor(keySelector(last), idSelector(last));
        }

        return new CursorPage<T>(items, nextCursor, hasMore);
    }

    // Предикат `(SortKey, Id) > (lastKey, lastId)` = SortKey > lastKey OR (SortKey == lastKey AND Id > lastId),
    // выраженный через CompareTo (устойчиво к типам без оператора '>', напр. Guid).
    internal static Expression<Func<T, bool>> After<T, TKey, TId>(
        Expression<Func<T, TKey>> keySelector,
        Expression<Func<T, TId>> idSelector,
        (TKey Key, TId Id) cursor)
        where TKey : IComparable<TKey>
        where TId : IComparable<TId>
    {
        ParameterExpression parameter = keySelector.Parameters[0];
        Expression keyBody = keySelector.Body;
        Expression idBody = new ReplaceParameterVisitor(idSelector.Parameters[0], parameter).Visit(idSelector.Body);

        Expression keyCompare = CompareToZero(keyBody, cursor.Key, typeof(TKey));
        Expression idCompare = CompareToZero(idBody, cursor.Id, typeof(TId));

        // SortKey > lastKey || (SortKey == lastKey && Id > lastId)
        BinaryExpression body = Expression.OrElse(
            Expression.GreaterThan(keyCompare, ZeroConstant),
            Expression.AndAlso(
                Expression.Equal(keyCompare, ZeroConstant),
                Expression.GreaterThan(idCompare, ZeroConstant)));

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private static readonly ConstantExpression ZeroConstant = Expression.Constant(0);

    private static Expression CompareToZero(Expression value, object? other, Type type)
    {
        MethodInfo compareTo = type.GetMethod(nameof(IComparable<int>.CompareTo), [type])!;
        return Expression.Call(value, compareTo, Expression.Constant(other, type));
    }

    internal static string EncodeCursor<TKey, TId>(TKey key, TId id)
        => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new CursorPayload<TKey, TId>(key, id)));

    internal static (TKey Key, TId Id) DecodeCursor<TKey, TId>(string cursor)
    {
        CursorPayload<TKey, TId> payload = JsonSerializer.Deserialize<CursorPayload<TKey, TId>>(Convert.FromBase64String(cursor))
            ?? throw new FormatException("Invalid cursor.");
        return (payload.Key, payload.Id);
    }

    private sealed record CursorPayload<TKey, TId>(TKey Key, TId Id);

    private sealed class ReplaceParameterVisitor(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == from ? to : base.VisitParameter(node);
    }
}
