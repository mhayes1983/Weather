import RouteLink from '../components/RouteLink';
import type { Route } from '../useRoute';

interface TermsPageProps {
    onNavigate: (route: Route) => void;
}

function TermsPage({ onNavigate }: TermsPageProps) {
    return (
        <main className="prose">
            <header>
                <h1>Terms of Use</h1>
                <p className="location">Effective November 1, 2026</p>
            </header>

            <p>
                By using this site you agree to these terms. If you don't agree, please don't use the site. "We" means the
                site operator.
            </p>

            <h2>Personal use</h2>
            <p>This site is for personal, non-commercial use. Some of its data is licensed on that basis.</p>

            <h2>Not for safety decisions</h2>
            <p>
                Don't rely on this site to protect life or property. Forecasts, readings, radar and alerts can be wrong, late,
                incomplete or unavailable. For severe weather, use official sources such as
                the <a href="https://www.weather.gov/">National Weather Service</a> and follow the instructions of local
                authorities.
            </p>

            <h2>Acceptable use</h2>
            <p>Please don't:</p>
            <ul>
                <li>scrape the site or call its API with automated tools</li>
                <li>send repeated requests that could use up its limited allowance with data providers</li>
                <li>try to disrupt the site or reach parts of it that aren't offered to you</li>
                <li>republish its data or present it as your own</li>
            </ul>

            <h2>Third-party data</h2>
            <p>
                Weather and map data comes from the providers listed on the <RouteLink route="/help" onNavigate={onNavigate}>Help</RouteLink> page
                and remains subject to their own terms. This includes Open-Meteo data
                under <a href="https://creativecommons.org/licenses/by/4.0/">CC BY 4.0</a>, map data
                from <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors, and Weather Underground
                data for personal, non-commercial use. We don't control these providers, and they can change or withdraw their
                data at any time. Links to other sites are provided for convenience only.
            </p>

            <h2>No warranty</h2>
            <p>
                The site is provided "as is" and "as available", with no warranties of any kind, express or implied, including
                accuracy, reliability, availability or fitness for a particular purpose.
            </p>

            <h2>Limitation of liability</h2>
            <p>
                To the extent the law allows, we aren't liable for any loss or damage arising from your use of the site, or from
                relying on its information.
            </p>

            <h2>Changes</h2>
            <p>
                We can change these terms, or change, limit or withdraw the site, at any time and without notice. The date above
                shows when the terms last changed. Using the site after a change means you accept the new terms.
            </p>

            <p>
                The <RouteLink route="/privacy" onNavigate={onNavigate}>Privacy Policy</RouteLink> explains what data the site
                handles.
            </p>
        </main>
    );
}

export default TermsPage;
