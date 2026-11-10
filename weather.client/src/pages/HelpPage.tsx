import RouteLink from '../components/RouteLink';
import type { Route } from '../useRoute';

interface HelpPageProps {
    onNavigate: (route: Route) => void;
}

function HelpPage({ onNavigate }: HelpPageProps) {
    return (
        <main className="prose">
            <header>
                <h1>Help &amp; About</h1>
            </header>

            <p>
                This site shows live readings from a home weather station alongside forecasts, alerts and radar. It starts
                on Topsham, Maine, and you can look up other stations and any US ZIP code.
            </p>

            <h2>Station</h2>
            <p>
                The <RouteLink route="/" onNavigate={onNavigate}>Station</RouteLink> page shows current conditions and charts
                of recent readings, and refreshes every minute. Charts cover the last 24 hours for the home station and the
                current day for other stations. Readings a station doesn't report are left out, and add-on sensors such as
                extra thermometers, soil probes, air quality monitors, lightning detectors and leak sensors appear only when
                the station has them. The Temperature card shows the highest and lowest readings over the same period as the
                charts, with the time of each, and the pressure trend is the change over the last 3 hours: steady is under
                0.02 inHg, and rapid is 0.06 inHg or more. Select a card's or chart's title to collapse or expand it, and drag its ⠿ handle (or focus it and
                use the arrow keys) to move it. Your layout is saved in this browser and applies to every station. The description of the sky beside the temperature is modelled by Open-Meteo for
                the station's area, so it can occasionally differ from what the station measures.
            </p>
            <p>
                When the National Weather Service has active alerts for the station's area, they appear at the top of the
                page. If alerts can't be checked, the page says so. That doesn't mean there are no alerts.
            </p>
            <p>
                Below the charts, the almanac compares the high, low, precipitation and snow with the average and record
                for the date. It opens on today, and its tabs switch to yesterday, which shows what was measured, or
                tomorrow, which shows the forecast like today. The line under the table says how far that day's high
                and low are above or below average. The average, record and actual come from the nearest
                climate station that still reports and has at least 30 years of records, so they can differ from the
                station's own readings. Temperature Forecast uses the same station for its averages. A dash means that
                value isn't available, and amounts the station only reported as part of a several-day total aren't
                shown as one day's. Each tab also shows that date's sunrise, sunset, moonrise, moonset and moon phase.
            </p>

            <h2>Choosing a station</h2>
            <p>Press <strong>Change station</strong> on the Station or Daily Forecast page. Both pages use the same station.</p>
            <ul>
                <li>Use the <strong>Station</strong> list to switch stations.</li>
                <li>To add one, enter a ZIP code or Weather Underground station ID under <strong>Find a station</strong>. A ZIP finds the nearest station that is reporting.</li>
                <li>Or enter a ZIP code and press <strong>Use Open-Meteo for this ZIP</strong>. This shows modelled estimates for that area rather than readings from a station.</li>
                <li>Added stations stay in your list, which holds up to 20. Select one and press <strong>✕</strong> to remove it.</li>
                <li>Press the <strong>star</strong> to make the selected station your default, so every page starts there.</li>
            </ul>

            <h2>Daily Forecast</h2>
            <p>
                The <RouteLink route="/daily" onNavigate={onNavigate}>Daily Forecast</RouteLink> page shows 16 days for the
                selected station. Select a day to see its hourly forecast, sun and moon times, and a written description where
                one is available. Written descriptions cover about the first 7 days.
            </p>
            <p>
                Below the days, <strong>Forecasters' discussion</strong> opens the latest Area Forecast Discussion from the
                National Weather Service office for the station's area. It's the forecasters' own explanation of the
                weather pattern and how confident they are, written for other forecasters, so it uses technical terms. Its list
                of watches and warnings is left out, because alerts are checked separately. It's hidden when it can't be
                loaded.
            </p>
            <p>
                Precipitation amounts are the rain and melted snow combined. When snow is expected, its depth is shown
                separately, because it's usually several times the melted amount.
            </p>

            <h2>Maps</h2>
            <p>
                The <RouteLink route="/radar" onNavigate={onNavigate}>Maps</RouteLink> page shows a US weather map you can drag
                and zoom. The buttons above the map switch between one map type at a time:
            </p>
            <ul>
                <li><strong>Radar</strong>: precipitation intensity. Snow can look weaker than it is.</li>
                <li><strong>Precip type</strong>: whether radar precipitation is snow, rain mixed with hail, or one of five kinds of rain, estimated from radar and model temperatures. Point at or tap a colour in its scale to see what it means. Sleet and freezing rain have no colour of their own.</li>
                <li><strong>Temperature</strong>: the National Weather Service forecast for the next few hours, not measured readings. It doesn't animate, and uses your °F or °C setting for its scale.</li>
                <li><strong>Clouds</strong>: infrared satellite images, which work day and night.</li>
                <li><strong>Lightning &amp; warnings</strong>: lightning strikes in each 15 minutes, over current NWS warnings, watches and advisories.</li>
            </ul>
            <p>
                Use the area buttons to jump to a region, <strong>Play</strong> to animate recent images, or the slider to pick a
                time. The map always starts on Radar.
            </p>

            <h2>Temperature Forecast</h2>
            <p>
                The <RouteLink route="/forecast" onNavigate={onNavigate}>Temperature Forecast</RouteLink> page puts daily lows and highs
                from several forecast sources side by side for any US ZIP code.
            </p>
            <ul>
                <li>Enter a ZIP code and press <strong>Go</strong> to load the table. The box starts on the current station's ZIP code.</li>
                <li>Each source has a Low and a High column. Use <strong>Show</strong> to hide either one.</li>
                <li>Sources go by short names, the same on the Snow Forecast page. Hover over a name, or press and hold it, to see what the source is.</li>
                <li>
                    The low is that morning's low, from 7pm the evening before to 9am, and the high is that afternoon's, from
                    9am to 7pm. A day's coldest or warmest hour can fall outside those times when a front passes, so they
                    can differ from a midnight-to-midnight low or high. Sources that give day and night forecasts, such as
                    NWS and NewsCenter Maine, show each night's low on the morning it ends.
                </li>
                <li>Coloured cells are well above or below the average low or high for the date. The legend shows the thresholds.</li>
                <li><strong>Consensus</strong> is the average of the sources shown, with the lowest and highest of them beneath. Hiding a source leaves it out, and it needs at least two sources for a date. It isn't coloured.</li>
                <li>Sources with the most days of forecast come first. Use the column list to hide sources, and drag a column heading to move it. These changes reset when you reload the page.</li>
                <li>Blank cells are normal. Sources forecast different numbers of days (HRRR and HRDPS only about 2), NBM and HRRR cover the US only, and NewsCenter Maine covers Maine ZIP codes only.</li>
                <li>A source that can't be reached shows an error in its column, or its last good data if it has any.</li>
            </ul>

            <h2>Snow Forecast</h2>
            <p>
                The <RouteLink route="/snow" onNavigate={onNavigate}>Snow Forecast</RouteLink> page compares how much snow each
                source expects for one storm, across several towns, with the winter alerts for those towns.
            </p>
            <ul>
                <li>Choose the storm's start and end dates (up to 7 days) and press <strong>Go</strong>. Totals cover midnight to midnight on each date.</li>
                <li>The towns start as Kittery, Portland, Brunswick, Lewiston, Bethel, Rockland and Bangor. Remove any, or add up to 8 ZIP codes. Your choice is saved in this browser.</li>
                <li>Where a forecast's text gives a range, such as "1 to 3 inches" from NWS or Weather Underground, it fills Low and High. Other sources give one amount, shown in both.</li>
                <li>≥ marks a forecast that doesn't cover all of your dates, so it's a minimum. That happens when a forecast ends first, such as WeatherAPI.com's 3 days, or when it has dropped hours already past, as NWS and Weather Underground do with today. The averages leave these out.</li>
                <li>NewsCenter Maine, WMTW and WGME publish snow totals only as maps, so their rows are for typing in their numbers. Those values are saved in this browser until you press <strong>Clear manual values</strong>, and count towards the averages. You can rename, add or remove these rows.</li>
                <li>Use the source list to hide rows from the table and the averages. Links below the alerts open each outlet's snow maps.</li>
            </ul>

            <h2>Settings</h2>
            <p>
                Open Settings from the gear icon in the top bar, or from the menu on small screens. Choose a colour theme (Auto
                follows your device) and °F or °C. Your choices are saved in this browser.
            </p>
            <p>
                On small touch screens, swipe right on the page to open the menu and left to close it. The radar map, the
                forecast table and other parts you can drag sideways don't open it.
            </p>

            <h2>Android app</h2>
            <p>
                The app works like this site, using your own keys. Setup asks for a ZIP code, then optional Weather Underground,
                Ambient Weather and WeatherAPI.com keys, and which stations to include. Anything you skip uses Open-Meteo instead. To change
                them later, open <strong>Data &amp; keys</strong> in Settings.
            </p>

            <h2>Data sources</h2>
            <ul>
                <li>Station readings: Ambient Weather and <a href="https://www.wunderground.com/">Weather Underground</a>, with modelled estimates from <a href="https://open-meteo.com/">Open-Meteo</a></li>
                <li>Forecasts: <a href="https://open-meteo.com/">Open-Meteo</a> (ECMWF, ECMWF AIFS, GFS, NBM, HRRR, GEM, HRDPS, ICON, UKMO, JMA, Météo-France, CMA and WeatherNext models, <a href="https://creativecommons.org/licenses/by/4.0/">CC BY 4.0</a>), <a href="https://www.weather.gov/">National Weather Service</a>, Weather Underground and <a href="https://www.newscentermaine.com/">NewsCenter Maine</a></li>
                <li>Snow forecasts: <a href="https://www.weather.gov/">National Weather Service</a>, Weather Underground, <a href="https://www.weatherapi.com/" title="Free Weather API">WeatherAPI.com</a> and Open-Meteo</li>
                <li>Alerts, radar and the weather map: <a href="https://www.weather.gov/">National Weather Service</a> and NOAA, including <a href="https://nowcoast.noaa.gov/">nowCOAST</a> satellite and lightning</li>
                <li>Averages, records and climate station measurements: <a href="https://www.rcc-acis.org/">ACIS</a></li>
                <li>Sun and moon data: <a href="https://aa.usno.navy.mil/">US Naval Observatory</a></li>
                <li>ZIP code locations: <a href="https://www.zippopotam.us/">Zippopotam.us</a></li>
                <li>Map: <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors</li>
            </ul>

            <p>
                See also the <RouteLink route="/terms" onNavigate={onNavigate}>Terms of Use</RouteLink> and <RouteLink route="/privacy" onNavigate={onNavigate}>Privacy Policy</RouteLink>.
            </p>
        </main>
    );
}

export default HelpPage;
