using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Weather.Core;

namespace Weather.Mobile;

/// <summary>
/// Runs Weather.Core on the phone. Saving setup builds new services, which applies new keys,
/// stations and location together and drops everything cached under the old ones.
/// </summary>
public sealed class AppHost(SettingsStore store)
{
    private IServiceProvider? _services;

    public IReadOnlyList<WeatherStationOptions> DefaultStations { get; private set; } = [];

    public T Get<T>() where T : notnull => _services!.GetRequiredService<T>();

    public async Task LoadAsync()
    {
        var defaults = ReadDefaults();
        DefaultStations = defaults.GetSection(nameof(WeatherOptions.Stations)).Get<List<WeatherStationOptions>>() ?? [];

        var settings = store.Load();
        var weatherUnderground = await store.GetKeyAsync(SettingsStore.WeatherUndergroundKey);
        var ambientApiKey = await store.GetKeyAsync(SettingsStore.AmbientApiKey);
        var ambientApplicationKey = await store.GetKeyAsync(SettingsStore.AmbientApplicationKey);
        var hasAmbient = !string.IsNullOrWhiteSpace(ambientApiKey) && !string.IsNullOrWhiteSpace(ambientApplicationKey);

        var overrides = new Dictionary<string, string?>
        {
            [nameof(WeatherOptions.HomeStationSource)] = (hasAmbient ? HomeStationSource.AmbientWeather : HomeStationSource.OpenMeteo).ToString(),
            ["WeatherUnderground:ApiKey"] = weatherUnderground,
            ["AmbientWeather:ApiKey"] = ambientApiKey,
            ["AmbientWeather:ApplicationKey"] = ambientApplicationKey,
        };

        if (settings is not null)
        {
            var location = settings.Location;
            overrides["DefaultLocation:Zip"] = location.Zip;
            overrides["DefaultLocation:Place"] = location.Place;
            overrides["DefaultLocation:State"] = location.State;
            overrides["DefaultLocation:Latitude"] = location.Latitude.ToString(CultureInfo.InvariantCulture);
            overrides["DefaultLocation:Longitude"] = location.Longitude.ToString(CultureInfo.InvariantCulture);
            overrides["DefaultLocation:TimeZoneId"] = location.TimeZoneId;
        }

        AddStations(overrides, settings, hasWeatherUnderground: !string.IsNullOrWhiteSpace(weatherUnderground));

        var configuration = new ConfigurationBuilder()
            .AddConfiguration(defaults)
            .AddInMemoryCollection(overrides)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWeatherCore(configuration);

        // Requests still running on the old services finish there, so these aren't disposed.
        _services = services.BuildServiceProvider();
    }

    // Overrides can only replace entries in the file's list, and AddWeatherCore skips blank IDs.
    private void AddStations(Dictionary<string, string?> overrides, AppSettings? settings, bool hasWeatherUnderground)
    {
        var selected = hasWeatherUnderground ? settings?.StationIds ?? [.. DefaultStations.Select(station => station.Id)] : [];

        for (var i = 0; i < DefaultStations.Count; i++)
        {
            if (!selected.Contains(DefaultStations[i].Id, StringComparer.OrdinalIgnoreCase))
            {
                overrides[$"Stations:{i}:Id"] = "";
            }
        }

        var added = hasWeatherUnderground ? settings?.AddedStationIds ?? [] : [];
        for (var i = 0; i < added.Count; i++)
        {
            overrides[$"Stations:{DefaultStations.Count + i}:Id"] = added[i];
        }
    }

    private static IConfiguration ReadDefaults()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("appsettings.json")!;
        var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        bytes.Position = 0;

        return new ConfigurationBuilder().AddJsonStream(bytes).Build().GetSection(WeatherOptions.SectionName);
    }
}
