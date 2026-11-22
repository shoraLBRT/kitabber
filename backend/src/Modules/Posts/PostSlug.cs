using System.Globalization;
using System.Text;

namespace Platform.Modules.Posts;

/// <summary>
/// The slug of a post's address <c>&lt;blog address&gt;/&lt;id&gt;-&lt;slug&gt;</c> (FR-POST, ADR-0041): the title transliterated
/// to Latin letters, in lower case, words joined by hyphens. It is not stored: it follows the title, and an address
/// with an old or wrong slug still finds the post by its id and leads to the current one.
/// </summary>
public static class PostSlug
{
    /// <summary>The longest slug; a longer one is cut at the last hyphen that fits.</summary>
    public const int MaxLength = 60;

    // Russian letters as Latin ones, close to the Yandex and DTF addresses; other Cyrillic letters of the neighbouring
    // languages that people often type are added. Other letters with diacritics lose them (é → e).
    private static readonly Dictionary<char, string> _letters = new()
    {
        ['а'] = "a",
        ['б'] = "b",
        ['в'] = "v",
        ['г'] = "g",
        ['д'] = "d",
        ['е'] = "e",
        ['ё'] = "e",
        ['ж'] = "zh",
        ['з'] = "z",
        ['и'] = "i",
        ['й'] = "y",
        ['к'] = "k",
        ['л'] = "l",
        ['м'] = "m",
        ['н'] = "n",
        ['о'] = "o",
        ['п'] = "p",
        ['р'] = "r",
        ['с'] = "s",
        ['т'] = "t",
        ['у'] = "u",
        ['ф'] = "f",
        ['х'] = "h",
        ['ц'] = "c",
        ['ч'] = "ch",
        ['ш'] = "sh",
        ['щ'] = "shch",
        ['ъ'] = "",
        ['ы'] = "y",
        ['ь'] = "",
        ['э'] = "e",
        ['ю'] = "yu",
        ['я'] = "ya",
        ['і'] = "i",
        ['ї'] = "yi",
        ['є'] = "ye",
        ['ґ'] = "g",
        ['ў'] = "u",
    };

    /// <summary>The slug of <paramref name="title"/>; <c>null</c> when there is no title or nothing of it is left.</summary>
    public static string? From(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var slug = new StringBuilder(title.Length);
        var hyphen = false;
        foreach (var letter in Latin(title.ToLowerInvariant()))
        {
            if (letter is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                if (hyphen && slug.Length > 0)
                {
                    slug.Append('-');
                }

                slug.Append(letter);
                hyphen = false;
            }
            else
            {
                hyphen = true;
            }
        }

        return slug.Length == 0 ? null : Cut(slug.ToString());
    }

    // The letters of the text in Latin: Cyrillic through the table (before the diacritics go, so й stays y), the rest
    // without their diacritics.
    private static IEnumerable<char> Latin(string text)
    {
        foreach (var letter in text.Normalize(NormalizationForm.FormC))
        {
            if (_letters.TryGetValue(letter, out var latin))
            {
                foreach (var part in latin)
                {
                    yield return part;
                }

                continue;
            }

            // Emoji and other letters outside the basic plane are no Latin letters: they separate words.
            if (char.IsSurrogate(letter))
            {
                yield return ' ';
                continue;
            }

            foreach (var part in letter.ToString().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(part) != UnicodeCategory.NonSpacingMark)
                {
                    yield return part;
                }
            }
        }
    }

    private static string Cut(string slug)
    {
        if (slug.Length <= MaxLength)
        {
            return slug;
        }

        var cut = slug.LastIndexOf('-', MaxLength);
        return cut > 0 ? slug[..cut] : slug[..MaxLength];
    }
}
