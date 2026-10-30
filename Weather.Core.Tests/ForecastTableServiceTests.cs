using Microsoft.Extensions.Logging.Abstractions;
using Weather.Core.Climate;
using Weather.Core.Locations;
using Weather.Core.Models;
using Weather.Core.Sources;

namespace Weather.Core.Tests;

public class ForecastTableServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 30);

    private sealed class FakeSource(string id, Func<SourceResult> fetch, bool applies = true) : IForecastSource
    {
        public int Calls { get; private set; }
        public ColumnDescriptor Column { get; } = new(id, id.ToUpperInvariant());
        public TimeSpan CacheDuration { get; init; } = TimeSpan.FromMinutes(30);
        public bool AppliesTo(ForecastLocation location) => applies;

        public Task<SourceResult> GetDailyHighsAsync(ForecastLocation location, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(fetch());
        }
    }

    private sealed class FakeNormals(bool fails = false) : IClimateNormals
    {
        public Task<ClimateNormals> GetAsync(ForecastLocation location, DateOnly start, DateOnly end, CancellationToken cancellationToken)
        {
            if (fails)
            {
                throw new HttpRequestException("Down");
            }

            var highs = Enumerable.Range(0, end.DayNumber - start.DayNumber + 1)
                .ToDictionary(start.AddDays, _ => 53);
            return Task.FromResult(new ClimateNormals("BRUNSWICK", highs));
        }
    }

    private static SourceResult HighToday(int high) => SourceResult.Ok([new DailyHigh(Today, high)], TestData.Now);

    private static ForecastTableService CreateService(
        IEnumerable<IForecastSource> sources,
        IClimateNormals? normals = null,
        TimeProvider? time = null,
        WeatherCache? cache = null)
    {
        cache ??= new WeatherCache();
        var options = TestData.Options();
        var resolver = new LocationResolver(
            StubHttp.Json(_ => throw new InvalidOperationException("No lookup expected.")), cache, options);
        return new ForecastTableService(
            sources,
            normals ?? new FakeNormals(),
            resolver,
            cache,
            options,
            time ?? new FixedTime(TestData.Now),
            NullLogger<ForecastTableService>.Instance);
    }

    [Fact]
    public async Task BuildsSixteenRowsFromTodayWithHighlights()
    {
        var table = await CreateService([new FakeSource("gfs", () => HighToday(60))]).GetAsync("04086");

        Assert.Equal(16, table.Rows.Count);
        Assert.Equal(Today, table.Rows[0].Date);
        Assert.Equal(Today.AddDays(15), table.Rows[^1].Date);
        Assert.Equal(new ForecastCell(60, Anomaly.Above), table.Rows[0].Cells["gfs"]);
        Assert.Empty(table.Rows[1].Cells);
        Assert.Equal(new AverageHigh(53, 53, "BRUNSWICK"), table.AverageHigh);
    }

    [Fact]
    public async Task StartsFromTheLocationsLocalDate()
    {
        // 02:00 UTC on Oct 31 is still Oct 30 in Maine.
        var time = new FixedTime(new DateTimeOffset(2026, 10, 31, 2, 0, 0, TimeSpan.Zero));

        var table = await CreateService([], time: time).GetAsync("04086");

        Assert.Equal(Today, table.Rows[0].Date);
    }

    [Fact]
    public async Task OneFailingSourceLeavesTheOthersIntact()
    {
        var table = await CreateService(
        [
            new FakeSource("bad", () => throw new HttpRequestException("Boom")),
            new FakeSource("good", () => HighToday(55)),
        ]).GetAsync("04086");

        Assert.Equal(SourceStatus.Error, table.Columns[0].Status);
        Assert.Equal(55, table.Rows[0].Cells["good"].HighF);
    }

    [Fact]
    public async Task FailedRefreshKeepsLastGoodData()
    {
        var cache = new WeatherCache();
        var shouldFail = false;
        var source = new FakeSource(
            "flaky",
            () => shouldFail ? throw new TimeoutException() : HighToday(55))
        { CacheDuration = TimeSpan.Zero + TimeSpan.FromTicks(1) };
        var service = CreateService([source], cache: cache);

        await service.GetAsync("04086");
        await Task.Delay(5);
        shouldFail = true;
        var table = await service.GetAsync("04086");

        Assert.Equal(2, source.Calls);
        Assert.Equal(55, table.Rows[0].Cells["flaky"].HighF);
        Assert.StartsWith("Showing last good data", table.Columns[0].Message);
    }

    [Fact]
    public async Task HidesSourcesThatDoNotApply()
    {
        var table = await CreateService(
        [
            new FakeSource("gfs", () => HighToday(55)),
            new FakeSource("ncm", () => HighToday(55), applies: false),
        ]).GetAsync("04086");

        Assert.Equal(["gfs"], table.Columns.Select(column => column.Id));
    }

    [Fact]
    public async Task ReturnsHighsWithoutHighlightsWhenAveragesAreUnavailable()
    {
        var table = await CreateService([new FakeSource("gfs", () => HighToday(90))], new FakeNormals(fails: true)).GetAsync("04086");

        Assert.Equal(new ForecastCell(90, Anomaly.None), table.Rows[0].Cells["gfs"]);
        Assert.Null(table.AverageHigh);
        Assert.NotNull(table.AverageMessage);
    }

    [Fact]
    public async Task CachesSourceResults()
    {
        var source = new FakeSource("gfs", () => HighToday(55));
        var service = CreateService([source]);

        await service.GetAsync("04086");
        await service.GetAsync("04086");

        Assert.Equal(1, source.Calls);
    }
}
