using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Weather.Core.Models;
using Weather.Core.Snow;
using Weather.Core.Sources;

namespace Weather.Core.Tests;

public class SourceColumnsTests
{
    [Fact]
    public void Ids_AreUnique_AndLabelsAreShort()
    {
        Assert.Equal(SourceColumns.All.Count, SourceColumns.All.Select(column => column.Id).Distinct().Count());
        Assert.Equal(SourceColumns.All.Count, SourceColumns.All.Select(column => column.Label).Distinct().Count());
        Assert.All(SourceColumns.All, column =>
            Assert.InRange(column.Label.Length, 1, SourceColumns.MaxLabelLength));
    }

    [Fact]
    public void BothTables_NameEachSourceFromSourceColumns()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWeatherCore(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DefaultLocation:Zip"] = "04086",
        }).Build());
        using var provider = services.BuildServiceProvider();

        var forecast = provider.GetServices<IForecastSource>().Select(source => source.Column).ToList();
        var snow = provider.GetServices<ISnowSource>().Select(source => source.Column).ToList();

        Assert.All(forecast.Concat(snow), column => Assert.Contains(column, SourceColumns.All));
        Assert.Equal(forecast.Count, forecast.Select(column => column.Id).Distinct().Count());
        Assert.Equal(snow.Count, snow.Select(column => column.Id).Distinct().Count());

        // The sources on both pages, so the same id never gets two names.
        Assert.Contains(SourceColumns.Nws, snow);
        Assert.Contains(SourceColumns.WeatherUnderground, snow);
        Assert.Contains(SourceColumns.WeatherNext, snow);
    }
}
