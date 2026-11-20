using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Platform.Configuration;
using Platform.Modules.Identity.SignIn;

namespace Platform.Modules.Identity.IntegrationTests.SignIn;

/// <summary>The provider's keys and clients from settings (ADR-0018, docs/CONFIG.md). No database.</summary>
public sealed class ProviderConfigurationTests
{
    [Theory]
    [InlineData(PlatformEnvironments.Staging)]
    [InlineData(PlatformEnvironments.Production)]
    public void StandsDoNotStartWithoutKeys(string environment)
    {
        var configuration = Settings([]);

        var signing = Assert.Throws<InvalidConfigurationException>(() => ProviderKeys.SigningKey(configuration, environment));
        var encryption = Assert.Throws<InvalidConfigurationException>(() => ProviderKeys.EncryptionKey(configuration, environment));

        Assert.Contains("Identity__SigningKey", Assert.Single(signing.Problems), StringComparison.Ordinal);
        Assert.Contains("Identity__EncryptionKey", Assert.Single(encryption.Problems), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PlatformEnvironments.Development)]
    [InlineData(PlatformEnvironments.Test)]
    public void DevelopmentAndTestGenerateMissingKeys(string environment)
    {
        var configuration = Settings([]);

        Assert.Null(ProviderKeys.SigningKey(configuration, environment));
        Assert.Null(ProviderKeys.EncryptionKey(configuration, environment));
    }

    [Fact]
    public void KeysAreReadFromPemOrBase64()
    {
        using var rsa = RSA.Create(2048);
        var pem = ProviderKeys.SigningKey(Settings(new() { [IdentitySettings.SigningKeyKey] = rsa.ExportPkcs8PrivateKeyPem() }), PlatformEnvironments.Staging);
        var der = ProviderKeys.SigningKey(
            Settings(new() { [IdentitySettings.SigningKeyKey] = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()) }), PlatformEnvironments.Staging);
        var aes = ProviderKeys.EncryptionKey(
            Settings(new() { [IdentitySettings.EncryptionKeyKey] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }), PlatformEnvironments.Staging);

        Assert.NotNull(pem);
        Assert.Equal(pem.KeyId, der!.KeyId);
        Assert.Equal(32, aes!.Key.Length);
    }

    [Fact]
    public void WeakOrMalformedKeysAreRefusedWithoutShowingThem()
    {
        using var weak = RSA.Create(1024);
        var cases = new (string Key, string Value)[]
        {
            (IdentitySettings.SigningKeyKey, "not a key"),
            (IdentitySettings.SigningKeyKey, weak.ExportPkcs8PrivateKeyPem()),
            (IdentitySettings.EncryptionKeyKey, Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))),
            (IdentitySettings.EncryptionKeyKey, "%%%"),
        };

        foreach (var (key, value) in cases)
        {
            var configuration = Settings(new() { [key] = value });
            var error = Assert.Throws<InvalidConfigurationException>(() =>
            {
                _ = ProviderKeys.SigningKey(configuration, PlatformEnvironments.Test);
                _ = ProviderKeys.EncryptionKey(configuration, PlatformEnvironments.Test);
            });
            Assert.DoesNotContain(value, error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ClientsArePublicWithPkceAndExactRedirects()
    {
        var clients = OidcClients.FromConfiguration(Settings(new()
        {
            [IdentitySettings.WebRedirectUrisKey] = "https://example.org/auth/callback, http://localhost:3000/auth/callback",
        }));

        var web = Assert.Single(clients);
        Assert.Equal(OidcClients.WebClientId, web.ClientId);
        Assert.Equal("public", web.ClientType);
        Assert.Null(web.ClientSecret);
        Assert.Contains("ft:pkce", web.Requirements);
        Assert.Equal(["https://example.org/auth/callback", "http://localhost:3000/auth/callback"], web.RedirectUris.Select(u => u.ToString()));
    }

    [Theory]
    [InlineData("/auth/callback")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://example.org/auth/callback#fragment")]
    public void BadRedirectAddressStopsTheHost(string uri)
    {
        var error = Assert.Throws<InvalidConfigurationException>(() =>
            OidcClients.FromConfiguration(Settings(new() { [IdentitySettings.AdminRedirectUrisKey] = uri })));

        Assert.Contains(IdentitySettings.AdminRedirectUrisKey, Assert.Single(error.Problems), StringComparison.Ordinal);
    }

    private static IConfiguration Settings(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
