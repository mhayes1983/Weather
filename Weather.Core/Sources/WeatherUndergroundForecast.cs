namespace Weather.Core.Sources;

/// <summary>
/// The Weather Underground daily forecast response, shared by the temperature and snow columns
/// so both pages together use one call of the shared quota per ZIP.
/// </summary>
internal static class WeatherUndergroundForecast
{
    public static Task<string> GetJsonAsync(
        IHttpClientFactory httpClientFactory,
        WeatherCache cache,
        KeyedSourceOptions settings,
        string zip,
        CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(
            $"wu-daily:{zip}",
            async () =>
            {
                var url = $"https://api.weather.com/v3/wx/forecast/daily/{settings.ForecastDays}day?postalKey={zip}:US&units=e&language=en-US&format=json&apiKey={Uri.EscapeDataString(settings.ApiKey!)}";
                var client = httpClientFactory.CreateClient(WeatherHttp.ClientName);
                return (await client.GetStringAsync(url, cancellationToken), TimeSpan.FromMinutes(settings.CacheMinutes));
            });
}
