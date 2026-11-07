using System.Globalization;
using System.Text.RegularExpressions;

namespace Weather.Core.Snow;

/// <summary>
/// Reads a snow range from forecast text, such as NWS's "New snow accumulation of 1 to 3 inches
/// possible" or Weather Underground's "Snow accumulations around one inch".
/// </summary>
public static partial class SnowText
{
    /// <summary>NWS says "little or no" when it expects under half an inch.</summary>
    public const double LittleOrNoInches = 0.5;

    public static (double Low, double High)? Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        foreach (var sentence in text.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var lower = sentence.ToLowerInvariant();
            if (!lower.Contains("snow") || !(lower.Contains("accumulat") || lower.Contains("snowfall")))
            {
                continue;
            }

            if (LittleOrNo().IsMatch(lower))
            {
                return (0, LittleOrNoInches);
            }

            if (Range().Match(lower) is { Success: true } range
                && Number(range.Groups["low"].Value) is double low
                && Number(range.Groups["high"].Value) is double high)
            {
                return (low, high);
            }

            if (Around().Match(lower) is { Success: true } around && Number(around.Groups["value"].Value) is double value)
            {
                return (value, value);
            }

            if (UpTo().Match(lower) is { Success: true } upTo && Number(upTo.Groups["value"].Value) is double most)
            {
                return (0, most);
            }
        }

        return null;
    }

    private static double? Number(string text) => text.Trim() switch
    {
        "a" or "an" or "one" => 1,
        "half an" or "a half" or "one half" or "half" => 0.5,
        "two" => 2,
        "three" => 3,
        "four" => 4,
        "five" => 5,
        "six" => 6,
        "seven" => 7,
        "eight" => 8,
        "nine" => 9,
        "ten" => 10,
        "eleven" => 11,
        "twelve" => 12,
        var digits when double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => number,
        _ => null,
    };

    private const string Amount = @"\d+(?:\.\d+)?|half an|a half|one half|an|a|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve";

    [GeneratedRegex(@"\blittle or no (?:new )?snow")]
    private static partial Regex LittleOrNo();

    [GeneratedRegex(@"\b(?<low>" + Amount + @")\s*(?:to|-)\s*(?<high>" + Amount + @")\s*inch")]
    private static partial Regex Range();

    [GeneratedRegex(@"\b(?:around|near|about)\s+(?<value>" + Amount + @")\s*inch")]
    private static partial Regex Around();

    [GeneratedRegex(@"\b(?:less than|under|up to)\s+(?<value>" + Amount + @")\s*(?:of an\s+)?inch")]
    private static partial Regex UpTo();
}
