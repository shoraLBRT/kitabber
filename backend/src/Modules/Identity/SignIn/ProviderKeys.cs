using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Platform.Configuration;

namespace Platform.Modules.Identity.SignIn;

/// <summary>
/// The keys of the OpenID Connect provider (ADR-0018): an RSA key signs tokens, an AES-256 key encrypts authorization
/// codes. On the stands they come from secrets (<see cref="IdentitySettings.SigningKeyKey"/>,
/// <see cref="IdentitySettings.EncryptionKeyKey"/>); in Development and Test a missing key is generated at start, so the
/// repository holds no key at all. A generated key lives only as long as the process.
/// </summary>
public static class ProviderKeys
{
    /// <summary>The signing key from configuration; null when it is not set and the environment may generate one.</summary>
    public static RsaSecurityKey? SigningKey(IConfiguration configuration, string environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var value = configuration[IdentitySettings.SigningKeyKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            return MissingKey(IdentitySettings.SigningKeyKey, environment) is { } error ? throw error : null;
        }

        var rsa = RSA.Create();
        try
        {
            if (value.TrimStart().StartsWith("-----", StringComparison.Ordinal))
            {
                rsa.ImportFromPem(value);
            }
            else
            {
                ImportDer(rsa, Convert.FromBase64String(value.Trim()));
            }
        }
        catch (Exception e) when (e is CryptographicException or FormatException or ArgumentException)
        {
            rsa.Dispose();
            throw new InvalidConfigurationException(
                [$"{IdentitySettings.SigningKeyKey}: not an RSA private key in PEM or base64 DER (PKCS#8 or PKCS#1)"]);
        }

        if (rsa.KeySize < 2048)
        {
            rsa.Dispose();
            throw new InvalidConfigurationException([$"{IdentitySettings.SigningKeyKey}: the RSA key must have at least 2048 bits"]);
        }

        var key = new RsaSecurityKey(rsa);
        // A stable key id derived from the public key: clients pick the key from the JWKS by it.
        key.KeyId = Base64UrlEncoder.Encode(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()))[..16];
        return key;
    }

    /// <summary>The encryption key from configuration; null when it is not set and the environment may generate one.</summary>
    public static SymmetricSecurityKey? EncryptionKey(IConfiguration configuration, string environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var value = configuration[IdentitySettings.EncryptionKeyKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            return MissingKey(IdentitySettings.EncryptionKeyKey, environment) is { } error ? throw error : null;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(value.Trim());
        }
        catch (FormatException)
        {
            throw new InvalidConfigurationException([$"{IdentitySettings.EncryptionKeyKey}: not base64"]);
        }

        return bytes.Length == 32
            ? new SymmetricSecurityKey(bytes)
            : throw new InvalidConfigurationException(
                [$"{IdentitySettings.EncryptionKeyKey}: expected 32 bytes (AES-256), got {bytes.Length}"]);
    }

    /// <summary>A DER private key: PKCS#8, or PKCS#1 as some versions of openssl write it.</summary>
    private static void ImportDer(RSA rsa, byte[] der)
    {
        try
        {
            rsa.ImportPkcs8PrivateKey(der, out _);
        }
        catch (CryptographicException)
        {
            rsa.ImportRSAPrivateKey(der, out _);
        }
    }

    /// <summary>Whether <paramref name="environment"/> may run on keys generated at start.</summary>
    public static bool MayGenerateKeys(string environment) => PlatformEnvironments.AllowsSecretsInSettingsFiles(environment);

    private static InvalidConfigurationException? MissingKey(string key, string environment) =>
        MayGenerateKeys(environment)
            ? null
            : new InvalidConfigurationException(
                [$"{key} is required in {environment} (environment variable {key.Replace(":", "__", StringComparison.Ordinal)}): " +
                 "a generated key would sign every instance's tokens differently and change at each restart"]);
}
