namespace Platform.Caching;

/// <summary>
/// A buffer of writes that a module applies to its database later, in batches (ADR-0083): the store behind fast
/// counters. Entries live in Redis, so a restart of every instance of the host loses none of them; memory is the fake
/// for tests and is not shared between processes. Registered by
/// <see cref="CacheServiceCollectionExtensions.AddPlatformCache"/> with the same provider as the cache.
/// </summary>
/// <remarks>
/// A batch is applied at least once: an instance that stops between <see cref="ClaimAsync"/> and
/// <see cref="CompleteAsync"/> leaves the batch to be claimed again when its lease runs out. The module's flush must
/// therefore be idempotent — applying the same batch twice changes nothing the second time.
/// </remarks>
public interface IWriteBuffer
{
    /// <summary>
    /// Appends <paramref name="entry"/> to the buffer <paramref name="buffer"/> unless the mark <paramref name="once"/>
    /// is already set, and sets it for <paramref name="lifetime"/>, in one atomic step; <c>true</c> when appended.
    /// </summary>
    Task<bool> AppendOnceAsync(
        string buffer, string once, TimeSpan lifetime, string entry, CancellationToken cancellationToken = default);

    /// <summary>Appends <paramref name="entry"/> to the buffer <paramref name="buffer"/>.</summary>
    Task AppendAsync(string buffer, string entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes a batch to apply: a batch claimed earlier whose lease of <paramref name="lease"/> has run out (its
    /// claimer stopped), or else every entry appended since the last claim, sealed into a new batch. <c>null</c> when
    /// there is nothing to apply. Entries keep the order they were appended in.
    /// </summary>
    Task<WriteBatch?> ClaimAsync(string buffer, TimeSpan lease, CancellationToken cancellationToken = default);

    /// <summary>Removes an applied batch for good.</summary>
    Task CompleteAsync(WriteBatch batch, CancellationToken cancellationToken = default);
}

/// <summary>A sealed part of a <see cref="IWriteBuffer"/>: its id and its entries in the order they were appended.</summary>
public sealed record WriteBatch(string Buffer, string Id, IReadOnlyList<string> Entries);
