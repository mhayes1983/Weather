using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Weather.Core.Models;
using Weather.Core.Sources;

namespace Weather.Core.DailyForecast;

/// <summary>The Area Forecast Discussion from the NWS office that forecasts for a location.</summary>
public sealed partial class NwsDiscussion(IHttpClientFactory httpClientFactory, WeatherCache cache)
{
    private const string GridMarker = "/gridpoints/";
    private static readonly TimeSpan GridCacheDuration = TimeSpan.FromDays(30);

    // ".SYNOPSIS...", ".NEAR TERM /THROUGH TONIGHT/...", sometimes with text after the dots.
    [GeneratedRegex(@"^\.(?<title>[A-Z0-9][^.]*?)\s*\.\.\.(?<rest>.*)$")]
    private static partial Regex SectionStart();

    /// <exception cref="InvalidOperationException">The office has no discussion to show.</exception>
    public async Task<ForecastDiscussion> GetAsync(GeoPoint location, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);

        // The same cached lookup as the day descriptions, so this adds no /points call.
        var forecastUrl = await cache.GetOrCreateAsync(
            string.Create(CultureInfo.InvariantCulture, $"nws-grid:{location.Latitude:0.####},{location.Longitude:0.####}"),
            async () => (await NwsSource.GetForecastUrlAsync(client, location.Latitude, location.Longitude, cancellationToken), GridCacheDuration));

        var office = Office(forecastUrl) ?? throw new InvalidOperationException("The NWS office couldn't be read.");

        using var list = JsonDocument.Parse(await client.GetStringAsync(
            $"https://api.weather.gov/products/types/AFD/locations/{office}", cancellationToken));

        var newest = list.RootElement.GetProperty("@graph").EnumerateArray()
            .Select(product => (
                Id: product.GetProperty("id").GetString()!,
                IssuedAt: DateTimeOffset.Parse(product.GetProperty("issuanceTime").GetString()!, CultureInfo.InvariantCulture)))
            .OrderByDescending(product => product.IssuedAt)
            .FirstOrDefault();
        if (newest.Id is null)
        {
            throw new InvalidOperationException("The NWS office has no discussion.");
        }

        using var product = JsonDocument.Parse(await client.GetStringAsync(
            $"https://api.weather.gov/products/{Uri.EscapeDataString(newest.Id)}", cancellationToken));

        var sections = Parse(product.RootElement.GetProperty("productText").GetString() ?? "");
        return sections.Count == 0
            ? throw new InvalidOperationException("The NWS discussion couldn't be read.")
            : new ForecastDiscussion(office, newest.IssuedAt, sections);
    }

    // "https://api.weather.gov/gridpoints/GYX/65,60/forecast" is GYX.
    public static string? Office(string forecastUrl)
    {
        var start = forecastUrl.IndexOf(GridMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        var office = forecastUrl[(start + GridMarker.Length)..].Split('/')[0];
        return office.Length > 0 && office.All(char.IsAsciiLetterOrDigit) ? office.ToUpperInvariant() : null;
    }

    /// <summary>
    /// Splits the product into its titled sections, joining the hard-wrapped lines of each paragraph.
    /// The header, the forecasters' names after "$$" and the watches and warnings list are left out:
    /// alerts have their own check, and a list from an older discussion could be wrong.
    /// </summary>
    internal static IReadOnlyList<DiscussionSection> Parse(string text)
    {
        var sections = new List<DiscussionSection>();
        string? title = null;
        var paragraphs = new List<string>();
        var lines = new List<string>();

        void EndParagraph()
        {
            if (lines.Count > 0)
            {
                paragraphs.Add(string.Join(' ', lines));
                lines.Clear();
            }
        }

        void EndSection()
        {
            EndParagraph();
            if (title is not null && paragraphs.Count > 0 && !title.Contains("WATCHES/WARNINGS", StringComparison.Ordinal))
            {
                sections.Add(new DiscussionSection(title, [.. paragraphs]));
            }

            title = null;
            paragraphs.Clear();
        }

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line == "$$")
            {
                break;
            }

            if (line == "&&")
            {
                EndSection();
            }
            else if (SectionStart().Match(line) is { Success: true } match)
            {
                EndSection();
                title = match.Groups["title"].Value;
                if (match.Groups["rest"].Value.Trim() is { Length: > 0 } rest)
                {
                    lines.Add(rest);
                }
            }
            else if (line.Length == 0)
            {
                EndParagraph();
            }
            else if (title is not null)
            {
                lines.Add(line);
            }
        }

        EndSection();
        return sections;
    }
}
