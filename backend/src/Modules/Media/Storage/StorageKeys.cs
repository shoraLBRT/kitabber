using System.Security.Cryptography;

namespace Platform.Modules.Media.Storage;

/// <summary>
/// Keys of objects in the storage (ADR-0030). An original's key is 128 random bits from a cryptographic generator:
/// it says nothing of the owner, the file's name, the time or the media's identifier, so it cannot be guessed from
/// any of them or from another key.
/// </summary>
public static class StorageKeys
{
    /// <summary>The prefix of originals; derived files (sizes, posters, transcodes) will get prefixes of their own.</summary>
    public const string OriginalsPrefix = "originals/";

    /// <summary>The number of random bytes in a key.</summary>
    public const int RandomBytes = 16;

    /// <summary>A new key for an original: <c>originals/</c> and 32 lower-case hex digits.</summary>
    public static string NewOriginal() =>
        OriginalsPrefix + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(RandomBytes));
}
