# Weather

Compares daily forecast highs from several sources in one 16-day table, highlighting days well above or below average. Default location is Topsham, ME (04086); any US ZIP works. Shows current data only: there is no database or history.

## Layout

- `Weather.Core`: all logic. Must stay free of ASP.NET so a future Android app can reuse it.
- `Weather.Core.Tests`: xUnit tests. HTTP is stubbed through `TestSupport.cs`; tests never call real services.
- `Weather.Server`: thin API (`GET /api/forecast?zip=`). Also serves the built client.
- `weather.client`: React + TypeScript + Vite. No UI or state libraries.

## Commands

```bash
dotnet build Weather.slnx
dotnet test Weather.Core.Tests/Weather.Core.Tests.csproj
dotnet run --project Weather.Server --launch-profile https
```

From `weather.client`:

```bash
npm run build
npm run lint
```

The app runs at https://localhost:54777. If the build reports locked files, Visual Studio or another `Weather.Server` process is still running; that isn't a code error.

## Hosting

Runs on TrueNAS as a Docker custom app (`deploy/truenas-compose.yaml`). `.github/workflows/publish-image.yml` publishes the image from `master`. The container serves plain HTTP on 8080, so HTTPS redirection is Development only. User-secrets don't load there, so keys come from environment variables such as `Weather__WeatherUnderground__ApiKey`.

## Rules

- **Temperatures are °F everywhere in Core and the API.** Celsius is display-only, in `weather.client/src/temperature.ts`.
- **Highlighting is decided in Core** (`AnomalyClassifier`), always in °F, so changing units never changes colours. Thresholds are settings, currently 6° either way.
- **One bad source must never break the table.** Sources throw on failure; `ForecastTableService` turns that into a column error, or last good data if it has any.
- **Sources don't take the request's cancellation token.** Results are shared through `WeatherCache`, so each source gets its own timeout.
- **Default column order is registration order** in `ServiceCollectionExtensions.cs`. Users can hide and drag columns; that's client state and resets on reload.
- **Don't log URLs from the `weather` HTTP client.** Weather Underground's key travels in the query string, which is why that client's logging is off.
- **Keys go in .NET user-secrets**, never in `appsettings.json`, the repo or chat.
- **All caching is in memory.** Don't add a database or disk storage for forecasts.

## Adding a forecast source

1. Add a class in `Weather.Core/Sources` implementing `IForecastSource`.
2. Register it in `ServiceCollectionExtensions.cs`, in the position you want the column.
3. Add tests to `SourceTests.cs` using a stubbed response.
4. If it needs a key, return `SourceResult.NeedsKey()` when none is set, and add its options to `WeatherOptions`.

The client needs no changes; columns come from the API.

## Source quirks

- **ECMWF:** Open-Meteo returns null for the last two days of the 16-day range.
- **GEM and ICON:** Open-Meteo models with about 10 and 7 days of data.
- **WeatherNext:** the ensemble mean, from Open-Meteo's separate ensemble endpoint, about 14 days. Averaging 64 members mutes long-range highs. Open-Meteo's FAQ says ensemble APIs need a paid plan, though the free host answers today, so it may stop.
- **NewsCenter Maine:** an HTML scrape of `/10-day`, Maine ZIPs only. Dates have no year, and the page drops today during the afternoon, so blank NCM cells for today are normal.
- **Weather Underground:** the key only allows the `5day` endpoint, which returns 6 days. The 7, 10 and 15-day endpoints return 401. Personal, non-commercial use only.
- **NWS:** about 7 days. Evening forecasts have no high for today.
- **Average high:** ACIS normals from the nearest station with data. Topsham resolves to Brunswick using the coordinates in `appsettings.json`; the ZIP centre would resolve to Bath instead.
- **AccuWeather:** removed on purpose. Its API has no lasting free tier and its website blocks automated requests. Don't scrape it, or Weather Underground's pages.

## Later work

- Android app. It should call the API rather than use Core directly, so keys stay off the device.
- Weather station data. It's observed rather than forecast, so it gets its own interface and endpoint.
