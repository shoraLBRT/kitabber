namespace Platform.Http.Pagination;

/// <summary>
/// One page of a list. <see cref="NextCursor"/> is null on the last page; otherwise the client passes it back as
/// <c>?cursor=</c> to get the next page.
/// </summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
