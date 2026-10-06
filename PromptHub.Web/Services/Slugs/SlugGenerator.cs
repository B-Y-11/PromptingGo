using System.Text;
using System.Text.RegularExpressions;

namespace PromptHub.Web.Services.Slugs;

public sealed partial class SlugGenerator
    : ISlugGenerator
{
    private static readonly IReadOnlyDictionary<char, string>
        Transliteration =
            new Dictionary<char, string>
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
                ['ц'] = "ts",
                ['ч'] = "ch",
                ['ш'] = "sh",
                ['щ'] = "sch",
                ['ъ'] = "",
                ['ы'] = "y",
                ['ь'] = "",
                ['э'] = "e",
                ['ю'] = "yu",
                ['я'] = "ya"
            };

    public string Generate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var builder = new StringBuilder();

        foreach (var character in
                 value.Trim().ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' ||
                character is >= '0' and <= '9')
            {
                builder.Append(character);
                continue;
            }

            if (Transliteration.TryGetValue(
                    character,
                    out var replacement))
            {
                builder.Append(replacement);
                continue;
            }

            builder.Append('-');
        }

        var result = RepeatedHyphensRegex()
            .Replace(builder.ToString(), "-")
            .Trim('-');

        if (string.IsNullOrWhiteSpace(result))
        {
            return "prompt";
        }

        return result.Length <= 180
            ? result
            : result[..180].TrimEnd('-');
    }

    [GeneratedRegex("-{2,}")]
    private static partial Regex RepeatedHyphensRegex();
}
