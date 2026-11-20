using Platform.Modules.Identity.Email;

namespace Platform.Modules.Identity.IntegrationTests.Email;

/// <summary>The normalised email (ADR-0020): the key accounts are unique by and a future merge matches by. No database.</summary>
public sealed class EmailAddressesTests
{
    [Theory]
    [InlineData("User@Example.COM", "user@example.com")]
    [InlineData("  anna@example.com  ", "anna@example.com")]
    [InlineData("anna@example.com.", "anna@example.com")]
    [InlineData("J.Doe@GMail.com", "jdoe@gmail.com")]
    [InlineData("j.d.o.e@googlemail.com", "jdoe@gmail.com")]
    [InlineData("jdoe+news@gmail.com", "jdoe+news@gmail.com")]
    [InlineData("j.doe@yandex.ru", "j.doe@yandex.ru")]
    [InlineData("j.doe+news@example.com", "j.doe+news@example.com")]
    [InlineData("Иван@Почта.РФ", "иван@xn--80a1acny.xn--p1ai")]
    [InlineData("ivan@xn--80a1acny.xn--p1ai", "ivan@xn--80a1acny.xn--p1ai")]
    [InlineData("é@example.com", "é@example.com")]
    public void NormalisesTheAddress(string email, string normalized) =>
        Assert.Equal(normalized, EmailAddresses.Normalize(email));

    [Theory]
    [InlineData("Anna.Smith@Example.com", "anna.smith@EXAMPLE.com")]
    [InlineData("J.Doe@gmail.com", "jdoe@GoogleMail.com")]
    [InlineData("Иван@почта.рф", "иван@xn--80a1acny.xn--p1ai")]
    public void SpellingsOfOneAddressShareTheKey(string first, string second) =>
        Assert.Equal(EmailAddresses.Normalize(first), EmailAddresses.Normalize(second));

    [Theory]
    [InlineData("jdoe+a@gmail.com", "jdoe+b@gmail.com")]
    [InlineData("j.doe@example.com", "jdoe@example.com")]
    public void DifferentAddressesKeepDifferentKeys(string first, string second) =>
        Assert.NotEqual(EmailAddresses.Normalize(first), EmailAddresses.Normalize(second));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("anna")]
    [InlineData("anna@")]
    [InlineData("@example.com")]
    [InlineData("anna@localhost")]
    [InlineData("anna@@example.com")]
    [InlineData("an@na@example.com")]
    [InlineData(".anna@example.com")]
    [InlineData("anna.@example.com")]
    [InlineData("an..na@example.com")]
    [InlineData("an na@example.com")]
    [InlineData("\"anna\"@example.com")]
    [InlineData("anna@exa mple.com")]
    [InlineData("anna@exa_mple.com")]
    [InlineData("anna@example..com")]
    [InlineData("...@gmail.com")]
    [InlineData("an\u0000na@example.com")]
    public void RefusesWhatIsNotAnAddress(string? email) => Assert.Null(EmailAddresses.Normalize(email));

    [Fact]
    public void RefusesTooLongAddresses()
    {
        Assert.Null(EmailAddresses.Normalize(new string('a', 65) + "@example.com"));
        Assert.Null(EmailAddresses.Normalize("anna@" + string.Join('.', Enumerable.Repeat(new string('d', 60), 5)) + ".com"));
        Assert.NotNull(EmailAddresses.Normalize(new string('a', 64) + "@example.com"));
    }
}
