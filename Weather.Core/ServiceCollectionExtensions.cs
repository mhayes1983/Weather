using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Weather.Core.Alerts;
using Weather.Core.Almanac;
using Weather.Core.Api;
using Weather.Core.Climate;
using Weather.Core.DailyForecast;
using Weather.Core.Locations;
using Weather.Core.Models;
using Weather.Core.Snow;
using Weather.Core.Sources;
using Weather.Core.Station;

namespace Weather.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWeatherCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WeatherOptions>(configuration);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<WeatherCache>();
        services.AddSingleton<LocationResolver>();
        services.AddSingleton<ClimateStations>();
        services.AddSingleton<IClimateNormals, AcisClimateNormals>();
        services.AddSingleton<ForecastTableService>();
        AddHomeStation(services, configuration.Get<WeatherOptions>() ?? new WeatherOptions());
        services.AddSingleton<StationDirectory>();
        services.AddSingleton<StationLookup>();
        services.AddSingleton<StationService>();
        services.AddSingleton<StationLocator>();
        services.AddSingleton<StationSky>();
        services.AddSingleton<NwsAlerts>();
        services.AddSingleton<AlertService>();
        services.AddSingleton<OpenMeteoOutlook>();
        services.AddSingleton<NwsDescriptions>();
        services.AddSingleton<UsnoMoon>();
        services.AddSingleton<DailyForecastService>();
        services.AddSingleton<AcisAlmanac>();
        services.AddSingleton<AlmanacService>();
        services.AddSingleton<KeyCheck>();
        services.AddSingleton<SnowTableService>();
        services.AddSingleton<SnowAlertService>();
        services.AddSingleton<LocalApi>();

        // The home station comes first in the dropdown, then the configured stations in order.
        var stations = (configuration.Get<WeatherOptions>()?.Stations ?? [])
            .Where(station => !string.IsNullOrWhiteSpace(station.Id)
                && !string.Equals(station.Id, AmbientWeatherStation.HomeId, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(station => station.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var station in stations)
        {
            services.AddSingleton<IWeatherStation>(provider =>
                ActivatorUtilities.CreateInstance<WeatherUndergroundStation>(provider, station));
        }

        // Logging is off for this client because Weather Underground and Ambient Weather keys travel in the URL.
        services.AddHttpClient(WeatherHttp.ClientName, (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<WeatherOptions>>().Value;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        }).RemoveAllLoggers();

        // Registration order breaks ties in the table's column order. The forecast endpoint's models
        // share one Open-Meteo call per location on each page.
        var forecastModels = OpenMeteoModels.Select(entry => entry.Model).ToArray();
        foreach (var (column, model) in OpenMeteoModels)
        {
            AddOpenMeteo(services, column, model, forecastModels);
        }

        AddOpenMeteo(services, WeatherNext, WeatherNextModel, [WeatherNextModel], OpenMeteoSource.EnsembleEndpoint);
        AddNewsCenterMaine(services, new ColumnDescriptor("ncm-coastal", "NCM Coastal"), "Portland 10-Day");
        AddNewsCenterMaine(services, new ColumnDescriptor("ncm-inland", "NCM Inland"), "Bangor 10-Day");
        services.AddSingleton<IForecastSource, WeatherUndergroundSource>();
        services.AddSingleton<IForecastSource, NwsSource>();

        // Snow Forecast rows, in this order. NewsCenter Maine, WMTW and WGME publish snow only as maps,
        // so the page has manual rows for them instead.
        services.AddSingleton<ISnowSource, NwsSnowSource>();
        services.AddSingleton<ISnowSource, WeatherUndergroundSnowSource>();
        services.AddSingleton<ISnowSource, WeatherApiSnowSource>();
        foreach (var (column, model) in OpenMeteoModels)
        {
            AddOpenMeteoSnow(services, column, model, forecastModels);
        }

        AddOpenMeteoSnow(services, WeatherNext, WeatherNextModel, [WeatherNextModel], OpenMeteoSource.EnsembleEndpoint);

        return services;
    }

    private static readonly ColumnDescriptor WeatherNext = new("weathernext", "WeatherNext (mean)");
    private const string WeatherNextModel = "google_weathernext2_ensemble_mean";

    // Open-Meteo's forecast endpoint models, grouped by agency. Each covers a different number of days:
    // HRRR and HRDPS about 2, Météo-France about 4, CMA about 5, ICON and UKMO about 7, NBM about 11,
    // GEM and JMA about 10, and ECMWF, AIFS and GFS 15 or 16. GraphCast, BOM and KMA return no snowfall.
    private static readonly (ColumnDescriptor Column, string Model)[] OpenMeteoModels =
    [
        (new("ecmwf", "ECMWF"), "ecmwf_ifs025"),
        (new("ecmwf-aifs", "ECMWF AIFS"), "ecmwf_aifs025_single"),
        (new("gfs", "GFS"), "gfs_seamless"),
        (new("nbm", "NBM"), "ncep_nbm_conus"),
        (new("hrrr", "HRRR"), "ncep_hrrr_conus"),
        (new("gem", "GEM"), "gem_seamless"),
        (new("hrdps", "HRDPS"), "gem_hrdps_continental"),
        (new("icon", "ICON"), "icon_seamless"),
        (new("ukmo", "UKMO"), "ukmo_seamless"),
        (new("jma", "JMA"), "jma_seamless"),
        (new("meteofrance", "Météo-France"), "meteofrance_seamless"),
        (new("cma", "CMA"), "cma_grapes_global"),
    ];

    // "home" must always be a registered station, or StationDirectory would treat it as a Weather Underground ID.
    private static void AddHomeStation(IServiceCollection services, WeatherOptions options)
    {
        if (options.HomeStationSource == HomeStationSource.OpenMeteo)
        {
            services.AddSingleton<IWeatherStation>(provider =>
                ActivatorUtilities.CreateInstance<OpenMeteoStation>(provider, AmbientWeatherStation.HomeId, options.DefaultLocation.Zip));
        }
        else
        {
            services.AddSingleton<IWeatherStation, AmbientWeatherStation>();
        }
    }

    private static void AddOpenMeteo(
        IServiceCollection services,
        ColumnDescriptor column,
        string model,
        IReadOnlyList<string> endpointModels,
        string endpoint = OpenMeteoSource.ForecastEndpoint) =>
        services.AddSingleton<IForecastSource>(provider =>
            ActivatorUtilities.CreateInstance<OpenMeteoSource>(provider, column, model, endpoint, endpointModels));

    private static void AddOpenMeteoSnow(
        IServiceCollection services,
        ColumnDescriptor column,
        string model,
        IReadOnlyList<string> endpointModels,
        string endpoint = OpenMeteoSource.ForecastEndpoint) =>
        services.AddSingleton<ISnowSource>(provider =>
            ActivatorUtilities.CreateInstance<OpenMeteoSnowSource>(provider, column, model, endpoint, endpointModels));

    private static void AddNewsCenterMaine(IServiceCollection services, ColumnDescriptor column, string section) =>
        services.AddSingleton<IForecastSource>(provider =>
            ActivatorUtilities.CreateInstance<NewsCenterMaineSource>(provider, column, section));
}
