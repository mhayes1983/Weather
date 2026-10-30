using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Weather.Core.Climate;
using Weather.Core.Locations;
using Weather.Core.Models;
using Weather.Core.Sources;

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

        // Logging is off for this client because Weather Underground keys travel in the URL.
        services.AddHttpClient(WeatherHttp.ClientName, (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<WeatherOptions>>().Value;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        }).RemoveAllLoggers();

        // Registration order is the table's column order.
        AddOpenMeteo(services, new ColumnDescriptor("ecmwf", "ECMWF"), "ecmwf_ifs025");
        AddOpenMeteo(services, new ColumnDescriptor("gfs", "GFS"), "gfs_seamless");
        AddNewsCenterMaine(services, new ColumnDescriptor("ncm-coastal", "NCM Coastal"), "Portland 10-Day");
        AddNewsCenterMaine(services, new ColumnDescriptor("ncm-inland", "NCM Inland"), "Bangor 10-Day");
        services.AddSingleton<IForecastSource, WeatherUndergroundSource>();
        services.AddSingleton<IForecastSource, NwsSource>();

        return services;
    }

    private static void AddOpenMeteo(IServiceCollection services, ColumnDescriptor column, string model) =>
        services.AddSingleton<IForecastSource>(provider =>
            ActivatorUtilities.CreateInstance<OpenMeteoSource>(provider, column, model));

    private static void AddNewsCenterMaine(IServiceCollection services, ColumnDescriptor column, string section) =>
        services.AddSingleton<IForecastSource>(provider =>
            ActivatorUtilities.CreateInstance<NewsCenterMaineSource>(provider, column, section));
}
