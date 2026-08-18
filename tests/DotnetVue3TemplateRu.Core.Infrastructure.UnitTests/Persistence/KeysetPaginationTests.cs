using DotnetVue3TemplateRu.Core.Infrastructure.Persistence;

namespace DotnetVue3TemplateRu.Core.Infrastructure.UnitTests.Persistence;

/// <summary>
/// Юнит-тесты строительных блоков keyset-пагинации на in-memory наборе (List.AsQueryable): кодек
/// курсора, предикат `(SortKey, Id) > cursor` и сборка страницы. Полный обход по курсорам должен
/// вернуть все элементы ровно один раз в порядке (SortKey, Id) - без пропусков и дублей на границах.
/// </summary>
public class KeysetPaginationTests
{
    private sealed record Item(long Sort, Guid Id);

    [Test]
    public async Task Cursor_roundtrips()
    {
        var id = Guid.NewGuid();
        string cursor = KeysetPagination.EncodeCursor(42L, id);

        (long key, Guid decodedId) = KeysetPagination.DecodeCursor<long, Guid>(cursor);

        await Assert.That(key).IsEqualTo(42L);
        await Assert.That(decodedId).IsEqualTo(id);
    }

    [Test]
    public async Task BuildPage_reports_more_when_extra_row_fetched()
    {
        var rows = new List<Item> { new(1, Guid.NewGuid()), new(2, Guid.NewGuid()), new(3, Guid.NewGuid()) };

        CursorPage<Item> page = KeysetPagination.BuildPage(rows, i => i.Sort, i => i.Id, size: 2);

        await Assert.That(page.HasMore).IsTrue();
        await Assert.That(page.Items.Count).IsEqualTo(2);
        await Assert.That(page.NextCursor).IsNotNull();
    }

    [Test]
    public async Task BuildPage_reports_no_more_on_last_page()
    {
        var rows = new List<Item> { new(1, Guid.NewGuid()), new(2, Guid.NewGuid()) };

        CursorPage<Item> page = KeysetPagination.BuildPage(rows, i => i.Sort, i => i.Id, size: 2);

        await Assert.That(page.HasMore).IsFalse();
        await Assert.That(page.Items.Count).IsEqualTo(2);
        await Assert.That(page.NextCursor).IsNull();
    }

    [Test]
    public async Task Full_walk_covers_all_items_in_order_without_gaps_or_duplicates()
    {
        // Набор с повторами ключа сортировки (тайбрейк по Id обязателен для стабильности).
        var items = new List<Item>();
        for (int sort = 0; sort < 5; sort++)
        {
            for (int dup = 0; dup < 3; dup++)
            {
                items.Add(new Item(sort, Guid.NewGuid()));
            }
        }

        var expected = items.OrderBy(i => i.Sort).ThenBy(i => i.Id).ToList();

        var walked = new List<Item>();
        string? cursor = null;
        do
        {
            CursorPage<Item> page = Page(items, cursor, size: 4);
            walked.AddRange(page.Items);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        await Assert.That(walked.Count).IsEqualTo(expected.Count);
        await Assert.That(walked.Select(i => i.Id).Distinct().Count()).IsEqualTo(expected.Count);
        await Assert.That(walked.SequenceEqual(expected)).IsTrue();
    }

    // Синхронный аналог ToCursorPageAsync (EF-версия использует ToListAsync): те же строительные блоки.
    private static CursorPage<Item> Page(IReadOnlyList<Item> source, string? cursor, int size)
    {
        IOrderedQueryable<Item> ordered = source.AsQueryable().OrderBy(i => i.Sort).ThenBy(i => i.Id);

        IQueryable<Item> afterCursor = string.IsNullOrEmpty(cursor)
            ? ordered
            : ordered.Where(KeysetPagination.After<Item, long, Guid>(
                i => i.Sort, i => i.Id, KeysetPagination.DecodeCursor<long, Guid>(cursor)));

        var fetched = afterCursor.Take(size + 1).ToList();
        return KeysetPagination.BuildPage(fetched, i => i.Sort, i => i.Id, size);
    }
}
