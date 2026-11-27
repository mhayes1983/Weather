# Weather

Shows live readings and an almanac from an Ambient Weather station on the home page, a 16-day forecast, weather maps, and two comparison pages: daily lows and highs from several forecast sources in one 16-day table, and snow, rain and wind for one storm across several towns. Days well above or below normal are highlighted.

It opens on Topsham, ME (04086) and works with any US ZIP code. It shows current forecasts only, so there is no database.

## Features

- Station page with current conditions, trends and 24-hour charts, refreshed every minute, with cards and charts you can reorder and collapse
- Almanac for yesterday, today and tomorrow: high, low, precipitation and snow against the normal and the record, sunrise, sunset, hours of daylight and how they've changed, moon times and phase, and the tides near the coast
- Active National Weather Service alerts for the selected station, with a page listing them all
- Daily Forecast page for the selected station, with hourly detail, descriptions, sun and moon times, and the NWS forecasters' discussion
- Maps page: a zoomable US map of radar, precipitation type, temperature, clouds, or lightning with warnings
- Temperature Forecast: side-by-side lows and highs from each source for the next 16 days, with a consensus column
- Orange for 6°F or more above normal, blue for 6°F or more below, against normals from the nearest climate station
- Storm Forecast: each source's snow, rain, strongest gust and snow depth for one storm's dates across several towns, with averages, timing, reports, alerts and an outage risk guide
- Find a station by ZIP code or ID and keep it in that browser
- Star a station to make it your default in that browser
- Settings for °F/°C and Auto, Light and Dark themes, with Auto following the device
- ZIP code search
- Usable on a phone, and as an Android app

## Forecast sources

The Temperature Forecast and Storm Forecast tables use the same short names, set once in `Weather.Core/Models/SourceColumns.cs`. Hovering over a name shows the full source.

| Column | Source | Days | Needs a key |
|---|---|---|---|
| ECMWF | [Open-Meteo](https://open-meteo.com/), ECMWF IFS | About 15 | No |
| AIFS | [Open-Meteo](https://open-meteo.com/), ECMWF AIFS | About 15 | No |
| GFS | [Open-Meteo](https://open-meteo.com/), NOAA GFS | 16 | No |
| NBM | [Open-Meteo](https://open-meteo.com/), National Blend of Models, US only | About 11 | No |
| HRRR | [Open-Meteo](https://open-meteo.com/), NOAA HRRR, US only | About 2 | No |
| GEM | [Open-Meteo](https://open-meteo.com/), Environment Canada GEM | About 10 | No |
| HRDPS | [Open-Meteo](https://open-meteo.com/), Environment Canada HRDPS, northern US only | About 2 | No |
| ICON | [Open-Meteo](https://open-meteo.com/), DWD ICON | About 7 | No |
| UKMO | [Open-Meteo](https://open-meteo.com/), UK Met Office | About 7 | No |
| JMA | [Open-Meteo](https://open-meteo.com/), Japan Meteorological Agency | About 10 | No |
| MF | [Open-Meteo](https://open-meteo.com/), Météo-France ARPEGE | About 4 | No |
| CMA | [Open-Meteo](https://open-meteo.com/), CMA GRAPES | About 5 | No |
| WN | [Open-Meteo](https://open-meteo.com/), Google WeatherNext 2 ensemble mean | About 14 | No |
| NCM-C | [NewsCenter Maine](https://www.newscentermaine.com/10-day), Portland 10-Day | 10 | No |
| NCM-I | [NewsCenter Maine](https://www.newscentermaine.com/10-day), Bangor 10-Day | 10 | No |
| WU | Weather Underground | 6 | Yes, a personal weather station key |
| NWS | [National Weather Service](https://www.weather.gov/documentation/services-web-api) | About 7 | No |

The Storm Forecast page also uses WAPI ([WeatherAPI.com](https://www.weatherapi.com/), 3 days on the free plan, needs a key), and has manual rows for NCM, WMTW and WGME, whose snow totals are only published as map images. Its reports come from NWS Local Storm Reports via the [Iowa Environmental Mesonet](https://mesonet.agron.iastate.edu/).

Normal lows and highs, and the almanac's records, come from [ACIS](https://www.rcc-acis.org/). Moon data comes from the [U.S. Naval Observatory](https://aa.usno.navy.mil/data/api), tides from [NOAA Tides and Currents](https://tidesandcurrents.noaa.gov/), and ZIP codes are looked up with [Zippopotam](https://www.zippopotam.us/). None of them need a key.

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

   Optionally, add a [WeatherAPI.com](https://www.weatherapi.com/) key for its Storm Forecast row the same way, as `Weather:WeatherApi:ApiKey`.

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
3. Replace the three key placeholders with your Weather Underground and Ambient Weather keys, on the NAS only. The WeatherAPI.com key is optional and commented out.
4. Open `http://<nas-ip>:8085`.

To update, redeploy the app, which pulls the latest image. The site serves plain HTTP, and it accepts any Weather Underground station ID, which uses your shared API limit. Keep it on your own network and use a VPN such as Tailscale for outside access.

## Daily Forecast

Shows 16 days for the station selected on the Station page, with its hourly forecast one click away. Your home station uses `DefaultLocation`, and other stations use the coordinates Weather Underground reports for them.

The forecast and sun times come from [Open-Meteo](https://open-meteo.com/), written descriptions and the forecasters' Area Forecast Discussion from the [National Weather Service](https://www.weather.gov/documentation/services-web-api) for about 7 days, and moon data from the [U.S. Naval Observatory](https://aa.usno.navy.mil/data/api). None of them need a key.

## Almanac

Below the Station page's charts, the almanac has tabs for yesterday, today and tomorrow. Each compares the high, low, precipitation and snow (measured for yesterday, forecast for today and tomorrow) with the normal and record from the nearest climate station that still reports and has at least 30 years of records, from [ACIS](https://www.rcc-acis.org/).

It also shows sunrise and sunset, hours of daylight, the change from the day before, and the total change since the last solstice (around June 21 or December 21). Sun times come from Open-Meteo, so they match the Daily Forecast, and the solstice total uses Open-Meteo's archive. Moon times and phase come from the U.S. Naval Observatory, and if it can't be reached only the moon is left out.

Near the coast it shows the morning and afternoon high and low tides, predicted by [NOAA Tides and Currents](https://tidesandcurrents.noaa.gov/) for the nearest tide station within 10 miles, in feet above mean lower low water. Inland stations show no tides.

## Weather alerts

Active National Weather Service watches, warnings and advisories for the selected station appear at the top of the Station page, collapsed and most severe first. Up to three show there, and **View all alerts** opens the full list. They use the same station location as the Daily Forecast and need no key.

The page only updates while it's open and alerts are cached for two minutes, so don't rely on it for safety warnings. If the check fails, the page says so rather than showing no alerts.

## Maps

The Maps page shows one map type at a time over an OpenStreetMap map, with Maine and Northeast buttons:

- **Radar:** NOAA/NWS base reflectivity, with a two-hour loop. Snow can look weaker than it is.
- **Precip type:** rain, snow, convective rain and hail from the same NOAA service. It has no sleet or freezing rain category.
- **Temperature:** the NWS forecast for the coming hours, not observed values.
- **Clouds:** GOES infrared satellite from NOAA nowCOAST.
- **Lightning & warnings:** strike density from nowCOAST, under the current NWS warnings.

The browser loads these images directly from NOAA and OpenStreetMap, so the page needs no key and no server code.

## Storm Forecast

Compares each source's forecast for one storm's dates across several towns, starting with those in `SnowTowns`. A toggle switches between four views: Snow, Rain (liquid, including melted snow), Wind (strongest gust) and Depth (modelled snow on the ground). Each has averages with the sources' spread, and all but Depth have manual rows for TV outlets, each source's storm timing, measured reports and the towns' alerts for that view. Snow adds NWS ice, a snow-to-liquid ratio and a mixed-precipitation flag, and an outage risk badge shows in every view.

TV outlets publish their totals only as map images, so their rows are typed in and kept in that browser. Selecting a source in Storm timing, or a model in Depth, opens its forecast period by period for one town. The page loads only when you press Go, which fetches every view at once, and its address then holds the dates, towns and view, so it can be shared.

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
| `Weather.Android` | .NET MAUI app that runs `Weather.Core` on the phone and hosts the website's client, with its own `Weather.Android.slnx` |
| `weather.client` | React and TypeScript website |

## Settings

These are in the `Weather` section of `Weather.Server/appsettings.json`:

| Setting | Default | Purpose |
|---|---|---|
| `DefaultLocation` | Topsham, ME | Location used when no ZIP code is given |
| `AboveAverageF` | 6 | Degrees above average that turn a cell orange |
| `BelowAverageF` | 6 | Degrees below average that turn a cell blue |
| `ForecastDays` | 16 | Number of days in the forecasts |
| `SourceTimeoutSeconds` | 15 | How long to wait for each source |
| `FreeSourceCacheMinutes` | 60 | How long to keep results from sources without a key |
| `ErrorRetryMinutes` | 5 | How long to wait before retrying a failed source |
| `AlertCacheMinutes` | 2 | How long to keep a station's alerts |
| `WeatherUnderground:CacheMinutes` | 60 | How long to keep Weather Underground results |
| `WeatherApi:ForecastDays` | 3 | Days WeatherAPI.com forecasts on its free plan |
| `WeatherApi:CacheMinutes` | 60 | How long to keep WeatherAPI.com results |
| `SnowTowns` | See file | Towns the Storm Forecast page starts with, in column order |
| `AmbientWeather:MacAddress` | None | Station to show, if the account has more than one. Otherwise the first is used |
| `AmbientWeather:CurrentCacheSeconds` | 60 | How long to keep current station readings |
| `AmbientWeather:HistoryCacheMinutes` | 5 | How long to keep the 24-hour station history |
| `AmbientWeather:Name` | Home station | Name of your station in the dropdown |
| `AmbientWeather:OfflineAfterMinutes` | 15 | Age of the latest reading before your station shows as offline |
| `AmbientWeather:RequestSpacingMilliseconds` | 1100 | Minimum gap between Ambient Weather requests |
| `WeatherUndergroundStations:CurrentCacheMinutes` | 5 | How long to keep another station's current readings |
| `WeatherUndergroundStations:HistoryCacheMinutes` | 15 | How long to keep another station's history |
| `WeatherUndergroundStations:OfflineAfterMinutes` | 30 | Age of the latest reading before another station shows as offline |
| `OpenMeteoStations:CurrentCacheMinutes` | 15 | How long to keep a modelled station's current values |
| `OpenMeteoStations:HistoryCacheMinutes` | 60 | How long to keep a modelled station's history |
| `OpenMeteoStations:OfflineAfterMinutes` | 180 | Age of the latest value before a modelled station shows as offline |
| `OpenMeteoStations:SkyTimeoutMilliseconds` | 3000 | How long a station page waits for Open-Meteo's sky conditions |
| `Stations` | See file | Weather Underground stations in the dropdown, each with an `Id`, `Name` and optional `Zip` for the Temperature Forecast |

The Temperature Forecast's ZIP box starts on the current station's ZIP. Its fallback is set separately, in `weather.client/src/pages/ForecastPage.tsx`.

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

To add a forecast source, implement `IForecastSource` in `Weather.Core/Sources` with a column from `SourceColumns`, register it in `ServiceCollectionExtensions.cs`, and add it to the Help and Privacy pages and `sourceDescriptions.ts`. The new column then appears in the table automatically. `.claude/CLAUDE.md` has the full checklist.

## Android app

`Weather.Android` runs `Weather.Core` on the phone, with no server, and shows the website's client built with `--mode app`. Setup asks for a ZIP code and optional Weather Underground, Ambient Weather and WeatherAPI.com keys, which stay on the phone. Anything you skip uses Open-Meteo instead.

With the phone attached by USB:

```bash
dotnet build Weather.Android/Weather.Android.csproj -t:Run -f net10.0-android
```

To release a version, raise `ApplicationDisplayVersion` and `ApplicationVersion` in `Weather.Android.csproj`. When that reaches `master`, `.github/workflows/android-release.yml` builds the signed APK and publishes an `android-v{version}` release. A local build is signed with that machine's debug key, so it can't update a released APK, or the other way round.

## API

`GET /api/forecast?zip=04086`

The `zip` value is optional and defaults to Topsham. All temperatures are in °F. It returns 400 for a badly formatted ZIP code and 404 for one that doesn't exist.

`GET /api/daily-forecast?station=KMESCARB113`

Returns the station's 16-day forecast. The `station` value is optional and defaults to your home station. It returns 404 for a badly formed station ID.

`GET /api/daily-forecast/2026-11-02?station=KMESCARB113`

Returns one day's hourly forecast, description, sun and moon data, and the previous and next days that can be opened. It returns 404 when that day isn't in the forecast or has no hourly data, and 503 when the forecast can't be loaded. Times are the station's local time.

`GET /api/alerts?station=KMESCARB113`

Returns the station's active alerts, most severe first. The `station` value is optional and defaults to your home station. It returns 404 for a badly formed station ID.

`GET /api/discussion?station=KMESCARB113`

Returns the NWS Area Forecast Discussion for the station's forecast office, or none when it can't be loaded. It returns 404 for a badly formed station ID.

`GET /api/almanac?station=KMESCARB113`

Returns yesterday, today and tomorrow, each with the high, low, precipitation and snow against the normal and record, sun and moon times, daylight, and tides where there's a station nearby. It names the climate and tide stations so the page can credit them. It returns 404 for a badly formed station ID.

`GET /api/stations/lookup?q=04086`

Finds a station by ZIP code or ID. It returns 400 for a badly formed value, 404 when nothing has reported in the last hour, and 503 when Weather Underground can't be reached or no key is set.

`GET /api/stations/lookup?q=04086&source=open-meteo`

Returns an Open-Meteo station for a ZIP code, with an ID such as `om-04086`. It needs no key and makes no Weather Underground calls. It returns 400 for a badly formed ZIP code, 404 for an unknown one, and 503 when the ZIP code can't be checked.

`GET /api/stations`

Lists the stations for the dropdown, with your home station first.

`GET /api/station?id=KMESCARB113`

Returns a station's current readings and history. The `id` value is optional and defaults to your home station. All temperatures are in °F. It returns 404 for a badly formed station ID.

`modelled` is true when the values come from Open-Meteo rather than a station, and `historyMaxGapMinutes` is the longest gap between history points that charts should join. `sky` holds Open-Meteo's `conditions` and `icon` for the station's location, or null when they aren't available.

`GET /api/snow/towns`

Lists the Storm Forecast's starting towns.

`GET /api/snow?zips=04086,04101&start=2026-11-12&end=2026-11-13`

Returns each source's snow, rain, wind and depth for those towns and dates. `GET /api/snow/hourly?zip=04086&start=…&end=…` returns every source's forecast periods for one town, `GET /api/snow/reports?zips=…&start=…&end=…` the reports near each town, and `GET /api/snow/alerts?zips=…` the towns' active alerts. Each returns 400 for badly formed towns or dates.

## Data use

Open-Meteo's free API is for non-commercial use, and its data is licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/), so the website credits it. Weather Underground data is limited to personal, non-commercial use. A public or commercial version of this site would need a different arrangement. The NewsCenter Maine columns read the station's web page, so a redesign of that page could break them.

## License

The code is released under the [MIT License](LICENSE). It covers the code only, not the weather data: each data source keeps its own terms, described under [Data use](#data-use), and anyone running the site with their own keys must follow them.
