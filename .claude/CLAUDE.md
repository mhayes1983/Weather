# Weather

The home page (`/`) shows live readings and 24-hour charts from an Ambient Weather station. `/forecast` compares daily forecast highs from several sources in one 16-day table, highlighting days well above or below average. Default location is Topsham, ME (04086); any US ZIP works. There is no database: station history comes from Ambient Weather on each fetch.

## Layout

- `Weather.Core`: all logic. Must stay free of ASP.NET so a future Android app can reuse it.
- `Weather.Core.Tests`: xUnit tests. HTTP is stubbed through `TestSupport.cs`; tests never call real services.
- `Weather.Server`: thin API (`GET /api/forecast?zip=`, `GET /api/station`). Also serves the built client.
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
- **Don't log URLs from the `weather` HTTP client.** Weather Underground's and Ambient Weather's keys travel in the query string, which is why that client's logging is off.
- **Station data stays separate from forecasts.** It's observed, so it uses `IWeatherStation` and `StationService` in `Weather.Core/Station`, not `IForecastSource`. `/api/station` must never expose keys, the MAC address, the street address or coordinates.
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
- **Ambient Weather:** needs both an `apiKey` and an `applicationKey`. It allows 1 request a second per key and returns 429 above that, so `AmbientWeatherStation` runs calls one at a time with a gap. History comes in 5-minute steps and can lag by up to 10 minutes. `hourlyrainin` is a rate in in/hr, not an hourly total.
- **AccuWeather:** removed on purpose. Its API has no lasting free tier and its website blocks automated requests. Don't scrape it, or Weather Underground's pages.

## Later work

- Android app. It should call the API rather than use Core directly, so keys stay off the device.
