namespace Platform.Http.Pagination;

/// <summary>
/// A decoded page request for keyset (cursor) pagination. The list endpoint filters its query to the items
/// strictly after <see cref="After"/> in its sort order, takes <see cref="FetchCount"/> items (one more than the
/// limit, to learn whether a next page exists) and passes them to <see cref="ToPage{T}"/>:
/// <code>
/// var page = query.ToRequest&lt;PostCursor&gt;();
/// var rows = await posts
///     .Where(p =&gt; page.After == null || p.CreatedAt &lt; page.After.CreatedAt
///         || (p.CreatedAt == page.After.CreatedAt &amp;&amp; p.Id &lt; page.After.Id))
///     .OrderByDescending(p =&gt; p.CreatedAt).ThenByDescending(p =&gt; p.Id)
///     .Take(page.FetchCount).ToListAsync(ct);
/// return page.ToPage(rows, p =&gt; new PostCursor(p.CreatedAt, p.Id));
/// </code>
/// The sort must be total (end with a unique key such as the id), or items on a page boundary are lost.
/// </summary>
/// <typeparam name="TCursor">The keyset values of an item: a small record such as <c>(CreatedAt, Id)</c>.</typeparam>
public sealed class PageRequest<TCursor>
    where TCursor : notnull
{
    internal PageRequest(TCursor? after, int limit)
    {
        After = after;
        Limit = limit;
    }

    /// <summary>The position of the last item of the previous page; null for the first page.</summary>
    public TCursor? After { get; }

    public int Limit { get; }

    /// <summary>How many items to read: the limit plus one.</summary>
    public int FetchCount => Limit + 1;

    /// <summary>Builds the page from the items read with <see cref="FetchCount"/>, in the list's sort order.</summary>
    public CursorPage<T> ToPage<T>(IReadOnlyList<T> fetched, Func<T, TCursor> cursorOf)
    {
        ArgumentNullException.ThrowIfNull(fetched);
        ArgumentNullException.ThrowIfNull(cursorOf);

        if (fetched.Count <= Limit)
        {
            return new CursorPage<T>(fetched, null);
        }

        var items = fetched.Take(Limit).ToArray();
        return new CursorPage<T>(items, CursorCodec.Encode(cursorOf(items[^1])));
    }
}
