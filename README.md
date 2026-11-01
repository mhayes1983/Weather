# Weather

Shows live readings from an Ambient Weather station on the home page, and compares daily high temperatures from several forecast sources in one 16-day table. Days well above or below average are highlighted.

It opens on Topsham, ME (04086) and works with any US ZIP code. It shows current forecasts only, so there is no database.

## Features

- Station page with current conditions and 24-hour charts, refreshed every minute
- Active National Weather Service alerts for the selected station, with a page listing them all
- Zoomable radar map of Maine and the Northeast, with a two-hour loop
- Daily Forecast page for the selected station, with hourly detail, descriptions, and sun and moon times
- Side-by-side highs from each source for the next 16 days
- Orange for 6°F or more above average, blue for 6°F or more below
- Average high for the period, from the nearest climate station
- Find a station by ZIP code or ID and keep it in that browser
- Star a station to make it your default in that browser
- °F/°C toggle
- Auto, Light and Dark themes, with Auto following the device
- ZIP code search
- Usable on a phone

## Forecast sources

| Column | Source | Days | Needs a key |
|---|---|---|---|
| ECMWF | [Open-Meteo](https://open-meteo.com/) | 14 | No |
| GFS | [Open-Meteo](https://open-meteo.com/) | 16 | No |
| GEM | [Open-Meteo](https://open-meteo.com/) | 10 | No |
| ICON | [Open-Meteo](https://open-meteo.com/) | 7 | No |
| WeatherNext (mean) | [Open-Meteo](https://open-meteo.com/), Google WeatherNext 2 ensemble mean | 14 | No |
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

3. Create an API key and an application key on your [AmbientWeather.net account page](https://ambientweather.net/account), then add them. Without both, the station page asks for keys and the forecast table still works.

   ```bash
   dotnet user-secrets set "Weather:AmbientWeather:ApiKey" "YOUR_API_KEY" --project Weather.Server
   ```

   ```bash
   dotnet user-secrets set "Weather:AmbientWeather:ApplicationKey" "YOUR_APPLICATION_KEY" --project Weather.Server
   ```

4. Start the app, then open https://localhost:54777.

   ```bash
   dotnet run --project Weather.Server --launch-profile https
   ```

You can also open `Weather.slnx` in Visual Studio and run `Weather.Server`.

## Hosting on TrueNAS

Each push to `master` builds a container image and publishes it to `ghcr.io/mhayes1983/weather:latest`.

1. If the GitHub package is private, make it public or add `ghcr.io` credentials under Apps > Configuration > Manage Container Registries on the NAS.
2. In TrueNAS, go to Apps > Discover Apps > Install via YAML and paste `deploy/truenas-compose.yaml`.
3. Replace the three key placeholders with your Weather Underground and Ambient Weather keys, on the NAS only.
4. Open `http://<nas-ip>:8085`.

To update, redeploy the app, which pulls the latest image. The site serves plain HTTP, and it accepts any Weather Underground station ID, which uses your shared API limit. Keep it on your own network and use a VPN such as Tailscale for outside access.

## Daily Forecast

Shows 16 days for the station selected on the Station page, with its hourly forecast one click away. Your home station uses `DefaultLocation`, and other stations use the coordinates Weather Underground reports for them.

The forecast comes from [Open-Meteo](https://open-meteo.com/), written descriptions from the [National Weather Service](https://www.weather.gov/documentation/services-web-api) for about 7 days, and moon data from the [U.S. Naval Observatory](https://aa.usno.navy.mil/data/api). None of them need a key.

## Weather alerts

Active National Weather Service watches, warnings and advisories for the selected station appear at the top of the Station page, collapsed and most severe first. Up to three show there, and **View all alerts** opens the full list. They use the same station location as the Daily Forecast and need no key.

The page only updates while it's open and alerts are cached for two minutes, so don't rely on it for safety warnings. If the check fails, the page says so rather than showing no alerts.

## Radar

The Radar page shows NOAA/NWS base reflectivity over an OpenStreetMap map, with Maine and Northeast buttons and a two-hour loop. The browser loads the radar and map images directly from those services, so it needs no key and no server code. It shows precipitation only, and snow can look weaker than it is.

## Other weather stations

The station page has a dropdown for other Weather Underground stations, listed under `Stations` in `Weather.Server/appsettings.json`. Each entry has a case-sensitive `Id` and a `Name`. They use your Weather Underground key, and your own station stays on Ambient Weather, which updates faster and reports more.

Other stations are fetched only while selected, and results are cached so they don't use up the key's limits, reported as 1,500 calls a day and 30 a minute. Readings a station doesn't report are left out, and its charts cover the current day rather than the last 24 hours.

Type a ZIP code or Weather Underground station ID into **Find a station** to add one. A ZIP code picks the closest station that has reported in the last hour and is sending a temperature. Added stations stay in that browser's dropdown, and the ✕ button removes one. The list holds 20 stations, and when it's full you need to remove one before adding another.

Select a station and click its star to make it your default. It then opens on every visit to the site in that browser, and it's saved only in that browser, with no accounts or server storage. Clicking the star again goes back to the home station.

## Project structure

| Folder | Purpose |
|---|---|
| `Weather.Core` | Forecast sources, averages, highlighting and caching, in a library shared by the server and the Android app |
| `Weather.Core.Tests` | Unit tests |
| `Weather.Server` | ASP.NET Core API, which also serves the website |
| `Weather.Android` | .NET MAUI app that runs `Weather.Core` on the phone and hosts the website's client |
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
| `FreeSourceCacheMinutes` | 60 | How long to keep results from sources without a key |
| `ErrorRetryMinutes` | 5 | How long to wait before retrying a failed source |
| `AlertCacheMinutes` | 2 | How long to keep a station's alerts |
| `WeatherUnderground:CacheMinutes` | 60 | How long to keep Weather Underground results |
| `AmbientWeather:MacAddress` | None | Station to show, if the account has more than one. Otherwise the first is used |
| `AmbientWeather:CurrentCacheSeconds` | 60 | How long to keep current station readings |
| `AmbientWeather:HistoryCacheMinutes` | 5 | How long to keep the 24-hour station history |
| `AmbientWeather:Name` | Home station | Name of your station in the dropdown |
| `AmbientWeather:OfflineAfterMinutes` | 15 | Age of the latest reading before your station shows as offline |
| `AmbientWeather:RequestSpacingMilliseconds` | 1100 | Minimum gap between Ambient Weather requests |
| `WeatherUndergroundStations:CurrentCacheMinutes` | 5 | How long to keep another station's current readings |
| `WeatherUndergroundStations:HistoryCacheMinutes` | 15 | How long to keep another station's history |
| `WeatherUndergroundStations:OfflineAfterMinutes` | 30 | Age of the latest reading before another station shows as offline |
| `Stations` | See file | Weather Underground stations in the dropdown |

The website's default ZIP code is set separately, in `weather.client/src/App.tsx`.

## Development

Run the tests:

```bash
dotnet test Weather.Core.Tests/Weather.Core.Tests.csproj
```

Build, lint and test the website from `weather.client`:

```bash
npm run build
npm run lint
npm test
```

To add a forecast source, implement `IForecastSource` in `Weather.Core/Sources` and register it in `ServiceCollectionExtensions.cs`. The new column then appears in the table automatically.

## API

`GET /api/forecast?zip=04086`

The `zip` value is optional and defaults to Topsham. All temperatures are in °F. It returns 400 for a badly formatted ZIP code and 404 for one that doesn't exist.

`GET /api/daily-forecast?station=KMESCARB113`

Returns the station's 16-day forecast. The `station` value is optional and defaults to your home station. It returns 404 for a badly formed station ID.

`GET /api/daily-forecast/2026-11-02?station=KMESCARB113`

Returns one day's hourly forecast, description, sun and moon data, and the previous and next days that can be opened. It returns 404 when that day isn't in the forecast or has no hourly data, and 503 when the forecast can't be loaded. Times are the station's local time.

`GET /api/alerts?station=KMESCARB113`

Returns the station's active alerts, most severe first. The `station` value is optional and defaults to your home station. It returns 404 for a badly formed station ID.

`GET /api/stations/lookup?q=04086`

Finds a station by ZIP code or ID. It returns 400 for a badly formed value, 404 when nothing has reported in the last hour, and 503 when Weather Underground can't be reached or no key is set.

`GET /api/stations`

Lists the stations for the dropdown, with your home station first.

`GET /api/station?id=KMESCARB113`

Returns a station's current readings and history. The `id` value is optional and defaults to your home station. All temperatures are in °F. It returns 404 for a badly formed station ID.

## Planned

- Winter weather page (snow, ice, wind, and alerts)

## Data use

Open-Meteo's free API is for non-commercial use, and its data is licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/), so the website credits it. Weather Underground data is limited to personal, non-commercial use. A public or commercial version of this site would need a different arrangement. The NewsCenter Maine columns read the station's web page, so a redesign of that page could break them.
