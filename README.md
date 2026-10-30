# Weather

Compares daily high temperatures from several forecast sources in one 16-day table. Days well above or below average are highlighted, replacing a spreadsheet that was filled in by hand.

It opens on Topsham, ME (04086) and works with any US ZIP code. It shows current forecasts only, so there is no database.

## Features

- Side-by-side highs from each source for the next 16 days
- Orange for 6°F or more above average, blue for 6°F or more below
- Average high for the period, from the nearest climate station
- °F/°C toggle
- ZIP code search
- Usable on a phone

## Forecast sources

| Column | Source | Days | Needs a key |
|---|---|---|---|
| ECMWF | [Open-Meteo](https://open-meteo.com/) | 14 | No |
| GFS | [Open-Meteo](https://open-meteo.com/) | 16 | No |
| NCM Coastal | [NewsCenter Maine](https://www.newscentermaine.com/10-day), Portland 10-Day | 10 | No |
| NCM Inland | [NewsCenter Maine](https://www.newscentermaine.com/10-day), Bangor 10-Day | 10 | No |
| WU | Weather Underground | 6 | Yes, a personal weather station key |
| NWS | [National Weather Service](https://www.weather.gov/documentation/services-web-api) | About 7 | No |

Average highs come from [ACIS](https://www.rcc-acis.org/), and ZIP codes are looked up with [Zippopotam](https://www.zippopotam.us/).

Cells stay blank past the end of a source's range. The NCM columns only appear for Maine ZIP codes. If a source fails, its column shows an error and the rest of the table still loads.

## Getting started

You'll need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and [Node.js](https://nodejs.org/).

1. Trust the .NET development certificate. You only need to do this once.

   ```bash
   dotnet dev-certs https --trust
   ```

2. Add your Weather Underground key. Without one, the WU column shows "needs key" and everything else still works.

   ```bash
   dotnet user-secrets set "Weather:WeatherUnderground:ApiKey" "YOUR_KEY" --project Weather.Server
   ```

3. Start the app, then open https://localhost:54777.

   ```bash
   dotnet run --project Weather.Server --launch-profile https
   ```

You can also open `Weather.slnx` in Visual Studio and run `Weather.Server`.

## Project structure

| Folder | Purpose |
|---|---|
| `Weather.Core` | Forecast sources, averages, highlighting and caching, in a library that a future Android app can reuse |
| `Weather.Core.Tests` | Unit tests |
| `Weather.Server` | ASP.NET Core API, which also serves the website |
| `weather.client` | React and TypeScript website |

## Settings

These are in the `Weather` section of `Weather.Server/appsettings.json`:

| Setting | Default | Purpose |
|---|---|---|
| `DefaultLocation` | Topsham, ME | Location used when no ZIP code is given |
| `AboveAverageF` | 6 | Degrees above average that turn a cell orange |
| `BelowAverageF` | 6 | Degrees below average that turn a cell blue |
| `ForecastDays` | 16 | Number of rows in the table |
| `SourceTimeoutSeconds` | 15 | How long to wait for each source |
| `FreeSourceCacheMinutes` | 30 | How long to keep results from sources without a key |
| `ErrorRetryMinutes` | 5 | How long to wait before retrying a failed source |
| `WeatherUnderground:CacheMinutes` | 60 | How long to keep Weather Underground results |

The website's default ZIP code is set separately, in `weather.client/src/App.tsx`.

## Development

Run the tests:

```bash
dotnet test Weather.Core.Tests/Weather.Core.Tests.csproj
```

Build and lint the website from `weather.client`:

```bash
npm run build
npm run lint
```

To add a forecast source, implement `IForecastSource` in `Weather.Core/Sources` and register it in `ServiceCollectionExtensions.cs`. The new column then appears in the table automatically.

## API

`GET /api/forecast?zip=04086`

The `zip` value is optional and defaults to Topsham. All temperatures are in °F. It returns 400 for a badly formatted ZIP code and 404 for one that doesn't exist.

## Planned

- Android app
- Personal weather station data
- A free replacement for the AccuWeather column

## Data use

Weather Underground data is limited to personal, non-commercial use. A public or commercial version of this site would need a different arrangement. The NewsCenter Maine columns read the station's web page, so a redesign of that page could break them.
