import { useEffect, useState } from 'react';
import { fetchForecastDay, type ForecastDayDetail } from '../api/dailyForecast';
import CollapsibleCard from '../components/CollapsibleCard';
import RouteLink from '../components/RouteLink';
import UnitToggle from '../components/UnitToggle';
import { formatDate, formatTime } from '../localTime';
import { displayTemperature, type Unit } from '../temperature';
import type { Route } from '../useRoute';
import { weatherIcon } from '../weatherIcons';
import { compass } from '../wind';

interface ForecastDayPageProps {
    date: string;
    station: string;
    unit: Unit;
    onUnitChange: (unit: Unit) => void;
    onNavigate: (route: Route) => void;
}

const missing = '—';
const smallScreen = '(max-width: 720px)';

type CardName = 'day' | 'night' | 'sun' | 'moon';

function presentRows(rows: [string, string | null][]): [string, string][] {
    return rows.filter((row): row is [string, string] => row[1] !== null);
}

function shortDate(date: string) {
    return formatDate(date, { weekday: 'short', month: 'numeric', day: 'numeric' });
}

function ReadingList({ rows }: { rows: [string, string][] }) {
    return (
        <dl>
            {rows.map(([label, value]) =>
                <div key={label}>
                    <dt>{label}</dt>
                    <dd>{value}</dd>
                </div>
            )}
        </dl>
    );
}

// Sun and moon are less important, so they start closed where space is tight.
function initialCards(): Record<CardName, boolean> {
    const large = !window.matchMedia(smallScreen).matches;
    return { day: true, night: true, sun: large, moon: large };
}

function ForecastDayPage({ date, station, unit, onUnitChange, onNavigate }: ForecastDayPageProps) {
    const [loaded, setLoaded] = useState<ForecastDayDetail>();
    const [failure, setFailure] = useState<{ date: string; message: string }>();
    const [openCards, setOpenCards] = useState(initialCards);

    // The page stays mounted between days so open cards carry over, and these
    // checks stop the previous day appearing under the new heading.
    const detail = loaded?.day.date === date ? loaded : undefined;
    const error = failure?.date === date ? failure.message : undefined;

    useEffect(() => {
        const controller = new AbortController();

        fetchForecastDay(date, station, controller.signal)
            .then(setLoaded)
            .catch((reason: Error) => {
                if (!controller.signal.aborted) {
                    setFailure({ date, message: reason.message });
                }
            });

        return () => controller.abort();
    }, [date, station]);

    function temperature(fahrenheit: number | null) {
        return fahrenheit === null ? missing : `${displayTemperature(fahrenheit, unit)}°${unit}`;
    }

    function cardProps(name: CardName) {
        return {
            open: openCards[name],
            onToggle: (open: boolean) => setOpenCards(cards => ({ ...cards, [name]: open })),
        };
    }

    function percent(value: number | null) {
        return value === null ? missing : `${value}%`;
    }

    const day = detail?.day;
    const moon = detail?.moon;
    const sunRows = presentRows([
        ['Sunrise', day?.sunrise ? formatTime(day.sunrise) : null],
        ['Sunset', day?.sunset ? formatTime(day.sunset) : null],
    ]);
    const moonRows = presentRows([
        ['Moonrise', moon?.moonrise ? formatTime(moon.moonrise) : null],
        ['Moonset', moon?.moonset ? formatTime(moon.moonset) : null],
        ['Phase', moon?.phase ?? null],
        ['Visible', moon?.illuminationPercent != null ? `${moon.illuminationPercent}%` : null],
    ]);

    return (
        <main>
            <p className="back-link">
                <RouteLink route="/daily" onNavigate={onNavigate}>← Daily Forecast</RouteLink>
            </p>

            {detail &&
                <nav className="day-nav" aria-label="Days">
                    {detail.previousDate
                        ? <RouteLink route={`/daily/${detail.previousDate}`} onNavigate={onNavigate}>‹ {shortDate(detail.previousDate)}</RouteLink>
                        : <span />}
                    {detail.nextDate &&
                        <RouteLink route={`/daily/${detail.nextDate}`} onNavigate={onNavigate}>{shortDate(detail.nextDate)} ›</RouteLink>}
                </nav>}

            <header>
                <h1>{formatDate(date, { weekday: 'long', month: 'long', day: 'numeric' })}</h1>
                {detail && <p className="location">{detail.stationName}</p>}
            </header>

            {detail &&
                <div className="controls">
                    <p className="day-summary">
                        <span aria-hidden="true">{weatherIcon(detail.day.icon)}</span> {detail.day.conditions}
                        {' · '}High {temperature(detail.day.highF)}, low {temperature(detail.day.lowF)}
                        {detail.day.uvIndex !== null && <> · UV {Math.round(detail.day.uvIndex)}</>}
                    </p>
                    <UnitToggle unit={unit} onChange={onUnitChange} />
                </div>}

            {error && <p className="error" role="alert">{error}</p>}
            {!detail && !error && <p>Loading forecast…</p>}

            {detail &&
                <>
                    {(detail.dayDescription || detail.nightDescription) &&
                        <div className="day-sections">
                            {detail.dayDescription &&
                                <CollapsibleCard title="Day" {...cardProps('day')}>
                                    <p>{detail.dayDescription}</p>
                                </CollapsibleCard>}
                            {detail.nightDescription &&
                                <CollapsibleCard title="Night" {...cardProps('night')}>
                                    <p>{detail.nightDescription}</p>
                                </CollapsibleCard>}
                        </div>}

                    {(sunRows.length > 0 || moonRows.length > 0) &&
                        <div className="day-sections">
                            {sunRows.length > 0 &&
                                <CollapsibleCard title="Sun" {...cardProps('sun')}>
                                    <ReadingList rows={sunRows} />
                                </CollapsibleCard>}
                            {moonRows.length > 0 &&
                                <CollapsibleCard title="Moon" {...cardProps('moon')}>
                                    <ReadingList rows={moonRows} />
                                </CollapsibleCard>}
                        </div>}

                    <div className="table-scroll">
                        <table className="hourly-table">
                            <thead>
                                <tr>
                                    <th scope="col">Time</th>
                                    <th scope="col">Conditions</th>
                                    <th scope="col">Temp.</th>
                                    <th scope="col">Feels Like</th>
                                    <th scope="col">Precip</th>
                                    <th scope="col">Amount</th>
                                    <th scope="col">Cloud Cover</th>
                                    <th scope="col">Dew Point</th>
                                    <th scope="col">Humidity</th>
                                    <th scope="col">Wind</th>
                                    <th scope="col">Pressure</th>
                                </tr>
                            </thead>
                            <tbody>
                                {detail.hours.map((hour, index) =>
                                    <tr key={`${hour.time}-${index}`}>
                                        <th scope="row">{formatTime(hour.time)}</th>
                                        <td className="hourly-conditions">
                                            <span aria-hidden="true">{weatherIcon(hour.icon)}</span> {hour.conditions}
                                        </td>
                                        <td>{temperature(hour.temperatureF)}</td>
                                        <td>{temperature(hour.feelsLikeF)}</td>
                                        <td>{percent(hour.precipChancePercent)}</td>
                                        <td>{hour.precipIn === null ? missing : `${hour.precipIn.toFixed(2)} in`}</td>
                                        <td>{percent(hour.cloudCoverPercent)}</td>
                                        <td>{temperature(hour.dewPointF)}</td>
                                        <td>{percent(hour.humidityPercent)}</td>
                                        <td>{hour.windMph === null ? missing : `${Math.round(hour.windMph)} mph`} {compass(hour.windDirectionDegrees)}</td>
                                        <td>{hour.pressureInHg === null ? missing : `${hour.pressureInHg.toFixed(2)} in`}</td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>

                    <footer className="attribution">
                        Weather data by <a href="https://open-meteo.com/">Open-Meteo.com</a> (<a href="https://creativecommons.org/licenses/by/4.0/">CC BY 4.0</a>).
                        {' '}Descriptions from the <a href="https://www.weather.gov/">National Weather Service</a>.
                        {' '}Moon data from the <a href="https://aa.usno.navy.mil/">U.S. Naval Observatory</a>.
                    </footer>
                </>}
        </main>
    );
}

export default ForecastDayPage;
