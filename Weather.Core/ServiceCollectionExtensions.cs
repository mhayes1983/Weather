using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Weather.Core.Alerts;
using Weather.Core.Climate;
using Weather.Core.DailyForecast;
using Weather.Core.Locations;
using Weather.Core.Models;
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
        services.AddSingleton<IClimateNormals, AcisClimateNormals>();
        services.AddSingleton<ForecastTableService>();
        services.AddSingleton<IWeatherStation, AmbientWeatherStation>();
        services.AddSingleton<StationDirectory>();
        services.AddSingleton<StationLookup>();
        services.AddSingleton<StationService>();
        services.AddSingleton<StationLocator>();
        services.AddSingleton<NwsAlerts>();
        services.AddSingleton<AlertService>();
        services.AddSingleton<OpenMeteoOutlook>();
        services.AddSingleton<NwsDescriptions>();
        services.AddSingleton<UsnoMoon>();
        services.AddSingleton<DailyForecastService>();

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

        // Registration order is the table's column order.
        AddOpenMeteo(services, new ColumnDescriptor("ecmwf", "ECMWF"), "ecmwf_ifs025");
        AddOpenMeteo(services, new ColumnDescriptor("gfs", "GFS"), "gfs_seamless");
        AddOpenMeteo(services, new ColumnDescriptor("gem", "GEM"), "gem_seamless");
        AddOpenMeteo(services, new ColumnDescriptor("icon", "ICON"), "icon_seamless");
        AddOpenMeteo(services, new ColumnDescriptor("weathernext", "WeatherNext (mean)"), "google_weathernext2_ensemble_mean", OpenMeteoSource.EnsembleEndpoint);
        AddNewsCenterMaine(services, new ColumnDescriptor("ncm-coastal", "NCM Coastal"), "Portland 10-Day");
        AddNewsCenterMaine(services, new ColumnDescriptor("ncm-inland", "NCM Inland"), "Bangor 10-Day");
        services.AddSingleton<IForecastSource, WeatherUndergroundSource>();
        services.AddSingleton<IForecastSource, NwsSource>();

        return services;
    }

    private static void AddOpenMeteo(
        IServiceCollection services,
        ColumnDescriptor column,
        string model,
        string endpoint = OpenMeteoSource.ForecastEndpoint) =>
        services.AddSingleton<IForecastSource>(provider =>
            ActivatorUtilities.CreateInstance<OpenMeteoSource>(provider, column, model, endpoint));

    private static void AddNewsCenterMaine(IServiceCollection services, ColumnDescriptor column, string section) =>
        services.AddSingleton<IForecastSource>(provider =>
            ActivatorUtilities.CreateInstance<NewsCenterMaineSource>(provider, column, section));
}
