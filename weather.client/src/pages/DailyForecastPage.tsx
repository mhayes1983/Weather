import { useEffect, useState } from 'react';
import { fetchDailyForecast, type DailyForecastReport, type ForecastDay } from '../api/dailyForecast';
import RouteLink from '../components/RouteLink';
import UnitToggle from '../components/UnitToggle';
import { formatDate } from '../localTime';
import { displayTemperature, type Unit } from '../temperature';
import type { Route } from '../useRoute';
import { weatherIcon } from '../weatherIcons';
import { compass } from '../wind';

interface DailyForecastPageProps {
    station: string;
    unit: Unit;
    onUnitChange: (unit: Unit) => void;
    onNavigate: (route: Route) => void;
}

function DailyForecastPage({ station, unit, onUnitChange, onNavigate }: DailyForecastPageProps) {
    const [report, setReport] = useState<DailyForecastReport>();
    const [error, setError] = useState<string>();

    useEffect(() => {
        const controller = new AbortController();

        fetchDailyForecast(station, controller.signal)
            .then(data => {
                setReport(data);
                setError(undefined);
            })
            .catch((reason: Error) => {
                if (!controller.signal.aborted) {
                    setError(reason.message);
                }
            });

        return () => controller.abort();
    }, [station]);

    const days = report?.days ?? [];
    const lows = days.flatMap(day => day.lowF ?? []);
    const highs = days.flatMap(day => day.highF ?? []);
    const coldest = Math.min(...lows, ...highs);
    const warmest = Math.max(...lows, ...highs);
    const span = warmest - coldest || 1;

    function temperature(fahrenheit: number | null) {
        return fahrenheit === null ? '—' : `${displayTemperature(fahrenheit, unit)}°`;
    }

    function dayContent(day: ForecastDay) {
        const low = day.lowF ?? day.highF ?? coldest;
        const high = day.highF ?? day.lowF ?? coldest;
        const direction = compass(day.windDirectionDegrees);

        return (
            <>
                <span className="daily-date">
                    {day.date === report?.today ? 'Today' : formatDate(day.date, { weekday: 'short', month: 'numeric', day: 'numeric' })}
                </span>
                <span className="daily-icon" aria-hidden="true">{weatherIcon(day.icon)}</span>
                <span className="daily-conditions">{day.conditions}</span>
                <span className="daily-rain">
                    {day.precipChancePercent ?? '—'}%
                    {day.precipIn !== null && day.precipIn > 0 && <> · {day.precipIn.toFixed(2)} in</>}
                </span>
                <span className="daily-low">{temperature(day.lowF)}</span>
                <span className="daily-range" aria-hidden="true">
                    <span style={{
                        left: `${(low - coldest) / span * 100}%`,
                        width: `${(high - low) / span * 100}%`,
                    }} />
                </span>
                <span className="daily-high">{temperature(day.highF)}</span>
                <span className="daily-wind">
                    {day.windMph === null ? '—' : `${Math.round(day.windMph)} mph`} {direction}
                </span>
            </>
        );
    }

    return (
        <main>
            <header>
                <h1>Daily Forecast</h1>
                {report &&
                    <p className="location">
                        {report.stationName} · <RouteLink route="/" onNavigate={onNavigate}>Change station</RouteLink>
                    </p>}
            </header>

            <div className="controls">
                <UnitToggle unit={unit} onChange={onUnitChange} />
            </div>

            {error && <p className="error" role="alert">{error}</p>}
            {!report && !error && <p>Loading forecast…</p>}
            {report?.status === 'NeedsKey' && <p className="error" role="alert">Add your Weather Underground key to see this station's forecast.</p>}
            {report?.status === 'Error' && <p className="error" role="alert">{report.message}</p>}

            {days.length > 0 &&
                <>
                    <ol className="daily-list">
                        {days.map(day =>
                            <li key={day.date}>
                                {day.hasHourly
                                    ? <RouteLink className="daily-row" route={`/daily/${day.date}`} onNavigate={onNavigate}>{dayContent(day)}</RouteLink>
                                    : <div className="daily-row">{dayContent(day)}</div>}
                            </li>
                        )}
                    </ol>
                    <footer className="attribution">
                        Weather data by <a href="https://open-meteo.com/">Open-Meteo.com</a> (<a href="https://creativecommons.org/licenses/by/4.0/">CC BY 4.0</a>)
                    </footer>
                </>}
        </main>
    );
}

export default DailyForecastPage;
