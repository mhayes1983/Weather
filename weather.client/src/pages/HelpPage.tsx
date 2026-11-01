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
                current day for other stations.
            </p>
            <p>
                When the National Weather Service has active alerts for the station's area, they appear at the top of the
                page. If alerts can't be checked, the page says so. That doesn't mean there are no alerts.
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
                Precipitation amounts are the rain and melted snow combined. When snow is expected, its depth is shown
                separately, because it's usually several times the melted amount.
            </p>

            <h2>Radar</h2>
            <p>
                The <RouteLink route="/radar" onNavigate={onNavigate}>Radar</RouteLink> page shows recent US radar on a map you
                can drag and zoom. Use the area buttons to jump to a region, <strong>Play</strong> to animate the last two hours,
                or the slider to pick a time. Radar shows precipitation only, and snow can look weaker than it is.
            </p>

            <h2>Forecast Comparison</h2>
            <p>
                The <RouteLink route="/forecast" onNavigate={onNavigate}>Forecast Comparison</RouteLink> page puts daily highs
                from several forecast sources side by side for any US ZIP code.
            </p>
            <ul>
                <li>Coloured cells are days well above or below the average high for the date. The legend shows the thresholds.</li>
                <li>Use the column list to hide sources, and drag a column heading to move it. These changes reset when you reload the page.</li>
                <li>Blank cells are normal. Sources forecast different numbers of days, and NewsCenter Maine covers Maine ZIP codes only.</li>
                <li>A source that can't be reached shows an error in its column, or its last good data if it has any.</li>
            </ul>

            <h2>Settings</h2>
            <p>
                Open Settings from the gear icon in the top bar, or from the menu on small screens. Choose a colour theme (Auto
                follows your device) and °F or °C. Your choices are saved in this browser.
            </p>

            <h2>Android app</h2>
            <p>
                The app works like this site, using your own keys. Setup asks for a ZIP code, then optional Weather Underground
                and Ambient Weather keys, and which stations to include. Anything you skip uses Open-Meteo instead. To change
                them later, open <strong>Data &amp; keys</strong> in Settings.
            </p>

            <h2>Data sources</h2>
            <ul>
                <li>Station readings: Ambient Weather and <a href="https://www.wunderground.com/">Weather Underground</a>, with modelled estimates from <a href="https://open-meteo.com/">Open-Meteo</a></li>
                <li>Forecasts: <a href="https://open-meteo.com/">Open-Meteo</a> (ECMWF, GFS, GEM, ICON and WeatherNext models, <a href="https://creativecommons.org/licenses/by/4.0/">CC BY 4.0</a>), <a href="https://www.weather.gov/">National Weather Service</a>, Weather Underground and <a href="https://www.newscentermaine.com/">NewsCenter Maine</a></li>
                <li>Alerts and radar: <a href="https://www.weather.gov/">National Weather Service</a> and NOAA</li>
                <li>Average highs: <a href="https://www.rcc-acis.org/">ACIS</a></li>
                <li>Moon data: <a href="https://aa.usno.navy.mil/">US Naval Observatory</a></li>
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
