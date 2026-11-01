import RouteLink from '../components/RouteLink';
import type { Route } from '../useRoute';

interface PrivacyPageProps {
    onNavigate: (route: Route) => void;
}

function PrivacyPage({ onNavigate }: PrivacyPageProps) {
    return (
        <main className="prose">
            <header>
                <h1>Privacy Policy</h1>
                <p className="location">Effective November 1, 2026</p>
            </header>

            <p>
                This site collects almost nothing. It has no accounts, cookies, analytics or advertising, and no database of
                visitors. "We" means the site operator.
            </p>

            <h2>What stays in your browser</h2>
            <p>Your preferences are saved in your browser's local storage. They aren't sent to us or shared between devices.</p>
            <ul>
                <li>Colour theme</li>
                <li>Temperature unit</li>
                <li>Default station</li>
                <li>Stations you've added (up to 20)</li>
            </ul>
            <p>To remove them, clear this site's data in your browser settings.</p>

            <h2>What reaches the server</h2>
            <p>
                When you enter a ZIP code or station ID, or select a station, the site's server uses it to fetch weather data.
                Results are held in memory for a short time, then discarded, and aren't saved to disk. The site doesn't link
                them to you or keep a record of your visits. If a data request fails, a short warning that can include the ZIP
                code or station ID is written to the server's log. The network and hosting equipment that runs the site may keep
                their own routine connection records.
            </p>

            <h2>Data sources the server contacts</h2>
            <p>
                The server requests data on your behalf, sending a ZIP code, station ID or coordinates. Your IP address isn't
                passed on to these services:
            </p>
            <ul>
                <li>Open-Meteo: forecasts, and modelled conditions for a ZIP code or a station's location</li>
                <li>National Weather Service: forecasts and alerts</li>
                <li>Weather Underground: forecasts, station readings and station search</li>
                <li>Ambient Weather: home station readings</li>
                <li>ACIS: average highs</li>
                <li>US Naval Observatory: moon data</li>
                <li>Zippopotam.us: ZIP code locations</li>
                <li>NewsCenter Maine: Maine forecasts</li>
            </ul>

            <h2>Services your browser contacts directly</h2>
            <p>
                The Radar page loads images directly from <a href="https://www.noaa.gov/">NOAA</a> and map tiles
                from <a href="https://www.openstreetmap.org/">OpenStreetMap</a>. Like any website, those services can see your IP
                address, and which map areas you view. Their own privacy policies apply. Other pages don't contact them.
            </p>

            <h2>The Android app</h2>
            <p>
                The Android app has no server. It runs on your phone and contacts the data sources above directly, so they can
                see your IP address, and it uses any Weather Underground and Ambient Weather keys you enter. It also calls NOAA
                and OpenStreetMap for radar, as a browser does.
            </p>
            <ul>
                <li>Your keys are encrypted with Android's secure storage, and aren't sent to us or included in Android backups.</li>
                <li>Your ZIP code and chosen stations are saved on your phone, along with the preferences listed above.</li>
                <li>Uninstalling the app removes all of this. You can also remove your keys from Data &amp; keys in Settings.</li>
            </ul>

            <h2>Changes</h2>
            <p>
                We may update this policy, and the date above shows when it last changed. It works alongside
                the <RouteLink route="/terms" onNavigate={onNavigate}>Terms of Use</RouteLink>.
            </p>
        </main>
    );
}

export default PrivacyPage;
