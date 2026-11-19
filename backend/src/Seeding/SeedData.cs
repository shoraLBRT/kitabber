using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Platform.Seeding;

/// <summary>
/// The source of every generated value (ADR-0009): identifiers, random choices and time. Everything is derived from
/// one integer seed and a <see cref="TimeProvider"/>, so the same seed and clock give the same data on every run and
/// on every machine. Modules never read each other's tables: a posts factory takes its author with
/// <c>Id("account", 3)</c>, the same identifier the accounts factory gives to its fourth account.
/// </summary>
public sealed class SeedData
{
    /// <summary>The seed of the stand and of the tests unless they choose another one.</summary>
    public const int DefaultSeed = 20260115;

    public SeedData(int seed, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        Seed = seed;
        Time = time;
    }

    public int Seed { get; }

    /// <summary>The clock; generated timestamps are relative to its current time.</summary>
    public TimeProvider Time { get; }

    /// <summary>The current time of <see cref="Time"/>.</summary>
    public DateTimeOffset Now => Time.GetUtcNow();

    /// <summary>
    /// The identifier of the <paramref name="number"/>-th object of a <paramref name="kind"/> (for example
    /// <c>account</c>, <c>post</c>). Stable for a seed; different kinds and seeds never share identifiers.
    /// </summary>
    public Guid Id(string kind, int number)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes($"id:{Seed}:{kind}:{number}"), hash);
        var bytes = hash[..16];
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x80); // RFC 9562 version 8 (custom), as read by Guid's layout
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 9562 variant
        return new Guid(bytes);
    }

    /// <summary>
    /// A random sequence of its own for a <paramref name="scope"/>, such as a factory or a seeder. Sequences of
    /// different scopes do not affect each other: a new module does not change the data of the existing ones.
    /// </summary>
    public Random Random(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes($"random:{Seed}:{scope}"), hash);
#pragma warning disable CA5394 // Demo data needs a repeatable sequence, not a secure one.
        return new Random(BinaryPrimitives.ReadInt32LittleEndian(hash));
#pragma warning restore CA5394
    }
}
