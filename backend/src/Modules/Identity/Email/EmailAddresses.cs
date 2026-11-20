using System.Globalization;
using System.Text;

namespace Platform.Modules.Identity.Email;

/// <summary>
/// The rules of the normalised email (FR-ID, ADR-0020): the key an account is unique by, and the key a future merge of
/// accounts matches confirmed emails by. The account keeps the address as typed as well; mail goes there.
/// </summary>
public static class EmailAddresses
{
    /// <summary>The longest address SMTP carries (RFC 5321: a path of 256 octets minus the angle brackets).</summary>
    public const int MaxLength = 254;

    private const int MaxLocalLength = 64;

    // Gmail ignores dots in the mailbox name and serves both domains; no other provider's aliases are folded.
    private static readonly string[] _gmailDomains = ["gmail.com", "googlemail.com"];

    private static readonly IdnMapping _idn = new() { UseStd3AsciiRules = true };

    /// <summary>
    /// The normalised form of <paramref name="email"/>, or <c>null</c> when it is not an address the platform accepts.
    /// The mailbox name is put into Unicode NFC and lower case; the domain into lower-case ASCII (IDN to punycode, the
    /// trailing dot removed). For Gmail the dots of the mailbox name are removed and <c>googlemail.com</c> becomes
    /// <c>gmail.com</c>. A <c>+tag</c> is kept for every provider: it is a different address to the platform.
    /// </summary>
    public static string? Normalize(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var value = email.Trim();
        var at = value.IndexOf('@', StringComparison.Ordinal);
        if (value.Length > MaxLength || at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1)
        {
            return null;
        }

        var local = LocalPart(value[..at]);
        var domain = Domain(value[(at + 1)..]);
        if (local is null || domain is null)
        {
            return null;
        }

        if (_gmailDomains.Contains(domain, StringComparer.Ordinal))
        {
            local = local.Replace(".", string.Empty, StringComparison.Ordinal);
            domain = _gmailDomains[0];
            if (local.Length == 0)
            {
                return null;
            }
        }

        var normalized = $"{local}@{domain}";
        return normalized.Length <= MaxLength ? normalized : null;
    }

    // A dot-atom mailbox name (RFC 5322) with Unicode letters allowed (RFC 6531); quoted names are not accepted.
    private static string? LocalPart(string local)
    {
        if (local.Length > MaxLocalLength)
        {
            return null;
        }

        try
        {
            local = local.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            // Not valid UTF-16: a lone surrogate.
            return null;
        }

        if (local.StartsWith('.') || local.EndsWith('.') || local.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        foreach (var c in local)
        {
            if (c < 0x80 && !char.IsAsciiLetterOrDigit(c) && !"!#$%&'*+-/=?^_`{|}~.".Contains(c, StringComparison.Ordinal))
            {
                return null;
            }

            if (char.IsControl(c) || char.IsWhiteSpace(c))
            {
                return null;
            }
        }

        return local.ToLowerInvariant();
    }

    private static string? Domain(string domain)
    {
        domain = domain.TrimEnd('.');
        if (domain.Length == 0 || !domain.Contains('.', StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return _idn.GetAscii(domain).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
