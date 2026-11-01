# Weather

The home page (`/`) shows live readings and 24-hour charts from an Ambient Weather station. `/daily` shows the selected station's 16-day forecast, and both it and the home page switch station through `StationHeader`. `/daily/YYYY-MM-DD` shows one day's hourly detail. `/radar` shows a zoomable radar map. `/forecast` compares daily forecast highs from several sources in one 16-day table, highlighting days well above or below average. `/help`, `/terms` and `/privacy` are static pages. Settings (theme and °F/°C) opens from the gear in the top bar, or from the side menu at 720px and below. Default location is Topsham, ME (04086); any US ZIP works. The home page can also show other Weather Underground stations from a configured list. There is no database: station history comes from each station's source on each fetch.

## Layout

- `Weather.Core`: all logic. Must stay free of ASP.NET because the Android app runs it on the phone.
- `Weather.Core.Tests`: xUnit tests. HTTP is stubbed through `TestSupport.cs`; tests never call real services.
- `Weather.Server`: thin API (`GET /api/forecast?zip=`, `GET /api/alerts?station=`, `GET /api/stations`, `GET /api/stations/lookup?q=&source=`, `GET /api/station?id=`, `GET /api/daily-forecast?station=`, `GET /api/daily-forecast/{date}?station=`). Also serves the built client.
- `weather.client`: React + TypeScript + Vite. No UI or state libraries, apart from Leaflet for the radar map, which loads only on that page.
- `Weather.Android`: .NET MAUI app with no server. It hosts `weather.client`, built with `--mode app`, in a `HybridWebView`. `appBridge.ts` sends `/api` calls through `InvokeDotNet` to `LocalApi` in Core. It has its own `Weather.Android.slnx`, so normal builds don't need the Android tools.

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
npm test
```

For the Android app, with the Pixel attached by USB:

```bash
dotnet build Weather.Android/Weather.Android.csproj -t:Run -f net10.0-android "-p:AndroidSdkDirectory=C:\Program Files (x86)\Android\android-sdk" "-p:JavaSdkDirectory=C:\Program Files\Android\openjdk\jdk-21.0.8"
```

The site runs at https://localhost:54777. If the build reports locked files, Visual Studio or another `Weather.Server` process is still running; that isn't a code error.

## Hosting

Runs on TrueNAS as a Docker custom app (`deploy/truenas-compose.yaml`). `.github/workflows/publish-image.yml` publishes the image from `master`. The container serves plain HTTP on 8080, so HTTPS redirection is Development only. User-secrets don't load there, so keys come from environment variables such as `Weather__WeatherUnderground__ApiKey`.

## Rules

- **Temperatures are °F everywhere in Core and the API.** Celsius is display-only, in `weather.client/src/temperature.ts`.
- **Highlighting is decided in Core** (`AnomalyClassifier`), always in °F, so changing units never changes colours. Thresholds are settings, currently 6° either way.
- **One bad source must never break the table.** Sources throw on failure; `ForecastTableService` turns that into a column error, or last good data if it has any.
- **Sources don't take the request's cancellation token.** Results are shared through `WeatherCache`, so each source gets its own timeout.
- **Default column order is registration order** in `ServiceCollectionExtensions.cs`. Users can hide and drag columns; that's client state and resets on reload.
- **Don't log URLs from the `weather` HTTP client.** Weather Underground's and Ambient Weather's keys travel in the query string, which is why that client's logging is off.
- **Station data stays separate from forecasts.** It's observed, so it uses `IWeatherStation` and `StationService` in `Weather.Core/Station`, not `IForecastSource`. The one exception is `OpenMeteoStation`, which gives model values for a ZIP. It sets `Modelled`, and the Station page must always say it's a modelled estimate. `/api/station` must never expose keys, the MAC address, the street address or coordinates. Configured stations use their configured name, and only a looked-up station shows WU's public neighbourhood.
- **Any well-formed station ID is accepted.** `StationDirectory` returns configured stations, or a new `WeatherUndergroundStation` for an ID a browser looked up. Cycling through IDs could use up the shared WU quota, so the site must stay on a private network unless a limit is added.
- **Looked-up stations stay in that browser.** They're saved in `localStorage`, with no accounts or server storage. The list holds 20. A full list refuses new stations until one is removed, rather than dropping one, because the dropped station could be the browser's default.
- **Forecast and alert locations stay in Core.** `StationLocator` supplies them through `IWeatherStation.GetCoordinatesAsync`, using `DefaultLocation` for the home station. `/api/daily-forecast` and `/api/alerts` return the station's configured name, or the ID for a looked-up station, never coordinates or a neighbourhood. So a looked-up station's home page shows its neighbourhood while its forecast shows its ID.
- **Alerts never show last good data.** An old alert could be wrong, so a failed check says alerts are unavailable, which must never look like "no alerts". NWS and USNO only add detail, so their failures hide a section rather than the page.
- **The home station stays on Ambient Weather.** Weather Underground is only for the other stations in `Stations`. It updates less often, reports less and has a shared quota. In the app, someone without Ambient keys gets `OpenMeteoStation` under the `home` ID through `HomeStationSource`.
- **`LocalApi` must match the controllers.** Any change to an endpoint's defaults, validation or status codes goes in both, and `LocalApiTests` compares them.
- **The app's keys stay on the phone.** They're entered in setup and kept in `SecureStorage`. They only cross the bridge going in: the client is told whether a key is saved, never what it is. App backups stay off.
- **The app never shows other sites.** `appBridge.ts` sends external links to the system browser, because a page loaded in the WebView has no way back and can reach the bridge. `OpenLink` accepts `https` only.
- **App-only client code stays out of the site.** Test `import.meta.env.MODE === 'app'` directly where it decides what loads, because the `isApp` export doesn't let the build drop it.
- **Colours follow the device by default.** Auto sets no attribute on `<html>`, so `index.css` uses `prefers-color-scheme`. Light and Dark set `data-theme`, and the choice is saved in that browser's `localStorage`. Any new colour needs defining for both themes, and dark values live in two blocks of `index.css` that must stay identical.
- **The temperature unit stays in that browser.** It's saved in `localStorage` and set only in Settings, not on individual pages.
- **The Help and Privacy pages describe what the site does.** Adding a data source, anything stored in the browser, or a host the browser calls directly means updating them, and changing `PrivacyPage` or `TermsPage` means updating its effective date.
- **A browser's default station stays in that browser.** It's saved in `localStorage` and sets which station every page starts on, with no accounts or server storage. The home station is the fallback, including when a saved station is no longer configured.
- **Keys go in .NET user-secrets**, never in `appsettings.json`, the repo or chat.
- **All caching is in memory.** Don't add a database or disk storage for forecasts.

## Adding a forecast source

1. Add a class in `Weather.Core/Sources` implementing `IForecastSource`.
2. Register it in `ServiceCollectionExtensions.cs`, in the position you want the column.
3. Add tests to `SourceTests.cs` using a stubbed response.
4. If it needs a key, return `SourceResult.NeedsKey()` when none is set, and add its options to `WeatherOptions`.
5. Add it to the data sources on `HelpPage.tsx` and `PrivacyPage.tsx`.

The table needs no client changes; columns come from the API.

## Source quirks

- **ECMWF:** Open-Meteo returns null for the last two days of the 16-day range.
- **GEM and ICON:** Open-Meteo models with about 10 and 7 days of data.
- **WeatherNext:** the ensemble mean, from Open-Meteo's separate ensemble endpoint, about 14 days. Averaging 64 members mutes long-range highs. Open-Meteo's FAQ says ensemble APIs need a paid plan, though the free host answers today, so it may stop.
- **NewsCenter Maine:** an HTML scrape of `/10-day`, Maine ZIPs only. Dates have no year, and the page drops today during the afternoon, so blank NCM cells for today are normal.
- **Weather Underground:** the key only allows the `5day` endpoint, which returns 6 days. The 7, 10 and 15-day endpoints return 401. Personal, non-commercial use only.
- **NWS:** about 7 days. Evening forecasts have no high for today.
- **Average high:** ACIS normals from the nearest station with data. Topsham resolves to Brunswick using the coordinates in `appsettings.json`; the ZIP centre would resolve to Bath instead.
- **Ambient Weather:** needs both an `apiKey` and an `applicationKey`. It allows 1 request a second per key and returns 429 above that, so `AmbientWeatherStation` runs calls one at a time with a gap. History comes in 5-minute steps and can lag by up to 10 minutes. `hourlyrainin` is a rate in in/hr, not an hourly total.
- **Station lookup:** `/v3/location/near?postalKey=ZIP:US&product=pws` returns the 10 nearest stations in parallel arrays. Its `updateTimeUtc` doesn't show when a station last reported: stations hours old by that value had readings from minutes ago. `StationLookup` ignores it and checks `/current` nearest first, through `StationService`'s cache, taking the first with a temperature and a reading from the last hour, because some send only a reading or two. A station with an unreadable reading or its own 4xx is skipped, but 401, 403 and 429 stop the scan because every other station would fail too. That's up to 11 calls for an uncached ZIP, and the result is cached for an hour. An unknown ID and a silent station both return an empty body from `/current`, so they can't be told apart.
- **Weather Underground stations:** the PWS key reads any station ID, through `/v2/pws/observations/current` and `/all/1day`. Limits are reported as 1,500 calls a day and 30 a minute, shared with the WU forecast column, which is why results are cached for 5 and 15 minutes and nothing polls in the background. A station silent for an hour returns an empty body. History covers the current local day, not 24 hours. Many stations have no UV or solar readings, and some have no rain gauge.
- **Open-Meteo Daily Forecast:** `precipitation` is liquid equivalent, including melted snow, while `snowfall` is depth, and `precipitation_unit=inch` returns both in inches. `timezone=auto` applies today's UTC offset to the whole range, so `OpenMeteoOutlook` corrects times after a clock change. Daily totals still follow that fixed offset, so they can be an hour out on those days.
- **Open-Meteo stations:** IDs are `om-{zip}`, and looking one up uses no Weather Underground calls or key. `current` covers a 15-minute interval, so its `precipitation` is scaled to in/hr. History is hourly, so `HistoryMaxGapMinutes` keeps the charts joined. The day's rain and top gust stop at now, because later hours are forecast.
- **Station sky:** stations don't report conditions, so `StationSky` asks Open-Meteo for `weather_code` and `is_day` at the station's location, cached for 15 minutes. The report waits for it, so it has a 3-second timeout of its own. It's modelled, so it can disagree with the readings beside it and the page must credit Open-Meteo. A failure or an offline station hides it. Its location comes from `StationLocator`, shared with alerts, so it adds no Weather Underground calls.
- **NWS alerts:** `/alerts/active?point=` with `status=actual&message_type=alert,update`. `limit` returns 400 on that endpoint. Times are in the alert area's local time, so they're shown as issued. Severity can be missing. `expires` is when the message lapses, not when the event ends, so it's never shown as an end time or used to hide an alert.
- **NWS descriptions:** about 7 days, so later days have none. A night period starting after midnight belongs to the previous date. The precipitation shown with a description comes from Open-Meteo's hours for 6am to 6pm or 6pm to 6am, skipping past hours, so it can differ from the day's total.
- **USNO moon data:** the moon doesn't rise or set every day, so either time can be missing. The `tz` value is for that date, with `dst=false`.
- **Radar:** the browser calls NOAA's `conus_bref_qcd` WMS and OpenStreetMap's tiles directly, so there's no server code. `GetCapabilities` lists about 60 scans over two hours, and the page uses the first scan of each ten-minute slot plus the newest, so frames stay stable as scans arrive. OpenStreetMap's tile policy requires its attribution to stay visible and suits light use only.
- **AccuWeather:** removed on purpose. Its API has no lasting free tier and its website blocks automated requests. Don't scrape it, or Weather Underground's pages.

